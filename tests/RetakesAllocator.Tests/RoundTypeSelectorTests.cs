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

    private static SpecialRoundConfig Special(string name, int chance) => new() { Name = name, Chance = chance };

    [Fact]
    public void SpecialRound_ReturnsTheRolledEntry()
    {
        var deagle = Special("deagle only", 5);
        var c = new RoundTypesConfig { Enabled = true, PistolChance = 0, HalfBuyChance = 0, FullBuyChance = 0, SpecialRounds = [deagle] };

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(RoundType.Special, RoundTypeSelector.Roll(c, new Random(i), out var special));
            Assert.Same(deagle, special);
        }
    }

    [Fact]
    public void SpecialRound_NotRolledWhenDisabledOrZeroChance()
    {
        var disabled = new RoundTypesConfig { Enabled = false, SpecialRounds = [Special("deagle only", 100)] };
        Assert.Equal(RoundType.FullBuy, RoundTypeSelector.Roll(disabled, new Random(1), out var none));
        Assert.Null(none);

        var zero = new RoundTypesConfig { Enabled = true, PistolChance = 0, HalfBuyChance = 0, FullBuyChance = 1, SpecialRounds = [Special("deagle only", 0), Special("scouts only", -3)] };

        for (var i = 0; i < 50; i++)
        {
            Assert.Equal(RoundType.FullBuy, RoundTypeSelector.Roll(zero, new Random(i), out var special));
            Assert.Null(special);
        }
    }

    [Fact]
    public void SpecialRound_NeverBeatsOpeningPistolRoundsOrVote()
    {
        var config = Config(true, 0, 0, 0);
        config.RoundTypes.SpecialRounds = [Special("deagle only", 100)];

        Assert.Equal(RoundType.Pistol, RoundTypeSelector.Select(0, voteActive: false, config, new Random(1), out var a));
        Assert.Null(a);
        Assert.Equal(RoundType.Vote, RoundTypeSelector.Select(5, voteActive: true, config, new Random(1), out var b));
        Assert.Null(b);
        Assert.Equal(RoundType.Special, RoundTypeSelector.Select(5, voteActive: false, config, new Random(1), out var c));
        Assert.NotNull(c);
    }

    [Fact]
    public void SpecialRound_WeightsRoughlyRespected()
    {
        var deagle = Special("deagle only", 10);
        var scouts = Special("scouts only", 10);
        var c = new RoundTypesConfig { Enabled = true, PistolChance = 20, HalfBuyChance = 10, FullBuyChance = 50, SpecialRounds = [deagle, scouts] };
        var rng = new Random(123);
        int deagles = 0, scoutRounds = 0, fullBuys = 0;

        for (var i = 0; i < 20000; i++)
        {
            var type = RoundTypeSelector.Roll(c, rng, out var special);

            if (special == deagle) deagles++;
            else if (special == scouts) scoutRounds++;
            else if (type == RoundType.FullBuy) fullBuys++;
        }

        Assert.InRange(deagles / 20000.0, 0.08, 0.12);
        Assert.InRange(scoutRounds / 20000.0, 0.08, 0.12);
        Assert.InRange(fullBuys / 20000.0, 0.48, 0.52);
    }
}
