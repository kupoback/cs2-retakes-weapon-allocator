using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using RetakesAllocator.Modules.Config;
using RetakesAllocator.Modules.Models;
using RetakesAllocator.Modules.Votes;

using static RetakesAllocator.Modules.Weapons.Menu;
using static RetakesAllocator.Modules.Votes.Votes;

namespace RetakesAllocator.Modules.Weapons;

public enum GiveAwp
{
    Never,
    Sometimes,
    Always
}

public class Allocator(Player player)
{
    public static List<Weapon> PrimaryT = new()
    {
        new Weapon("weapon_ak47", "AK-47"),
        new Weapon("weapon_sg556", "SG 553")
    };

    public static List<Weapon> PrimaryCt = new()
    {
        new Weapon("weapon_m4a1", "M4A4"),
        new Weapon("weapon_m4a1_silencer", "M4A1-S"),
        new Weapon("weapon_aug", "AUG")
    };

    public static List<Weapon> PistolsT = new()
    {
        new Weapon("weapon_glock", "Glock-18"),
        new Weapon("weapon_p250", "P250"),
    };

    public static List<Weapon> PistolsCT = new()
    {
        new Weapon("weapon_usp_silencer", "USP-S"),
        new Weapon("weapon_p250", "P250"),
        new Weapon("weapon_hkp2000", "P2000")
    };

    private static Nades _ctNades = new();
    private static Nades _nades = new();

    /// <summary>
    /// Whether this round splits each team's whole grenade pool between its players instead of
    /// giving everyone one grenade. Rolled once per round for both teams, so neither side gets the
    /// bigger kit alone.
    /// </summary>
    private static bool _distributeAllNades;

    private static GrenadeLimits _grenadeLimits = new();

    /// <summary>Counts <see cref="ResetNades"/> calls, so a player can tell whether their grenades are from this round.</summary>
    private static int _nadeRound;

    private int _nadesGivenRound = -1;
    private List<CsItem> _nadesGiven = new();

    private CCSPlayerController CCsPlayerController => player.Controller;

    public int PrimaryWeaponT = 0;
    public int PrimaryWeaponCt = 0;
    public int SecondaryWeaponT = 0;
    public int SecondaryWeaponCt = 0;

    public GiveAwp GiveAwp = GiveAwp.Never;
    public bool ShouldGiveAwp = false;

    /// <summary>
    /// The team the player was on when the AWP was rolled. If the retakes plugin moves them to the
    /// other side after that, they lose the AWP for the round, otherwise the team they join could
    /// end up with two.
    /// </summary>
    public CsTeam AwpTeam = CsTeam.None;

    private bool GetsAwp => ShouldGiveAwp && CCsPlayerController.Team == AwpTeam;

    public static void ResetNades()
    {
        _ctNades = new Nades(Core.NadesConfig.CtNades);
        _nades = new Nades(Core.NadesConfig.TNades);

        _nadeRound++;
        _distributeAllNades = Random.Shared.NextDouble() * 100 < Core.NadesConfig.DistributeAllChance;
        _grenadeLimits = new GrenadeLimits(
            ReadIntConVar("ammo_grenade_limit_total", 4),
            ReadIntConVar("ammo_grenade_limit_flashbang", 2),
            ReadIntConVar("ammo_grenade_limit_default", 1));
    }

