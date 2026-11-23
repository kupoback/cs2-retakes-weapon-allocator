using Xunit;
using RetakesAllocator.Modules;

namespace RetakesAllocator.Tests;

/// <summary>
/// The SQLite-to-MySQL import. The SQL is engine-agnostic, so two SQLite stores stand in for both.
/// </summary>
public class WeaponStoreImportTests
{
    private static async Task AddUser(WeaponStore store, string auth, string name, int tPrimary, int giveAwp)
    {
        await store.CreateUserAsync(auth, name);
        await store.SaveUserAsync(new WeaponPreference
        {
            Auth = auth,
            TPrimary = tPrimary,
            CtPrimary = 2,
            TSecondary = 1,
            CtSecondary = 2,
            GiveAwp = giveAwp,
        });
    }

    [Fact]
    public async Task CopiesEveryPlayerWithTheirChoices()
    {
        using var source = new TempDb();
        using var target = new TempDb();
        await AddUser(source.Store, "STEAM_1:0:1", "Alice", tPrimary: 1, giveAwp: 2);
        await AddUser(source.Store, "STEAM_1:0:2", "Bob", tPrimary: 0, giveAwp: 1);

        var imported = await target.Store.ImportFromAsync(source.Store);

        Assert.Equal(2, imported);

        var alice = await target.Store.GetUserAsync("STEAM_1:0:1");
        Assert.NotNull(alice);
        Assert.Equal("Alice", alice!.Name);
        Assert.Equal(1, alice.TPrimary);
        Assert.Equal(2, alice.CtPrimary);
        Assert.Equal(1, alice.TSecondary);
        Assert.Equal(2, alice.CtSecondary);
        Assert.Equal(2, alice.GiveAwp);

        Assert.NotNull(await target.Store.GetUserAsync("STEAM_1:0:2"));
    }

    [Fact]
    public async Task RunningAgain_CopiesNothing()
    {
        using var source = new TempDb();
        using var target = new TempDb();
        await AddUser(source.Store, "STEAM_1:0:1", "Alice", tPrimary: 1, giveAwp: 2);

        await target.Store.ImportFromAsync(source.Store);

        Assert.Equal(0, await target.Store.ImportFromAsync(source.Store));
    }

    [Fact]
    public async Task PlayerAlreadyInTarget_KeepsTheirNewerChoices()
    {
        using var source = new TempDb();
        using var target = new TempDb();
        await AddUser(source.Store, "STEAM_1:0:1", "Alice", tPrimary: 1, giveAwp: 2);
        await AddUser(source.Store, "STEAM_1:0:2", "Bob", tPrimary: 1, giveAwp: 2);

        // Alice joined the MySQL server before the import ran and picked something else.
        await AddUser(target.Store, "STEAM_1:0:1", "Alice", tPrimary: 0, giveAwp: 0);

        var imported = await target.Store.ImportFromAsync(source.Store);

        Assert.Equal(1, imported);
        Assert.Equal(0, (await target.Store.GetUserAsync("STEAM_1:0:1"))!.TPrimary);
        Assert.NotNull(await target.Store.GetUserAsync("STEAM_1:0:2"));
    }

    [Fact]
    public async Task EmptySource_CopiesNothing()
    {
        using var source = new TempDb();
        using var target = new TempDb();

        Assert.Equal(0, await target.Store.ImportFromAsync(source.Store));
    }
}
