using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using PermaLocke.App.Services;
using PermaLocke.Infrastructure;

namespace PermaLocke.App.Tests;

public sealed class SpritePreparationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "permalocke-sprite-test-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task Adding_a_rom_after_opening_the_app_retries_preparation()
    {
        var paths = new AppPaths(_root);
        var log = new SpriteLog();
        var service = new PokemonSpriteService(paths, log);
        await service.PrepareAsync();
        Assert.False(service.IsAvailable);
        // Recognisable header but no RomFS: trying extraction must now reach the error handler.
        WriteHeaderOnlyRom(paths.Rom);
        await service.PrepareAsync();
        Assert.Single(log.Failures);
        Assert.False(service.IsAvailable);
    }

    [Fact]
    public async Task A_failed_extraction_is_not_permanently_marked_ready()
    {
        var paths = new AppPaths(_root);
        WriteHeaderOnlyRom(paths.Rom);
        var log = new SpriteLog();
        var service = new PokemonSpriteService(paths, log);
        await service.PrepareAsync();
        await service.PrepareAsync();
        Assert.Equal(2, log.Failures.Count);
        Assert.False(service.IsAvailable);
    }

    [Fact]
    public void A_different_or_replaced_source_does_not_reuse_stale_sprites()
    {
        Directory.CreateDirectory(_root);
        var rom = Path.Combine(_root, "game.3ds");
        File.WriteAllText(rom, "old");
        var original = PokemonSpriteService.SourceKey(rom, null);
        Assert.Equal(original, PokemonSpriteService.SourceKey(rom, null));
        File.WriteAllText(rom, "replacement");
        var replaced = PokemonSpriteService.SourceKey(rom, null);
        Assert.NotEqual(original, replaced);
        Assert.NotEqual(replaced, PokemonSpriteService.SourceKey(rom, Path.Combine(_root, "expansion")));
    }

    private static void WriteHeaderOnlyRom(string folder)
    {
        Directory.CreateDirectory(folder);
        var bytes = new byte[0x4200];
        Encoding.ASCII.GetBytes("NCSD").CopyTo(bytes, 0x100);
        Encoding.ASCII.GetBytes("NCCH").CopyTo(bytes, 0x4100);
        Encoding.ASCII.GetBytes("CTR-P-A2BA").CopyTo(bytes, 0x4150);
        bytes[0x418F] = 4;
        File.WriteAllBytes(Path.Combine(folder, "header.3ds"), bytes);
    }

    private sealed class SpriteLog : ILogger<PokemonSpriteService>
    {
        public List<Exception> Failures { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Failures.Add(exception);
        }
    }
}
