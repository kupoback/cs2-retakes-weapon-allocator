using RetakesAllocator.Modules.Config;
using RetakesAllocator.Modules.Models;

namespace RetakesAllocator.Modules.Weapons;

/// <summary>
/// Picks the round type. Pure logic with no CounterStrikeSharp runtime calls, so it can be unit tested.
/// </summary>
public static class RoundTypeSelector
{
    /// <summary>
    /// Priority: the opening pistol rounds (PistolRound.RoundAmount) always come first, then a running
    /// vote, then the weighted roll from RoundTypes. With RoundTypes disabled every remaining round is
    /// a full buy, which is exactly how the plugin behaved before round types existed.
    /// </summary>
    public static RoundType Select(int roundsPlayed, bool voteActive, RetakesAllocatorConfig config, Random rng)
    {
        if (roundsPlayed < config.PistolRound.RoundAmount)
        {
            return RoundType.Pistol;
        }

        if (voteActive)
        {
            return RoundType.Vote;
        }

        return Roll(config.RoundTypes, rng);
    }

    /// <summary>
    /// Weighted roll. The three chances are weights, so they do not have to add up to 100:
    /// 1/1/2 means 25% pistol, 25% half buy, 50% full buy. Negative values count as 0, and if
    /// everything is 0 the round falls back to a full buy.
    /// </summary>
    public static RoundType Roll(RoundTypesConfig config, Random rng)
    {
        if (!config.Enabled)
        {
            return RoundType.FullBuy;
        }

        var pistol = Math.Max(0, config.PistolChance);
        var halfBuy = Math.Max(0, config.HalfBuyChance);
        var fullBuy = Math.Max(0, config.FullBuyChance);
        var total = pistol + halfBuy + fullBuy;

        if (total <= 0)
        {
            return RoundType.FullBuy;
        }

        var roll = rng.Next(total);

        if (roll < pistol)
        {
            return RoundType.Pistol;
        }

        return roll < pistol + halfBuy ? RoundType.HalfBuy : RoundType.FullBuy;
    }
}
