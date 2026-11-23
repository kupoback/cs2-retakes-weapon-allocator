using CounterStrikeSharp.API.Modules.Entities.Constants;
using RetakesAllocator.Modules.Models;

namespace RetakesAllocator.Modules.Weapons;

/// <summary>
/// How many grenades one player may carry, as the game enforces it (<c>ammo_grenade_limit_total</c>,
/// <c>ammo_grenade_limit_flashbang</c> and <c>ammo_grenade_limit_default</c>). The defaults are CS2's.
/// </summary>
public record GrenadeLimits(int Total = 4, int Flashbangs = 2, int PerType = 1);

/// <summary>
/// Splits a team's whole grenade pool between its players. Pure logic with no CounterStrikeSharp
/// runtime calls, so it can be unit tested.
/// </summary>
/// <remarks>
/// Players are given grenades one at a time as their spawn timers fire, so this hands out one
/// player's share at a time: whatever this player has to take so the players still to come can
/// carry the rest, topped up at random to an even split of what is left. Molotovs come back as
/// <see cref="CsItem.Molotov"/> for both teams; the caller swaps in the incendiary for CTs.
/// </remarks>
public static class NadeDistributor
{
    private static readonly CsItem[] Kinds =
        [CsItem.Flashbang, CsItem.SmokeGrenade, CsItem.HEGrenade, CsItem.Molotov];

    /// <summary>
    /// Takes one player's grenades out of <paramref name="pool"/> and returns them.
    /// </summary>
    /// <param name="playersLeft">Players on the team still to be given grenades this round, this one included.</param>
    public static List<CsItem> TakeShare(Nades pool, int playersLeft, GrenadeLimits limits, Random rng)
    {
        playersLeft = Math.Max(playersLeft, 1);

        var remaining = Expand(pool);
        Shuffle(remaining, rng);

        var hand = new List<CsItem>();
        var total = remaining.Count;

        // An even split, with the odd grenades going to random players.
        var target = total / playersLeft + (rng.Next(playersLeft) < total % playersLeft ? 1 : 0);

        // First whatever the others could not carry between them, so the random part below cannot
        // fill this player up with the wrong grenades. For the last player this is simply
        // everything they can hold.
        var others = playersLeft - 1;
        var excess = Excess(remaining, others, limits);

        while (excess > 0)
        {
            var taken = false;

            for (var i = 0; i < remaining.Count; i++)
            {
                if (!CanCarry(hand, remaining[i], limits))
                {
                    continue;
                }

                var item = remaining[i];
                remaining.RemoveAt(i);
                var newExcess = Excess(remaining, others, limits);

                if (newExcess < excess)
                {
                    hand.Add(item);
                    excess = newExcess;
                    taken = true;
                    break;
                }

                remaining.Insert(i, item);
            }

            if (!taken)
            {
                break;
            }
        }

        // Then top up at random to this player's even share. Taking a grenade never leaves the
        // others with something they cannot carry.
        for (var i = 0; i < remaining.Count && hand.Count < target; i++)
        {
            if (CanCarry(hand, remaining[i], limits))
            {
                hand.Add(remaining[i]);
                remaining.RemoveAt(i--);
            }
        }

        foreach (var item in hand)
        {
            pool.RemoveNade(item);
        }

        return hand;
    }

    /// <summary>Whether a player holding <paramref name="hand"/> can also pick up <paramref name="item"/>.</summary>
    public static bool CanCarry(IReadOnlyCollection<CsItem> hand, CsItem item, GrenadeLimits limits)
    {
        return hand.Count < limits.Total && hand.Count(held => held == item) < Cap(item, limits);
    }

    private static int Cap(CsItem kind, GrenadeLimits limits) =>
        kind == CsItem.Flashbang ? limits.Flashbangs : limits.PerType;

    /// <summary>
    /// How many grenades of <paramref name="items"/> could not be carried by <paramref name="players"/>
    /// players, however they were split. Checked over every combination of grenade kinds, because the
    /// total limit and the per-kind limits both apply.
    /// </summary>
    private static int Excess(IReadOnlyCollection<CsItem> items, int players, GrenadeLimits limits)
    {
        var excess = 0;

        for (var mask = 1; mask < 1 << Kinds.Length; mask++)
        {
            var count = 0;
            var cap = 0;

            for (var k = 0; k < Kinds.Length; k++)
            {
                if ((mask & (1 << k)) == 0)
                {
                    continue;
                }

                count += items.Count(item => item == Kinds[k]);
                cap += Cap(Kinds[k], limits);
            }

            excess = Math.Max(excess, count - players * Math.Min(cap, limits.Total));
        }

        return excess;
    }

    private static List<CsItem> Expand(Nades pool)
    {
        var items = new List<CsItem>();
        items.AddRange(Enumerable.Repeat(CsItem.Flashbang, Math.Max(pool.Flashbangs, 0)));
        items.AddRange(Enumerable.Repeat(CsItem.SmokeGrenade, Math.Max(pool.Smokes, 0)));
        items.AddRange(Enumerable.Repeat(CsItem.HEGrenade, Math.Max(pool.HeGrenades, 0)));
        items.AddRange(Enumerable.Repeat(CsItem.Molotov, Math.Max(pool.Molotovs, 0)));
        return items;
    }

    private static void Shuffle(List<CsItem> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
