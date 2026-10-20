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
    public static RoundType Select(int roundsPlayed, bool voteActive, RetakesAllocatorConfig config, Random rng) =>
        Select(roundsPlayed, voteActive, config, rng, out _);

    /// <inheritdoc cref="Select(int, bool, RetakesAllocatorConfig, Random)"/>
    /// <param name="special">The special round that was rolled, when the result is <see cref="RoundType.Special"/>.</param>
    public static RoundType Select(int roundsPlayed, bool voteActive, RetakesAllocatorConfig config, Random rng,
        out SpecialRoundConfig? special)
    {
        special = null;

        if (roundsPlayed < config.PistolRound.RoundAmount)
        {
            return RoundType.Pistol;
        }

        if (voteActive)
        {
            return RoundType.Vote;
        }

        return Roll(config.RoundTypes, rng, out special);
    }

    /// <summary>
    /// Weighted roll. The chances, special rounds included, are weights, so they do not have to add
    /// up to 100: 1/1/2 means 25% pistol, 25% half buy, 50% full buy. Negative values count as 0,
    /// and if everything is 0 the round falls back to a full buy.
    /// </summary>
    public static RoundType Roll(RoundTypesConfig config, Random rng) => Roll(config, rng, out _);

    /// <inheritdoc cref="Roll(RoundTypesConfig, Random)"/>
    /// <param name="special">The special round that was rolled, when the result is <see cref="RoundType.Special"/>.</param>
    public static RoundType Roll(RoundTypesConfig config, Random rng, out SpecialRoundConfig? special)
    {
        special = null;

        if (!config.Enabled)
        {
            return RoundType.FullBuy;
        }

        var pistol = Math.Max(0, config.PistolChance);
        var halfBuy = Math.Max(0, config.HalfBuyChance);
        var fullBuy = Math.Max(0, config.FullBuyChance);
        var specials = (config.SpecialRounds ?? []).Where(s => s != null!).ToList();
        var total = pistol + halfBuy + fullBuy + specials.Sum(s => Math.Max(0, s.Chance));

        if (total <= 0)
        {
            return RoundType.FullBuy;
        }

        var roll = rng.Next(total);

        if (roll < pistol)
        {
            return RoundType.Pistol;
        }

        roll -= pistol;

        if (roll < halfBuy)
        {
            return RoundType.HalfBuy;
        }

        roll -= halfBuy;

        foreach (var s in specials)
        {
            var chance = Math.Max(0, s.Chance);

            if (roll < chance)
            {
                special = s;
                return RoundType.Special;
            }

            roll -= chance;
        }

        return RoundType.FullBuy;
    }
}