    private static int ReadIntConVar(string name, int fallback)
    {
        try
        {
            var conVar = ConVar.Find(name);
            return conVar == null ? fallback : conVar.GetPrimitiveValue<int>();
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public bool SetupGiveAwp()
    {
        var giveAwp = GiveAwp switch
        {
            GiveAwp.Always => true,
            GiveAwp.Never => false,
            _ => new Random().Next(0, 2) == 1
        };

        return giveAwp;
    }

    public void AllocateNades()
    {
        if (!player.Controller.IsValid)
        {
            return;
        }

        if (!CCsPlayerController.PawnIsAlive)
        {
            return;
        }

        // No nades in warmup. Nobody is playing for anything there, and the pools they would draw
        // from are refilled at round prestart - which fires once for the whole of warmup, so
        // warmup spawns would drain what the first real round is entitled to.
        if (Core.GetGameRules() is { WarmupPeriod: true })
        {
            return;
        }

        if (CCsPlayerController.Team is < CsTeam.Terrorist or > CsTeam.CounterTerrorist)
        {
            return;
        }

        // Spawn can fire twice for one player. Hand back what they were given rather than drawing
        // again, which would take grenades meant for their teammates.
        if (_nadesGivenRound == _nadeRound)
        {
            foreach (var given in _nadesGiven)
            {
                CCsPlayerController.GiveNamedItem(given);
            }

            return;
        }

        _nadesGivenRound = _nadeRound;
        _nadesGiven = new List<CsItem>();

        switch (CCsPlayerController.Team)
        {
            case CsTeam.Terrorist when !_nades.HasNades():
            case CsTeam.CounterTerrorist when !_ctNades.HasNades():
                return;
        }

        var nades = CCsPlayerController.Team == CsTeam.Terrorist ? _nades : _ctNades;

        if (_distributeAllNades)
        {
            AllocateNadesShare(nades);
            return;
        }

        CsItem grenade;
        do{
            grenade = SelectGrenade();
        }
        while(!CheckIfNadeAvailable(nades, grenade));

        nades.RemoveNade(grenade);

        _nadesGiven.Add(grenade);
        CCsPlayerController.GiveNamedItem(grenade);
    }

    /// <summary>
    /// Gives this player their share of the team's whole grenade pool, leaving enough for the
    /// teammates who have not been given theirs yet.
    /// </summary>
    private void AllocateNadesShare(Nades nades)
    {
        var team = CCsPlayerController.Team;

        var playersLeft = 1 + Core.Players.Count(other =>
            other != player
            && other.WeaponsAllocator._nadesGivenRound != _nadeRound
            && other.Controller is { IsValid: true, PawnIsAlive: true }
            && other.Controller.Team == team);

        foreach (var grenade in NadeDistributor.TakeShare(nades, playersLeft, _grenadeLimits, Random.Shared))
        {
            var item = grenade == CsItem.Molotov && team == CsTeam.CounterTerrorist ? CsItem.Incendiary : grenade;

            _nadesGiven.Add(item);
            CCsPlayerController.GiveNamedItem(item);
        }
    }

    private bool CheckIfNadeAvailable(Nades nades, CsItem nade)
    {
        if (!player.Controller.IsValid)
        {
            return false;
        }

        if (!CCsPlayerController.PawnIsAlive)
        {
            return false;
        }

        if (CCsPlayerController.Team < CsTeam.Terrorist || CCsPlayerController.Team > CsTeam.CounterTerrorist)
        {
            return false;
        }

        return nade switch
        {
            CsItem.Flashbang => nades.HasFlashbangs(),
            CsItem.Molotov or CsItem.Incendiary => nades.HasMolotovs(),
            CsItem.HEGrenade => nades.HasHeGrenades(),
            CsItem.SmokeGrenade => nades.HasSmokes(),
            _ => false
        };
    }

    /// <summary>
    /// Empties the two slots allocation is about to refill, so giving a loadout twice leaves the
    /// player with one of each instead of two. Every allocation path only ever gives, so without
    /// this a duplicated spawn event stacks a second full loadout.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT <c>RemoveWeapons()</c>: that also takes the C4, which the retakes plugin
    /// hands to a terrorist at round start, before this runs 0.1s after spawn. The bomb has a slot
    /// of its own and is never cleared here.
    ///
    /// The knife IS cleared, because all three paths hand one straight back - the only exception
    /// being a vote with <c>GiveKnife: false</c>, which now genuinely leaves the player without one
    /// rather than silently keeping the spawn knife as it did before.
    /// </remarks>
    public void ClearAllocatedWeapons()
    {
        if (!player.Controller.IsValid || !CCsPlayerController.PawnIsAlive)
        {
            return;
        }

        CCsPlayerController.RemoveItemBySlot(gear_slot_t.GEAR_SLOT_RIFLE);
        CCsPlayerController.RemoveItemBySlot(gear_slot_t.GEAR_SLOT_PISTOL);
        // Removing by slot only takes one item, and a player can now hold several grenades.
        foreach (var grenade in GrenadeDesignerNames)
        {
            CCsPlayerController.RemoveItemByDesignerName(grenade, true);
        }
        CCsPlayerController.RemoveItemBySlot(gear_slot_t.GEAR_SLOT_KNIFE);
        // The taser shares the knife slot, and removing by slot only takes one item.
        CCsPlayerController.RemoveItemByDesignerName("weapon_taser", true);
    }

    private static readonly string[] GrenadeDesignerNames =
    [
        "weapon_hegrenade", "weapon_flashbang", "weapon_smokegrenade", "weapon_molotov",
        "weapon_incgrenade", "weapon_decoy"
    ];

    private bool CanAllocate() =>
        player.Controller.IsValid
        && CCsPlayerController.PawnIsAlive
        && CCsPlayerController.Team is >= CsTeam.Terrorist and <= CsTeam.CounterTerrorist;

    /// <summary>
    /// Swaps just the primary for the one the loadout now names, leaving the pistol, knife,
    /// grenades and bomb untouched. Used when a saved loadout changed only that slot.
    /// </summary>
    /// <remarks>
    /// Does nothing while <see cref="ShouldGiveAwp"/> is set: that player's rifle this round came
    /// from the AWP roll and not from their loadout, so swapping it would quietly take the AWP away.
    /// </remarks>
    public void ReplacePrimary()
    {
        if (!CanAllocate() || GetsAwp)
        {
            return;
        }

        var item = CCsPlayerController.Team == CsTeam.Terrorist
            ? PrimaryT[PrimaryWeaponT].Item
            : PrimaryCt[PrimaryWeaponCt].Item;

        CCsPlayerController.RemoveItemBySlot(gear_slot_t.GEAR_SLOT_RIFLE);
        CCsPlayerController.GiveNamedItem(item);
    }

    /// <summary>As <see cref="ReplacePrimary"/>, for the pistol slot.</summary>
    public void ReplaceSecondary()
    {
        if (!CanAllocate() || GetsAwp)
        {
            return;
        }

        var item = CCsPlayerController.Team == CsTeam.Terrorist
            ? PistolsT[SecondaryWeaponT].Item
            : PistolsCT[SecondaryWeaponCt].Item;

        CCsPlayerController.RemoveItemBySlot(gear_slot_t.GEAR_SLOT_PISTOL);
        CCsPlayerController.GiveNamedItem(item);
    }

    public void AllocateTaser()
    {
        if (!CanAllocate())
        {
            return;
        }

        CCsPlayerController.GiveNamedItem(CsItem.Taser);
    }

    public void AllocateArmor(bool giveFull = true)
    {
        if (!player.Controller.IsValid)
        {
            return;
        }

        if (!CCsPlayerController.PawnIsAlive)
        {
            return;
        }

        if (CCsPlayerController.Team < CsTeam.Terrorist || CCsPlayerController.Team > CsTeam.CounterTerrorist)
        {
            return;
        }

        CCsPlayerController.GiveNamedItem(giveFull ? CsItem.KevlarHelmet : CsItem.Kevlar);
    }

    public void Allocate()
    {
        if (!player.Controller.IsValid)
        {
            return;
        }

        if (!CCsPlayerController.PawnIsAlive)
        {
            return;
        }

        if (CCsPlayerController.Team < CsTeam.Terrorist || CCsPlayerController.Team > CsTeam.CounterTerrorist)
        {
            return;
        }

        string primary;
        if (GetsAwp)
        {
            primary = "weapon_awp";
        }
        else
        {
            primary = CCsPlayerController.Team == CsTeam.Terrorist ? PrimaryT[PrimaryWeaponT].Item : PrimaryCt[PrimaryWeaponCt].Item;
        }

        string secondary;

        if (GetsAwp)
        {
            secondary = "weapon_deagle";
        }
        else
        {
            secondary = CCsPlayerController.Team == CsTeam.Terrorist ? PistolsT[SecondaryWeaponT].Item : PistolsCT[SecondaryWeaponCt].Item;
        }

        CCsPlayerController.GiveNamedItem(primary);
        CCsPlayerController.GiveNamedItem(secondary);
        CCsPlayerController.GiveNamedItem(CsItem.Knife);

        if (CCsPlayerController.Team == CsTeam.CounterTerrorist)
        {
           GiveCtEquipment();
        }
    }

    public void AllocatePistolRound()
    {
        if (!player.Controller.IsValid)
        {
            return;
        }

        if (!CCsPlayerController.PawnIsAlive)
        {
            return;
        }

        if (CCsPlayerController.Team is < CsTeam.Terrorist or > CsTeam.CounterTerrorist)
        {
            return;
        }

        var secondary = (CCsPlayerController.Team == CsTeam.Terrorist) ? Core.Config.PistolRound.WeaponT : Core.Config.PistolRound.WeaponCt;

        CCsPlayerController.GiveNamedItem(secondary);
        CCsPlayerController.GiveNamedItem(CsItem.Knife);

        if (CCsPlayerController.Team == CsTeam.CounterTerrorist)
        {
           GiveCtEquipment();
        }
    }

    /// <summary>
    /// Half-buy loadout: a random SMG/shotgun from the team's HalfBuy list, the player's saved
    /// pistol, and a knife. Never an AWP - the round-start roll is cleared for half buys.
    /// Armour and grenades are handed out by the caller according to the HalfBuy config.
    /// </summary>
    public void AllocateHalfBuy()
    {
        if (!CanAllocate())
        {
            return;
        }

        var isT = CCsPlayerController.Team == CsTeam.Terrorist;
        var pool = isT ? Core.Config.HalfBuy.WeaponsT : Core.Config.HalfBuy.WeaponsCt;

        if (pool.Count > 0)
        {
            CCsPlayerController.GiveNamedItem(Utils.GetRandomFromList(pool));
        }

        var secondary = isT ? PistolsT[SecondaryWeaponT].Item : PistolsCT[SecondaryWeaponCt].Item;

        CCsPlayerController.GiveNamedItem(secondary);
        CCsPlayerController.GiveNamedItem(CsItem.Knife);

        if (CCsPlayerController.Team == CsTeam.CounterTerrorist)
        {
           GiveCtEquipment();
        }
    }

    /// <summary>
    /// Special-round loadout: every weapon in the team's list, e.g. just a deagle, plus a knife.
    /// Armour, grenades and the taser are handed out by the caller according to the round's config.
    /// </summary>
    public void AllocateSpecialRound(SpecialRoundConfig special)
    {
        if (!CanAllocate())
        {
            return;
        }

        var weapons = CCsPlayerController.Team == CsTeam.Terrorist ? special.WeaponsT : special.WeaponsCt;

        foreach (var weapon in weapons)
        {
            CCsPlayerController.GiveNamedItem(weapon);
        }

        CCsPlayerController.GiveNamedItem(CsItem.Knife);

        if (CCsPlayerController.Team == CsTeam.CounterTerrorist)
        {
           GiveCtEquipment();
        }
    }

    public void AllocateVote(Vote vote)
    {
         if (!player.Controller.IsValid)
         {
             return;
         }

         if (!CCsPlayerController.PawnIsAlive)
         {
             return;
         }

         if (CCsPlayerController.Team < CsTeam.Terrorist || CCsPlayerController.Team > CsTeam.CounterTerrorist)
         {
             return;
         }

         var weapons = CCsPlayerController.Team == CsTeam.Terrorist ? vote.WeaponsT : vote.WeaponsCt;

         if(weapons.Count > 1)
         {
             ShowWeaponSelectionMenu(CCsPlayerController, weapons, WeaponSelectionTime);
         } else {
             CCsPlayerController.GiveNamedItem("weapon_" + weapons.First());
         }

         if(vote.GiveKnife)
             CCsPlayerController.GiveNamedItem(CsItem.Knife);

         if (CCsPlayerController.Team == CsTeam.CounterTerrorist)
         {
             GiveCtEquipment();
         }
    }

    private CsItem SelectGrenade()
    {
        var grenade = CsItem.HEGrenade;

        var rand = new Random().Next(0,4);

        grenade = rand switch
        {
            0 => CsItem.HEGrenade,
            1 => CsItem.Flashbang,
            2 => CsItem.SmokeGrenade,
            3 => CCsPlayerController.Team == CsTeam.Terrorist ? CsItem.Molotov : CsItem.Incendiary,
            _ => grenade
        };

        return grenade;
    }

    private void GiveCtEquipment()
    {
        if (
            CCsPlayerController is { Team: CsTeam.CounterTerrorist, PlayerPawn.IsValid: true }
            && CCsPlayerController.PlayerPawn.Value != null
            && CCsPlayerController.PlayerPawn.Value.IsValid
            && CCsPlayerController.PlayerPawn.Value.ItemServices != null
        ) {
            var itemServices = new CCSPlayer_ItemServices(CCsPlayerController.PlayerPawn.Value.ItemServices.Handle)
            {
                HasDefuser = true
            };
        }
    }
}