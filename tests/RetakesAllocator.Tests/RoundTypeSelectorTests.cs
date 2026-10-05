using RetakesAllocator.Modules.Config;
using RetakesAllocator.Modules.Models;
using RetakesAllocator.Modules.Weapons;
using Xunit;

namespace RetakesAllocator.Tests;

public class RoundTypeSelectorTests
{
    private static RetakesAllocatorConfig Config(bool enabled, int pistol, int half, int full) => new()
    {
        PistolRound = new PistolRoundConfig { RoundAmount = 2 },
        RoundTypes = new RoundTypesConfig
        {
            Enabled = enabled,
            PistolChance = pistol,
            HalfBuyChance = half,
            FullBuyChance = full
        }
    };

    [Fact]
    public void OpeningPistolRounds_AlwaysWin()
    {
        var config = Config(true, 0, 0, 100);
        Assert.Equal(RoundType.Pistol, RoundTypeSelector.Select(0, voteActive: true, config, new Random(1)));
        Assert.Equal(RoundType.Pistol, RoundTypeSelector.Select(1, voteActive: false, config, new Random(1)));
    }

    [Fact]
    public void Vote_BeatsRandomRoll()
    {
        var config = Config(true, 100, 0, 0);
        Assert.Equal(RoundType.Vote, RoundTypeSelector.Select(5, voteActive: true, config, new Random(1)));
    }

    [Fact]
    public void Disabled_IsAlwaysFullBuy()
    {
        var config = Config(false, 100, 100, 0);
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(RoundType.FullBuy, RoundTypeSelector.Select(5, false, config, new Random(i)));
        }
    }

    [Fact]
    public void SingleNonZeroWeight_AlwaysPicksIt()
    {
        var rng = new Random(42);
        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(RoundType.HalfBuy, RoundTypeSelector.Roll(new RoundTypesConfig { Enabled = true, PistolChance = 0, HalfBuyChance = 7, FullBuyChance = 0 }, rng));
        }
    }

    [Fact]
    public void AllZeroOrNegative_FallsBackToFullBuy()
    {
        var c = new RoundTypesConfig { Enabled = true, PistolChance = -5, HalfBuyChance = 0, FullBuyChance = 0 };
        Assert.Equal(RoundType.FullBuy, RoundTypeSelector.Roll(c, new Random(1)));
    }

    [Fact]
    public void Weights_RoughlyRespected()
    {
        var c = new RoundTypesConfig { Enabled = true, PistolChance = 15, HalfBuyChance = 25, FullBuyChance = 60 };
        var rng = new Random(123);
        var counts = new Dictionary<RoundType, int> { [RoundType.Pistol] = 0, [RoundType.HalfBuy] = 0, [RoundType.FullBuy] = 0 };

        for (var i = 0; i < 20000; i++)
        {
            counts[RoundTypeSelector.Roll(c, rng)]++;
        }

        Assert.InRange(counts[RoundType.Pistol] / 20000.0, 0.13, 0.17);
        Assert.InRange(counts[RoundType.HalfBuy] / 20000.0, 0.23, 0.27);
        Assert.InRange(counts[RoundType.FullBuy] / 20000.0, 0.58, 0.62);
    }
}
