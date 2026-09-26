using System.Net;
using System.Net.Http;
using System.Text.Json;
using PermaLocke.App.Services;
using PermaLocke.Core.Domain;

namespace PermaLocke.Admin.Services;

/// <summary>
/// A run's history as the server holds it, wherever that is (§194): inside <c>runs.history</c> for the apps that upload
/// it whole, or one row per event in <c>eventos</c> for the ones that only upload what is new.
/// </summary>
public static class ServerHistory
{
    /// <summary>Rows per request: PostgREST hands out at most a thousand at a time.</summary>
    private const int Page = 1000;

    private sealed record EventRow(GameEvent Evento);

    private sealed record ClaimRow(Guid Run_id, GameEvent Evento);

    /// <summary>
    /// The history of a run with its events, reading them from <c>eventos</c> when <c>runs.history</c> has none but
    /// the summary says there are. As it was when the server has no <c>eventos</c> (SQL 15 not run).
    /// </summary>
    public static async Task<RunHistory?> CompleteAsync(DiscordLogin discord, RunHistory? history, RunSnapshot snapshot,
        JsonSerializerOptions json)
    {
        if (history is { Events.Count: > 0 } || snapshot.EventCount == 0)
        {
            return history;
        }

        var events = new List<GameEvent>();

        for (var offset = 0; ; offset += Page)
        {
            string? page;
            try
            {
                page = await discord.GetAsync($"eventos?select=evento&run_id=eq.{snapshot.RunId}&order=n&limit={Page}&offset={offset}");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return history;
            }

            if (page is null)
            {
                return history;
            }

            var rows = JsonSerializer.Deserialize<List<EventRow>>(page, json) ?? [];
            events.AddRange(rows.Select(row => row.Evento));

            if (rows.Count < Page)
            {
                break;
            }
        }

        return events.Count == 0
            ? history
            : (history ?? new RunHistory { RunId = snapshot.RunId }) with { Events = events };
    }

    /// <summary>The gifts collected in the runs that upload event by event, by run: <c>AdminGiftClaimed</c> only.</summary>
    public static async Task<ILookup<Guid, GameEvent>> ClaimsAsync(DiscordLogin discord, JsonSerializerOptions json)
    {
        try
        {
            var page = await discord.GetAsync($"eventos?select=run_id,evento&evento->>type=eq.{nameof(GameEventType.AdminGiftClaimed)}");
            return (JsonSerializer.Deserialize<List<ClaimRow>>(page ?? "[]", json) ?? []).ToLookup(row => row.Run_id, row => row.Evento);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return Array.Empty<ClaimRow>().ToLookup(row => row.Run_id, row => row.Evento);
        }
    }
}
