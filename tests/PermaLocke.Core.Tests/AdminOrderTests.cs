using System.Text.Json;
using PermaLocke.Core.Domain;

namespace PermaLocke.Core.Tests;

/// <summary>An order from Admin survives the trip through the server as the player's application reads it (2026-09-26).</summary>
public sealed class AdminOrderTests
{
    [Fact]
    public void An_order_round_trips_with_its_arguments_and_is_never_empty()
    {
        var sent = new AdminGift
        {
            Schema = AdminGift.OrderSchema,
            Id = Guid.NewGuid(),
            From = "Organizador",
            To = Guid.NewGuid().ToString(),
            Reason = "muerte mal detectada",
            Order = new AdminOrder(AdminOrderKinds.Revive, new Dictionary<string, string> { ["pokemon"] = "abc" }, "Revivir a Plumas")
        };

        // Como escribe GiftDesk y como lee GiftInbox.
        var json = JsonSerializer.Serialize(sent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var read = JsonSerializer.Deserialize<AdminGift>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true })!;

        Assert.Equal(AdminOrderKinds.Revive, read.Order!.Kind);
        Assert.Equal("abc", read.Order.Arg("pokemon"));
        Assert.Equal(string.Empty, read.Order.Arg("nada"));
        Assert.False(read.IsEmpty);
        Assert.Contains("Revivir a Plumas", read.Say());
        Assert.True(read.Schema <= AdminGift.CurrentSchema);
    }
}
