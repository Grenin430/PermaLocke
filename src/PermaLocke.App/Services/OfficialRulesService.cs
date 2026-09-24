using System.IO;
using Microsoft.Extensions.Logging;
using PermaLocke.Core.Abstractions;
using PermaLocke.Core.Domain;
using PermaLocke.Data;
using PermaLocke.Infrastructure;
using PermaLocke.Randomizer;
using PermaLocke.Rules;

namespace PermaLocke.App.Services;

/// <param name="Files">Each official file and how it compares with this machine's.</param>
/// <param name="Message">One line for the top of the panel.</param>
public sealed record RulesStatus(IReadOnlyList<RuleFileStatus> Files, string Message)
{
    public int Pending => Files.Count(f => f.CanAdopt);

    public bool CanAdopt => Pending > 0 && Files.All(f => f.State != RuleFileState.Unreadable);
}

/// <summary>
/// The official rules the admin leaves in the competition folder, and adopting them on this machine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each file is validated with the loader the application itself uses at start-up</b> before it is
/// offered. The list lives here and not in <c>PermaLocke.Data</c> because two of those loaders are in
/// projects Data does not reference (the rules engine and the randomizer), and a check that used a
/// different parser from the one that will read the file tomorrow would be checking something else.
/// </para>
/// <para>
/// The services that read these files are singletons built at start-up, so adopting takes effect on
/// the next start and the panel says so. Hot-swapping every catalogue under a running battle
/// watcher is not worth what it would risk (§123).
/// </para>
/// </remarks>
public sealed class OfficialRulesService(
    SyncService sync,
    IRunContext runContext,
    IEventStore events,
    IClock clock,
    IAbilityLookup abilities,
    AppPaths paths,
    ILogger<OfficialRulesService> logger)
{
    public string? RulesFolder => sync.SharedFolder is { Length: > 0 } root ? CompetitionLayout.Rules(root) : null;

    public RulesStatus Status()
    {
        if (RulesFolder is not { } folder)
        {
            return new RulesStatus([], "Elige la carpeta compartida.");
        }

        if (!Directory.Exists(folder))
        {
            return new RulesStatus([], "Todavía no hay reglas oficiales.");
        }

        var files = OfficialRules.Compare(folder, paths.Data, Validate);

        return new RulesStatus(files, files.Count switch
        {
            0 => "Todavía no hay reglas oficiales.",
            _ when files.Any(f => f.State == RuleFileState.Unreadable) =>
                "Las reglas oficiales tienen un error. Avisa al admin.",
            _ when files.All(f => f.State == RuleFileState.Same) => "Tienes las reglas oficiales.",
            _ => "Hay reglas oficiales nuevas."
        });
    }

    /// <summary>Copies the official rules over this machine's, keeping a copy, and records it.</summary>
    public async Task<string> AdoptAsync(CancellationToken ct = default)
    {
        if (RulesFolder is not { } folder || !Directory.Exists(folder))
        {
            return "No hay reglas oficiales que adoptar.";
        }

        var result = OfficialRules.Adopt(folder, paths.Data, paths.SaveBackups, Validate, clock.Now);

        if (result.Adopted.Count == 0)
        {
            return result.Problem;
        }

        logger.LogInformation("Reglas oficiales adoptadas: {Files}. Copia en {Backup}",
            string.Join(", ", result.Adopted.Select(a => a.Name)), result.Backup ?? "(no hacía falta)");

        // Regla 4: con que precios y que logros se jugo una run es parte de su historia. Sin run
        // cargada no hay cadena en la que escribirlo, y el log es lo unico que queda.
        if (runContext.Current is { } run)
        {
            var data = new Dictionary<string, string>
            {
                ["ficheros"] = string.Join(",", result.Adopted.Select(a => a.Name)),
                ["copia"] = result.Backup ?? string.Empty
            };

            foreach (var file in result.Adopted)
            {
                data[$"{file.Name}:antes"] = file.Before;
                data[$"{file.Name}:despues"] = file.After;
            }

            await events.AppendAsync(new GameEvent
            {
                Id = Guid.NewGuid(),
                RunId = run.Id,
                Timestamp = clock.Now,
                Type = GameEventType.RulesAdopted,
                Source = EventSource.Player,
                Actor = run.PlayerName,
                Description = $"Reglas oficiales adoptadas: {string.Join(", ", result.Adopted.Select(a => a.Name))}.",
                Data = data
            }, ct);
        }

        var regenerate = result.Adopted.Where(a => a.Kind == RuleFileKind.NeedsRegeneration).ToList();

        return (result.Problem.Length > 0 ? result.Problem + " " : string.Empty)
               + "Reglas adoptadas. Cierra y vuelve a abrir PermaLocke."
               + (regenerate.Count > 0
                   ? $" {string.Join(" y ", regenerate.Select(r => r.Name))} "
                     + "necesitan volver a generar e instalar tu mundo."
                   : string.Empty);
    }

    /// <summary>Loads one rules file with the application's own loader. Null when it loads.</summary>
    private string? Validate(string name, string path)
    {
        try
        {
            switch (name.ToLowerInvariant())
            {
                case "achievements.json": JsonAchievementCatalog.Load(path); break;
                case "gacha.json": JsonGachaCatalog.Load(path); break;
                case "grants.json": JsonCreditCatalog.Load(path); break;
                case "levelcaps.json": LevelCapTable.Load(path); break;
                case "penalties.json": JsonPenaltyCatalog.Load(path); break;
                case "rewards.json": JsonRewardCatalog.Load(path); break;
                case "roulette.json": JsonRouletteCatalog.Load(path, abilities); break;
                case "rules.json": RulesConfigurationLoader.Load(path); break;
                case "shop.json": JsonShopCatalog.Load(path); break;
                case "wondertrade.json": JsonWonderTradeCatalog.Load(path); break;
                case "randomizer.json": RandomizerOptionsLoader.Load(path); break;
                case "roles.json": JsonRoleCatalog.Load(path); break;
                default: return "No es un fichero de reglas.";
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "La regla oficial {File} no se puede cargar", name);
            return ex.Message;
        }
    }
}
