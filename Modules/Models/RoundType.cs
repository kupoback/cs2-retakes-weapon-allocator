namespace RetakesAllocator.Modules.Models;

/// <summary>What kind of loadout the current round hands out. Decided once per round at prestart.</summary>
public enum RoundType
{
    Pistol,
    HalfBuy,
    FullBuy,

    /// <summary>A passed vote is running; its own flags decide the loadout.</summary>
    Vote
}
