using PermaLocke.Core.Domain;
using PermaLocke.Data;

namespace PermaLocke.Core.Tests;

/// <summary>The postbox of the shared folder (§129): one file per gift, read back as it was written.</summary>
public sealed class GiftStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"permalocke-regalos-{Guid.NewGuid():N}");
    private readonly GiftStore _store = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void A_gift_survives_the_trip_through_the_folder()
    {
        var gift = new AdminGift
        {
            Id = Guid.NewGuid(),
            From = "Grenin",
            To = AdminGift.Everybody,
            Reason = "por ganar el combate del sábado",
            CreatedAt = new DateTimeOffset(2026, 9, 17, 20, 15, 3, TimeSpan.FromHours(2)),
            Points = 100,
            Items = [new GiftItem(25, "Hiperpoción", 2)],
            Rolls = new Dictionary<string, int> { ["decente"] = 2 },
            WonderTrades = 1
        };

        _store.Write(_root, gift);

        var read = Assert.Single(_store.ReadAll(_root));

        Assert.Equal(gift.Id, read.Id);
        Assert.Equal(gift.CreatedAt, read.CreatedAt);
        Assert.Equal(gift.Say(), read.Say());
        Assert.Equal(2, read.Rolls["decente"]);
    }

    [Fact]
    public void Several_gifts_come_back_newest_first()
    {
        _store.Write(_root, Gift("el primero", DateTimeOffset.UnixEpoch));
        _store.Write(_root, Gift("el segundo", DateTimeOffset.UnixEpoch.AddDays(1)));

        Assert.Equal(["el segundo", "el primero"], _store.ReadAll(_root).Select(gift => gift.Reason));
    }

    /// <summary>One file half-synchronised is one gift that does not load, not all of them.</summary>
    [Fact]
    public void A_broken_file_does_not_hide_the_rest()
    {
        _store.Write(_root, Gift("el bueno", DateTimeOffset.UnixEpoch));
        File.WriteAllText(Path.Combine(GiftStore.Folder(_root), "roto.json"), "{ esto no es json");

        Assert.Equal("el bueno", Assert.Single(_store.ReadAll(_root)).Reason);
    }

    [Fact]
    public void A_gift_can_be_withdrawn()
    {
        var gift = Gift("me he equivocado", DateTimeOffset.UnixEpoch);
        _store.Write(_root, gift);

        Assert.True(_store.Remove(_root, gift.Id));
        Assert.Empty(_store.ReadAll(_root));
        Assert.False(_store.Remove(_root, gift.Id));
    }

    [Fact]
    public void No_folder_is_no_gifts_rather_than_a_failure() => Assert.Empty(_store.ReadAll(_root));

    private static AdminGift Gift(string reason, DateTimeOffset at) => new()
    {
        Id = Guid.NewGuid(),
        From = "Grenin",
        To = AdminGift.Everybody,
        Reason = reason,
        CreatedAt = at,
        Points = 10
    };
}
