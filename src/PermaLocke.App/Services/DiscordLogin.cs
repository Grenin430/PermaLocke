using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Services;

/// <summary>Who is signed in, as the tournament server knows them.</summary>
public sealed record DiscordAccount(string Name, string? AvatarUrl, string RefreshToken, string DiscordId = "", bool Allowed = false,
    Guid UserId = default);

/// <summary>
/// Signs the player in with Discord through the tournament's Supabase project (phase 1: identity only, nothing of the
/// run is sent).
/// </summary>
/// <remarks>
/// <para>
/// OAuth with PKCE, the standard for desktop apps: the browser does the Discord part and comes back to
/// <c>http://127.0.0.1:{port}/callback</c>, where this class listens for one request. The app never sees the Discord
/// password, and the only key it carries (<c>Data/torneo.json</c>) is Supabase's public one, which is safe to ship:
/// what protects the data is row-level security on the server.
/// </para>
/// <para>
/// The refresh token is kept in <c>Config/discord.json</c>, encrypted with DPAPI for the current Windows user, so a
/// copied Config folder does not carry a working session to another PC.
/// </para>
/// </remarks>
public sealed class DiscordLogin(AppPaths paths, ILogger<DiscordLogin> logger)
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly TimeSpan BrowserWait = TimeSpan.FromMinutes(3);

    private sealed record ServerConfig(string Servidor, string ClavePublica, int Puerto);

    private string? _access;
    private DateTimeOffset _accessUntil;

    private string SessionPath => Path.Combine(paths.Config, "discord.json");

    private ServerConfig Config =>
        JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(Path.Combine(paths.Data, "torneo.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Data/torneo.json está vacío.");

    /// <summary>The saved account, or null if nobody has signed in on this PC.</summary>
    public DiscordAccount? Saved
    {
        get
        {
            try
            {
                if (!File.Exists(SessionPath)) return null;
                var json = Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    File.ReadAllBytes(SessionPath), null, DataProtectionScope.CurrentUser));
                return JsonSerializer.Deserialize<DiscordAccount>(json);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo leer la sesión de Discord guardada");
                return null;
            }
        }
    }

    /// <summary>Opens the browser, waits for Discord to send the player back and keeps the session.</summary>
    public async Task<DiscordAccount> SignInAsync(CancellationToken cancel = default)
    {
        var config = Config;
        var redirect = $"http://127.0.0.1:{config.Puerto}/callback";
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));

        using var listener = new HttpListener();
        listener.Prefixes.Add(redirect + "/");
        listener.Start();

        var authorize = $"{config.Servidor}/auth/v1/authorize?provider=discord&scopes=identify"
                        + $"&redirect_to={Uri.EscapeDataString(redirect)}"
                        + $"&code_challenge={Challenge(verifier)}&code_challenge_method=s256";
        Process.Start(new ProcessStartInfo(authorize) { UseShellExecute = true })?.Dispose();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(BrowserWait);
        var context = await listener.GetContextAsync().WaitAsync(timeout.Token);

        var code = context.Request.QueryString["code"];
        var error = context.Request.QueryString["error_description"] ?? context.Request.QueryString["error"];
        await Answer(context.Response, code is null
            ? "No se ha podido entrar. Vuelve a PermaLocke e inténtalo otra vez."
            : "Listo. Ya puedes cerrar esta pestaña y volver a PermaLocke.");

        if (code is null)
        {
            throw new InvalidOperationException(error ?? "Discord no ha devuelto ningún código.");
        }

        return await FinishAsync(config, await TokenAsync(config, "pkce", new { auth_code = code, code_verifier = verifier }));
    }

    /// <summary>Checks the saved session against the server and renews it; null if it no longer works.</summary>
    public async Task<DiscordAccount?> RefreshAsync()
    {
        if (Saved is not { } saved) return null;

        try
        {
            var config = Config;
            return await FinishAsync(config, await TokenAsync(config, "refresh_token", new { refresh_token = saved.RefreshToken }));
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            // El servidor ha dicho que no: la sesión ya no vale. Sin conexión no se borra nada.
            logger.LogInformation(ex, "La sesión de Discord ha caducado");
            SignOut();
            return null;
        }
    }

    /// <summary>
    /// Sends JSON to the tournament's REST API as the signed-in player; the server's row-level security decides what
    /// is accepted. Returns false when nobody is signed in.
    /// </summary>
    public async Task<bool> PostAsync(string path, string json, string? prefer = null, CancellationToken cancel = default) =>
        await SendAsync(HttpMethod.Post, path, json, prefer, cancel) is not null;

    /// <summary>Reads from the tournament's REST API as the signed-in player: JSON text, or null when nobody is signed in.</summary>
    public Task<string?> GetAsync(string path, CancellationToken cancel = default) =>
        SendAsync(HttpMethod.Get, path, null, null, cancel);

    /// <summary>Calls a function of the tournament's database as the signed-in player: its answer as JSON text, or null when nobody is signed in.</summary>
    public Task<string?> CallAsync(string function, string json = "{}", CancellationToken cancel = default) =>
        SendAsync(HttpMethod.Post, $"rpc/{function}", json, null, cancel);

    /// <summary>Deletes rows through the tournament's REST API as the signed-in player; the server decides whether it may.</summary>
    public Task<string?> DeleteAsync(string path, CancellationToken cancel = default) =>
        SendAsync(HttpMethod.Delete, path, null, null, cancel);

    /// <summary>Changes rows through the tournament's REST API as the signed-in player; the server decides whether it may.</summary>
    public Task<string?> PatchAsync(string path, string json, CancellationToken cancel = default) =>
        SendAsync(HttpMethod.Patch, path, json, null, cancel);

    private async Task<string?> SendAsync(HttpMethod method, string path, string? json, string? prefer, CancellationToken cancel)
    {
        if (_access is null || DateTimeOffset.UtcNow >= _accessUntil)
        {
            if (await RefreshAsync() is null) return null;
        }

        var config = Config;
        using var request = new HttpRequestMessage(method, $"{config.Servidor}/rest/v1/{path}");
        if (json is not null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Add("apikey", config.ClavePublica);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _access);
        if (prefer is not null) request.Headers.Add("Prefer", prefer);

        using var response = await Http.SendAsync(request, cancel);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancel);
    }

    /// <summary>Forgets the session on this PC.</summary>
    public void SignOut()
    {
        _access = null;
        if (File.Exists(SessionPath)) File.Delete(SessionPath);
    }

    /// <summary>The PKCE challenge for a verifier: base64url of its SHA-256 (RFC 7636).</summary>
    internal static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task<JsonNode> TokenAsync(ServerConfig config, string grant, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.Servidor}/auth/v1/token?grant_type={grant}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("apikey", config.ClavePublica);

        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())
               ?? throw new InvalidDataException("El servidor ha respondido vacío.");
    }

    /// <summary>
    /// Keeps the session and asks the server whether this Discord account is on the tournament's whitelist.
    /// </summary>
    /// <remarks>
    /// The list lives in the server's <c>whitelist</c> table, which the public key cannot read: only the
    /// <c>permitido()</c> function answers, and only about the account that is asking. The same check will guard
    /// what the server accepts in phase 2, which is where it actually protects something; the gate in the
    /// application only keeps out people who do not change the code.
    /// </remarks>
    private async Task<DiscordAccount> FinishAsync(ServerConfig config, JsonNode token)
    {
        var meta = token["user"]?["user_metadata"];
        var access = (string?)token["access_token"] ?? throw new InvalidDataException("El servidor no ha devuelto sesión.");
        _access = access;
        _accessUntil = DateTimeOffset.UtcNow.AddSeconds(((int?)token["expires_in"] ?? 3600) - 60);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.Servidor}/rest/v1/rpc/permitido")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("apikey", config.ClavePublica);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", access);
        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var allowed = (await response.Content.ReadAsStringAsync()).Trim() == "true";

        var account = new DiscordAccount(
            (string?)meta?["custom_claims"]?["global_name"] ?? (string?)meta?["full_name"] ?? (string?)meta?["name"] ?? "Jugador",
            (string?)meta?["avatar_url"],
            (string?)token["refresh_token"] ?? throw new InvalidDataException("El servidor no ha devuelto sesión."),
            (string?)meta?["provider_id"] ?? "",
            allowed,
            Guid.TryParse((string?)token["user"]?["id"], out var userId) ? userId : Guid.Empty);

        Directory.CreateDirectory(paths.Config);
        File.WriteAllBytes(SessionPath, ProtectedData.Protect(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(account)), null, DataProtectionScope.CurrentUser));
        return account;
    }

    private static async Task Answer(HttpListenerResponse response, string message)
    {
        var html = Encoding.UTF8.GetBytes(
            $"<!doctype html><meta charset=utf-8><title>PermaLocke</title><body style=\"font:18px sans-serif;background:#120e20;color:#eee;padding:40px\">{WebUtility.HtmlEncode(message)}</body>");
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = html.Length;
        await response.OutputStream.WriteAsync(html);
        response.Close();
    }
}
