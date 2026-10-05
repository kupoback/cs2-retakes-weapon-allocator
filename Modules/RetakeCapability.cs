using CounterStrikeSharp.API.Core.Capabilities;

using RetakesAllocator.Modules.Models;
using RetakesPluginShared;
using RetakesPluginShared.Events;

using static RetakesAllocator.Modules.Core;
using static RetakesAllocator.Modules.Utils;

namespace RetakesAllocator.Modules;

public class RetakeCapability
{

    private static IRetakesPluginEventSender? RetakesPluginEventSender { get; set; }

    public static void RetakeCapability_OnLoad()
    {
        Plugin.AddTimer(0.1f, () => { GetRetakesPluginEventSender().RetakesPluginEventHandlers += RetakesEventHandler; });
    }

    public static void RetakeCapability_OnUnload()
    {
        GetRetakesPluginEventSender().RetakesPluginEventHandlers -= RetakesEventHandler;
    }

    private static IRetakesPluginEventSender GetRetakesPluginEventSender()
    {
        if (RetakesPluginEventSender is not null)
        {
            return RetakesPluginEventSender;
        }

        var sender = new PluginCapability<IRetakesPluginEventSender>("retakes_plugin:event_sender").Get();

        RetakesPluginEventSender = sender ?? throw new Exception("Couldn't load retakes plugin event sender capability");
        return sender;
    }

    private static void RetakesEventHandler(object? _, IRetakesPluginEvent @event)
    {
        Action? handler = @event switch
        {
            AnnounceBombsiteEvent => HandleAnnounceBombsiteEvent,
            _ => null
        };
        handler?.Invoke();
    }

    private static void HandleAnnounceBombsiteEvent()
    {
        if(GetGameRules().WarmupPeriod)
        {
            return;
        }

        var pistolRoundsLeft = Core.Config.PistolRound.RoundAmount - RoundsCounter;

        string mode = CurrentRoundType switch
        {
            RoundType.Pistol when pistolRoundsLeft > 0 => $"pistol rounds, {pistolRoundsLeft} rounds left",
            RoundType.Pistol => "pistol round",
            RoundType.HalfBuy => "half buy round",
            RoundType.Vote when CurrentVote != null! => CurrentVote.Vote.Description + " mode",
            _ when Core.Config.RoundTypes.Enabled => "full buy round",
            _ => "normal mode"
        };

        PrintToChatAll($"{Prefix} Retake {mode}.");
    }
}