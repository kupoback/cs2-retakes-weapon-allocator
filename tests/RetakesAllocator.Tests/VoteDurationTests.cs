using RetakesAllocator.Modules;
using RetakesAllocator.Modules.Models;
using RetakesAllocator.Modules.Votes;
using Xunit;
using VotesClass = RetakesAllocator.Modules.Votes.Votes;

namespace RetakesAllocator.Tests;

[Collection("StaticState")]
public class VoteDurationTests : IDisposable
{
    private readonly AsyncVoteManager _vote = new(new Vote("vp", "pistol only", new(), new(), false, true));

    public void Dispose()
    {
        Core.CurrentVote = null!;
        VotesClass.RoundVote = null!;
        VotesClass.VoteRoundsLeft = 0;
    }

    /// <summary>Starts a round the way OnRoundPreStart does when the vote is running.</summary>
    private void PlayVoteRound() => VotesClass.RoundVote = Core.CurrentVote;

    [Fact]
    public void OneRoundVote_EndsAfterOneRound()
    {
        Core.CurrentVote = _vote;
        VotesClass.VoteRoundsLeft = 1;

        PlayVoteRound();
        Assert.Same(_vote, VotesClass.OnVoteRoundPlayed());
        Assert.Null(Core.CurrentVote);
    }

    [Fact]
    public void MultiRoundVote_LastsItsRounds()
    {
        Core.CurrentVote = _vote;
        VotesClass.VoteRoundsLeft = 3;

        PlayVoteRound();
        Assert.Null(VotesClass.OnVoteRoundPlayed());
        PlayVoteRound();
        Assert.Null(VotesClass.OnVoteRoundPlayed());
        Assert.Same(_vote, Core.CurrentVote);
        PlayVoteRound();
        Assert.Same(_vote, VotesClass.OnVoteRoundPlayed());
        Assert.Null(Core.CurrentVote);
    }

    [Fact]
    public void UnlimitedVote_NeverEnds()
    {
        Core.CurrentVote = _vote;
        VotesClass.VoteRoundsLeft = 0;

        for (var i = 0; i < 10; i++)
        {
            PlayVoteRound();
            Assert.Null(VotesClass.OnVoteRoundPlayed());
        }

        Assert.Same(_vote, Core.CurrentVote);
    }

    [Fact]
    public void RoundTheVotePassedIn_DoesNotCount()
    {
        // The vote passes mid-round: this round started without it.
        VotesClass.RoundVote = null!;
        Core.CurrentVote = _vote;
        VotesClass.VoteRoundsLeft = 1;

        Assert.Null(VotesClass.OnVoteRoundPlayed());
        Assert.Same(_vote, Core.CurrentVote);

        PlayVoteRound();
        Assert.Same(_vote, VotesClass.OnVoteRoundPlayed());
    }

    [Fact]
    public void ResetVotes_EndsRunningVote()
    {
        Core.CurrentVote = _vote;
        VotesClass.VoteRoundsLeft = 5;

        Assert.True(VotesClass.ResetVotes());
        Assert.Null(Core.CurrentVote);
        Assert.Equal(0, VotesClass.VoteRoundsLeft);
        Assert.False(VotesClass.ResetVotes());
    }
}
