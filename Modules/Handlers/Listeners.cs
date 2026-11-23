using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;

using static RetakesAllocator.Modules.Core;
using static RetakesAllocator.Modules.Utils;
using static RetakesAllocator.Modules.Votes.Votes;
using RetakesAllocator.Modules.Votes;
using RetakesAllocator.Modules.Weapons;

namespace RetakesAllocator.Modules.Handlers;

internal static class Listeners
{
    public static void RegisterListeners()
    {
        Plugin.RegisterListener<CounterStrikeSharp.API.Core.Listeners.OnMapStart>(OnMapStart);
        Plugin.RegisterListener<CounterStrikeSharp.API.Core.Listeners.OnClientAuthorized>(OnClientAuthorized);
        Plugin.RegisterListener<CounterStrikeSharp.API.Core.Listeners.OnClientDisconnect>(OnClientDisconnect);

        Plugin.AddCommandListener("say", OnSay);
        Plugin.AddCommandListener("say_team", OnSay);
    }  

    private static void OnMapStart(string mapName)
    {
        RoundsCounter = 0;
        Players.Clear();
        Utilities.GetPlayers().ForEach(AddPlayerToList);
        Votes_OnMapStart();
    }

    private static void OnClientAuthorized(int playerSlot, SteamID steamID)
    {
        CCSPlayerController player = Utilities.GetPlayerFromSlot(playerSlot)!;
        AddPlayerToList(player);
    }

    private static void OnClientDisconnect(int playerSlot)
    {
        // First, and by slot: it needs no controller, so nothing below can stop the menu's session
        // being dropped.
        LoadoutPanel.OnPlayerDisconnect(playerSlot);

        var player = Utilities.GetPlayerFromSlot(playerSlot);

        if (player == null || !player.IsValid)
        {
            return;
        }

        RemovePlayerFromList(player);

        if (player.UserId != null)
        {
            Votes_OnPlayerDisconnect(player);
        }
    }

    private static HookResult OnSay(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
            return HookResult.Continue;

        if(command.ArgCount < 2)
                return HookResult.Continue;

        var message = command.GetArg(1);

        if(!Core.Config.TriggerWords.Any(word => word.Equals(message)))
            return HookResult.Continue;

        var playerObj = FindPlayer(player); 

        if (playerObj == null!)
        {
            ReplyToCommand(command, $"{Prefix} This command can only be executed by a valid player.");
            return HookResult.Continue;
        }

        LoadoutPanel.Open(player);

        return HookResult.Continue;
    }
}
