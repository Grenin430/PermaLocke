namespace PermaLocke.App.Services;

/// <summary>
/// The tournament server as Admin talks to it: the signed-in account and the REST, function and Storage calls.
/// </summary>
/// <remarks>
/// <see cref="DiscordLogin"/> is the real one. Admin also has a demo server with made-up players (<c>--demo</c>), for
/// looking at its screens without touching anybody's data; it says so on screen.
/// </remarks>
public interface ITournamentServer
{
    DiscordAccount? Saved { get; }

    Task<DiscordAccount> SignInAsync(CancellationToken cancel = default);

    void SignOut();

    Task<string?> GetAsync(string path, CancellationToken cancel = default);

    Task<bool> PostAsync(string path, string json, string? prefer = null, CancellationToken cancel = default);

    Task<string?> CallAsync(string function, string json = "{}", CancellationToken cancel = default);

    Task<string?> DeleteAsync(string path, CancellationToken cancel = default);

    Task<string?> PatchAsync(string path, string json, CancellationToken cancel = default);

    Task<byte[]?> DownloadAsync(string bucket, string path, CancellationToken cancel = default);

    Task<string?> ListAsync(string bucket, string folder, CancellationToken cancel = default);

    Task<bool> RemoveAsync(string bucket, IReadOnlyList<string> paths, CancellationToken cancel = default);
}
