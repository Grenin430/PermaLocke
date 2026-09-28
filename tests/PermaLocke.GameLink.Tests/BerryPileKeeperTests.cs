using PermaLocke.GameLink.Field;

namespace PermaLocke.GameLink.Tests;

public class BerryPileKeeperTests
{
    [Fact]
    public void Live_piles_match_the_save_by_their_rolls_even_with_a_few_picked_or_regrown()
    {
        var save = new byte[BerryPileKeeper.Piles * BerryPileKeeper.EntrySize];
        new Random(7).NextBytes(save);
        var live = save.ToArray();

        for (var pile = 0; pile < 10; pile++)
        {
            live[pile * 4] = 0;
            live[(pile * 4) + 1] ^= 0xFF;
        }

        Assert.True(BerryPileKeeper.MatchesSave(live, save));

        for (var pile = 10; pile < 30; pile++) live[(pile * 4) + 2] ^= 0xFF;
        Assert.False(BerryPileKeeper.MatchesSave(live, save));
    }
}
