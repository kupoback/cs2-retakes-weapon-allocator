using Dapper;

namespace RetakesAllocator.Modules;

/// <summary>
/// Engine-agnostic data access for the `weapons` table. All queries are async and
/// parameterized; the concrete engine comes from the injected <see cref="IDatabaseProvider"/>.
/// </summary>
public class WeaponStore
{
    private readonly IDatabaseProvider _provider;

    static WeaponStore()
    {
        // Map snake_case columns (t_primary) to PascalCase properties (TPrimary).
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public WeaponStore(IDatabaseProvider provider)
    {
        _provider = provider;
    }

    /// <summary>Creates the schema if it does not already exist.</summary>
    public async Task InitializeAsync()
    {
        await using var conn = _provider.CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(_provider.CreateTableSql);
    }

    /// <summary>Returns the stored preferences for a SteamID, or null if none exist.</summary>
    public async Task<WeaponPreference?> GetUserAsync(string auth)
    {
        await using var conn = _provider.CreateConnection();
        await conn.OpenAsync();
        return await conn.QuerySingleOrDefaultAsync<WeaponPreference>(
            """
            SELECT auth, name, t_primary, ct_primary, t_secondary, ct_secondary, give_awp
            FROM weapons
            WHERE auth = @auth
            """,
            new { auth });
    }

    /// <summary>Inserts a new user row with default (zeroed) preferences.</summary>
    public async Task CreateUserAsync(string auth, string name)
    {
        await using var conn = _provider.CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO weapons (auth, name)
            VALUES (@auth, @name)
            """,
            new { auth, name });
    }

    /// <summary>
    /// Copies every player from <paramref name="source"/> that this store does not have yet. Used
    /// when a server moves from SQLite to MySQL. A player already here is left alone, so their
    /// newer choices win and running it again copies nothing. Returns how many were copied.
    /// </summary>
    public async Task<int> ImportFromAsync(WeaponStore source)
    {
        await using var conn = _provider.CreateConnection();
        await conn.OpenAsync();

        var existing = (await conn.QueryAsync<string>("SELECT auth FROM weapons")).ToHashSet();

        List<WeaponPreference> rows;

        await using (var sourceConn = source._provider.CreateConnection())
        {
            await sourceConn.OpenAsync();
            rows = (await sourceConn.QueryAsync<WeaponPreference>(
                "SELECT auth, name, t_primary, ct_primary, t_secondary, ct_secondary, give_awp FROM weapons"))
                .Where(row => !existing.Contains(row.Auth))
                .ToList();
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        // One transaction, so a failure part way copies nothing and the next start tries again.
        await using var transaction = await conn.BeginTransactionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO weapons (auth, name, t_primary, ct_primary, t_secondary, ct_secondary, give_awp)
            VALUES (@Auth, @Name, @TPrimary, @CtPrimary, @TSecondary, @CtSecondary, @GiveAwp)
            """,
            rows, transaction);
        await transaction.CommitAsync();

        return rows.Count;
    }

    /// <summary>Persists the four weapon preference columns plus give_awp for an existing user.</summary>
    public async Task SaveUserAsync(WeaponPreference pref)
    {
        await using var conn = _provider.CreateConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(
            """
            UPDATE weapons
            SET t_primary = @TPrimary,
                ct_primary = @CtPrimary,
                t_secondary = @TSecondary,
                ct_secondary = @CtSecondary,
                give_awp = @GiveAwp
            WHERE auth = @Auth
            """,
            pref);
    }
}
