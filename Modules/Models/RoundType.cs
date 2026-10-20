namespace RetakesAllocator.Modules.Models;

/// <summary>What kind of loadout the current round hands out. Decided once per round at prestart.</summary>
public enum RoundType
{
    Pistol,
    HalfBuy,
    FullBuy,

    /// <summary>One of the RoundTypes.SpecialRounds entries, e.g. deagle only; see Core.CurrentSpecialRound.</summary>
    Special,

    /// <summary>A passed vote is running; its own flags decide the loadout.</summary>
    Vote
}
