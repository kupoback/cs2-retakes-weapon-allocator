using RetakesAllocator.Modules.Config;
using RetakesAllocator.Modules.Weapons;
using Xunit;

namespace RetakesAllocator.Tests;

public class AwpSelectorTests
{
    private static readonly AwpConfig MinThree = new() { MinTeamSize = 3 };

    [Fact]
    public void NoCandidates_NobodyGetsAwp()
    {
        Assert.Null(AwpSelector.Pick(new List<string>(), 5, MinThree, new Random(1)));
    }

    [Fact]
    public void TeamBelowMinimum_NobodyGetsAwp()
    {
        Assert.Null(AwpSelector.Pick(new List<string> { "a", "b" }, 2, MinThree, new Random(1)));
    }

    [Fact]
    public void TeamAtMinimum_ExactlyOneCandidateGetsAwp()
    {
        var candidates = new List<string> { "a", "b", "c" };
        var pick = AwpSelector.Pick(candidates, 3, MinThree, new Random(1));
        Assert.Contains(pick, candidates);
    }

    [Fact]
    public void NoMinimum_SmallTeamCanGetAwp()
    {
        Assert.Equal("a", AwpSelector.Pick(new List<string> { "a" }, 1, new AwpConfig(), new Random(1)));
    }

    [Fact]
    public void EveryCandidateCanWin()
    {
        var candidates = new List<string> { "a", "b", "c" };
        var rng = new Random(7);
        var winners = Enumerable.Range(0, 200).Select(_ => AwpSelector.Pick(candidates, 3, MinThree, rng)).ToHashSet();
        Assert.Equal(3, winners.Count);
    }
}
