using RetakesAllocator.Modules.Config;

namespace RetakesAllocator.Modules.Weapons;

/// <summary>
/// Picks one team's AWP player. Pure logic with no CounterStrikeSharp runtime calls, so it can be unit tested.
/// </summary>
public static class AwpSelector
{
    /// <summary>
    /// Returns the one player on a team who gets the AWP this round, or default when nobody does:
    /// nobody on the team won their own Sometimes/Always roll, or the team is smaller than
    /// <see cref="AwpConfig.MinTeamSize"/>.
    /// </summary>
    /// <param name="candidates">Players on the team whose own AWP roll came up.</param>
    /// <param name="teamSize">Every player on the team, whether they want the AWP or not.</param>
    public static T? Pick<T>(IReadOnlyList<T> candidates, int teamSize, AwpConfig config, Random rng)
    {
        if (candidates.Count == 0 || teamSize < config.MinTeamSize)
        {
            return default;
        }

        return candidates[rng.Next(candidates.Count)];
    }
}
