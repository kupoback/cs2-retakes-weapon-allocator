using CounterStrikeSharp.API.Modules.Entities.Constants;
using RetakesAllocator.Modules.Models;
using RetakesAllocator.Modules.Weapons;
using Xunit;

namespace RetakesAllocator.Tests;

public class NadeDistributorTests
{
    private static readonly GrenadeLimits Cs2 = new();

    private static Nades Pool(int flashbangs = 0, int smokes = 0, int molotovs = 0, int heGrenades = 0) =>
        new() { Flashbangs = flashbangs, Smokes = smokes, Molotovs = molotovs, HeGrenades = heGrenades };

    /// <summary>Hands out the pool one player at a time, as the spawn timers do.</summary>
    private static List<List<CsItem>> Distribute(Nades pool, int players, Random rng, GrenadeLimits? limits = null)
    {
        var hands = new List<List<CsItem>>();

        for (var left = players; left > 0; left--)
        {
            hands.Add(NadeDistributor.TakeShare(pool, left, limits ?? Cs2, rng));
        }

        return hands;
    }

    private static void AssertLegal(List<CsItem> hand, GrenadeLimits limits)
    {
        Assert.True(hand.Count <= limits.Total, $"{hand.Count} grenades is over the total limit");

        foreach (var group in hand.GroupBy(item => item))
        {
            var cap = group.Key == CsItem.Flashbang ? limits.Flashbangs : limits.PerType;
            Assert.True(group.Count() <= cap, $"{group.Count()} x {group.Key} is over the limit");
        }
    }

    [Fact]
    public void SmokeAliasesSmokeGrenade()
    {
        // Nades.RemoveNade matches CsItem.Smoke; the distributor hands out CsItem.SmokeGrenade.
        Assert.Equal(CsItem.Smoke, CsItem.SmokeGrenade);
    }

    [Fact]
    public void ThreePlayers_SevenGrenades_AllHandedOut()
    {
        // The example from the request: 1 smoke, 2 molotovs, 2 HEs and 2 flashes for 3 CTs.
        for (var seed = 0; seed < 500; seed++)
        {
            var pool = Pool(flashbangs: 2, smokes: 1, molotovs: 2, heGrenades: 2);
            var hands = Distribute(pool, 3, new Random(seed));

            Assert.Equal(7, hands.Sum(hand => hand.Count));
            Assert.False(pool.HasNades());
            hands.ForEach(hand => AssertLegal(hand, Cs2));
            Assert.Equal(2, hands.SelectMany(hand => hand).Count(item => item == CsItem.Molotov));
        }
    }

    [Fact]
    public void EveryoneGetsSomething_WhenThereIsEnough()
    {
        for (var seed = 0; seed < 500; seed++)
        {
            var hands = Distribute(Pool(flashbangs: 2, smokes: 1, molotovs: 2, heGrenades: 2), 3, new Random(seed));

            Assert.All(hands, hand => Assert.InRange(hand.Count, 2, 3));
        }
    }

    [Fact]
    public void MorePlayersThanGrenades_NobodyGetsTwo()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var hands = Distribute(Pool(flashbangs: 1, smokes: 1, heGrenades: 1), 5, new Random(seed));

            Assert.Equal(3, hands.Sum(hand => hand.Count));
            Assert.All(hands, hand => Assert.InRange(hand.Count, 0, 1));
        }
    }

    [Fact]
    public void PoolLargerThanTeamCanCarry_ExtrasAreLeftBehind()
    {
        // One player can hold 2 flashes, 1 smoke and 1 HE at most.
        var pool = Pool(flashbangs: 4, smokes: 3, heGrenades: 3);
        var hand = NadeDistributor.TakeShare(pool, 1, Cs2, new Random(1));

        AssertLegal(hand, Cs2);
        Assert.Equal(4, hand.Count);
        Assert.Equal(6, pool.Flashbangs + pool.Smokes + pool.HeGrenades);
    }

    [Fact]
    public void EarlyPlayersLeaveWhatOthersCannotTake()
    {
        // Two players and two smokes: the first must not leave both smokes for the second.
        for (var seed = 0; seed < 200; seed++)
        {
            var hands = Distribute(Pool(smokes: 2, flashbangs: 2), 2, new Random(seed));

            Assert.Equal(4, hands.Sum(hand => hand.Count));
            Assert.All(hands, hand => AssertLegal(hand, Cs2));
        }
    }

    [Fact]
    public void ServerLimitsAreRespected()
    {
        var limits = new GrenadeLimits(Total: 2, Flashbangs: 1, PerType: 1);

        for (var seed = 0; seed < 200; seed++)
        {
            var hands = Distribute(Pool(flashbangs: 3, smokes: 1, molotovs: 1, heGrenades: 1), 3, new Random(seed), limits);

            Assert.Equal(6, hands.Sum(hand => hand.Count));
            Assert.All(hands, hand => AssertLegal(hand, limits));
        }
    }

    [Fact]
    public void AnyKitTheTeamCanCarry_IsHandedOutInFull()
    {
        var rng = new Random(42);

        for (var run = 0; run < 5000; run++)
        {
            var players = rng.Next(1, 6);
            var pool = Pool(rng.Next(0, 5), rng.Next(0, 4), rng.Next(0, 4), rng.Next(0, 4));
            var total = pool.Flashbangs + pool.Smokes + pool.Molotovs + pool.HeGrenades;

            // CS2 limits per player: 4 in all, 2 flashes, 1 of everything else.
            var carryable = pool.Smokes <= players && pool.Molotovs <= players && pool.HeGrenades <= players
                            && pool.Flashbangs <= 2 * players && total <= 4 * players;

            var hands = Distribute(pool, players, rng);

            hands.ForEach(hand => AssertLegal(hand, Cs2));

            if (carryable)
            {
                Assert.Equal(total, hands.Sum(hand => hand.Count));
            }
        }
    }

    [Fact]
    public void EmptyPool_GivesNothing()
    {
        Assert.Empty(NadeDistributor.TakeShare(Pool(), 3, Cs2, new Random(1)));
    }
}
