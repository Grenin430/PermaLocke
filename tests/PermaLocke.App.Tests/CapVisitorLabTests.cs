using PermaLocke.App.Views;
using Xunit;

namespace PermaLocke.App.Tests;

public sealed class CapVisitorLabTests
{
    [Fact]
    public void A_batch_with_four_fliers_keeps_every_flier_in_the_sky_preview()
    {
        int[] batch = [779, 398, 373, 6, 381];
        var selected = Enumerable.Range(0, CapVisitors.Lanes.Length)
            .Where(i => CapVisitors.Lanes[i].Flies)
            .SelectMany(i => CapVisitors.ForcedLaneSpecies(batch, i)).ToArray();
        Assert.Equal(new[] { 6, 373, 381, 398 }, selected.Order().ToArray());
        Assert.DoesNotContain(779, selected);
    }

    [Fact]
    public void Previous_batches_keep_their_original_assignments_and_empty_habitats_stay_empty()
    {
        int[] batch = [318, 119, 22, 149, 380];
        var selected = Enumerable.Range(0, CapVisitors.Lanes.Length)
            .SelectMany(i => CapVisitors.ForcedLaneSpecies(batch, i)).ToArray();
        Assert.Equal(new[] { 318, 119, 318, 22, 149, 380 }, selected);
        Assert.Empty(CapVisitors.ForcedLaneSpecies([779], 3));
        Assert.Empty(CapVisitors.ForcedLaneSpecies([381], 0));
    }
}
