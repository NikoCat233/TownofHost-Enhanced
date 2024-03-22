﻿using Hazel;
using System.Collections.Generic;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.Core;
using TOHE.Roles.Neutral;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Impostor;

internal class Lightning : RoleBase
{
    private const int Id = 24100;

    public static bool On;
    public override bool IsEnable => On;
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;

    private static OptionItem KillCooldown;
    private static OptionItem ConvertTime;
    private static OptionItem KillerConvertGhost;

    private static List<byte> playerIdList = [];
    private static List<byte> GhostPlayer = [];
    private static Dictionary<byte, PlayerControl> RealKiller = [];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.ImpostorRoles, CustomRoles.Lightning);
        KillCooldown = FloatOptionItem.Create(Id + 10, "LightningKillCooldown", new(0f, 180f, 2.5f), 30f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lightning])
            .SetValueFormat(OptionFormat.Seconds);
        ConvertTime = FloatOptionItem.Create(Id + 12, "LightningConvertTime", new(0f, 180f, 2.5f), 10f, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lightning])
            .SetValueFormat(OptionFormat.Seconds);
        KillerConvertGhost = BooleanOptionItem.Create(Id + 14, "LightningKillerConvertGhost", true, TabGroup.ImpostorRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Lightning]);
    }
    public override void Init()
    {
        On = false;
        playerIdList = [];
        GhostPlayer = [];
        RealKiller = [];
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        On = true;

        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.MarkOthers.Add(GetMarkInGhostPlayer);
        }
    }
    private static void SendRPC(byte playerId)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.LightningSetGhostPlayer, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(IsGhost(playerId));
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte GhostId = reader.ReadByte();
        bool isGhost = reader.ReadBoolean();
        if (GhostId == byte.MaxValue)
        {
            GhostPlayer = [];
            return;
        }
        if (isGhost)
        {
            if (!GhostPlayer.Contains(GhostId))
                GhostPlayer.Add(GhostId);
        }
        else
        {
            if (GhostPlayer.Contains(GhostId))
                GhostPlayer.Remove(GhostId);
        }
    }
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = KillCooldown.GetFloat();
    
    public static bool IsGhost(PlayerControl player) => IsGhost(player.PlayerId);
    private static bool IsGhost(byte id) => GhostPlayer.Contains(id);
    
    public override bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target)
    {
        if (killer == null || target == null || !killer.Is(CustomRoles.Lightning)) return false;
        if (IsGhost(target)) return false;

        killer.RpcGuardAndKill();
        target.RpcGuardAndKill();

        killer.SetKillCooldown();

        killer.RPCPlayCustomSound("Shield");
        StartConvertCountDown(killer, target);

        return false;
    }
    private static void StartConvertCountDown(PlayerControl killer, PlayerControl target)
    {
        _ = new LateTask(() =>
        {
            if (GameStates.IsInGame && GameStates.IsInTask && !GameStates.IsMeeting && target.IsAlive() && !Pelican.IsEaten(target.PlayerId))
            {
                GhostPlayer.Add(target.PlayerId);
                SendRPC(target.PlayerId);
                RealKiller.TryAdd(target.PlayerId, killer);

                if (!killer.inVent)
                    killer.RpcGuardAndKill(killer);

                Utils.NotifyRoles();
                Logger.Info($"{target.GetNameWithRole()} transformed into a quantum ghost", "Lightning");
            }
        }, ConvertTime.GetFloat(), "Lightning Convert Player To Ghost");
    }
    public override void OnMurderPlayerAsTarget(PlayerControl killer, PlayerControl target, bool inMeeting)
    {
        if (inMeeting) return;

        if (killer == null || target == null || killer == target) return;
        if (!KillerConvertGhost.GetBool() || IsGhost(killer)) return;
        RealKiller.TryAdd(killer.PlayerId, target);
        StartConvertCountDown(target, killer);
    }
    public override void OnFixedUpdateLowLoad(PlayerControl lightning)
    {
        if (GhostPlayer.Count <= 0) return;

        List<byte> deList = [];
        foreach (var ghost in GhostPlayer.ToArray())
        {
            var gs = Utils.GetPlayerById(ghost);
            if (gs == null || !gs.IsAlive() || gs.Data.Disconnected)
            {
                deList.Add(gs.PlayerId);
                continue;
            }
            var allAlivePlayerControls = Main.AllAlivePlayerControls.Where(x => x.PlayerId != gs.PlayerId && x.IsAlive() && !x.Is(CustomRoles.Lightning) && !IsGhost(x) && !Pelican.IsEaten(x.PlayerId)).ToArray();
            foreach (var pc in allAlivePlayerControls)
            {
                var pos = gs.transform.position;
                var dis = Vector2.Distance(pos, pc.transform.position);
                if (dis > 0.3f) continue;

                deList.Add(gs.PlayerId);
                Main.PlayerStates[gs.PlayerId].deathReason = PlayerState.DeathReason.Quantization;
                gs.SetRealKiller(RealKiller[gs.PlayerId]);
                gs.RpcMurderPlayerV3(gs);

                Logger.Info($"{gs.GetNameWithRole()} As a quantum ghost dying from a collision", "Lightning");
                break;
            }
        }
        if (deList.Count > 0)
        {
            GhostPlayer.RemoveAll(deList.Contains);
            foreach (var gs in deList.ToArray()) SendRPC(gs);
            Utils.NotifyRoles();
        }
    }
    public override void OnReportDeadBody(PlayerControl reporter, PlayerControl target)
    {
        foreach (var ghost in GhostPlayer.ToArray())
        {
            var gs = Utils.GetPlayerById(ghost);
            if (gs == null) continue;
            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Quantization, gs.PlayerId);
            gs.SetRealKiller(RealKiller[gs.PlayerId]);
            Logger.Info($"{gs.GetNameWithRole()} is quantum ghost - dead on start meeting", "Lightning");
        }
        GhostPlayer = [];
        SendRPC(byte.MaxValue);
        Utils.NotifyRoles();
    }

    public string GetMarkInGhostPlayer(PlayerControl seer, PlayerControl target = null, bool isForMeeting = false)
    {
        if (isForMeeting) return string.Empty;

        target ??= seer;

        return (!seer.IsAlive() && seer != target && IsGhost(target)) || IsGhost(target) ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Lightning), "■") : string.Empty;
    }

    public override void SetAbilityButtonText(HudManager hud, byte playerId)
    {
        hud.KillButton.OverrideText(Translator.GetString("LightningButtonText"));
    }
}