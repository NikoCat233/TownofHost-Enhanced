﻿using Hazel;
using InnerNet;
using System;
using System.Text.RegularExpressions;
using TOHE.Modules.ChatManager;
using TOHE.Roles.Core;
using UnityEngine;
using static TOHE.Translator;
using static TOHE.Utils;


namespace TOHE.Roles.Neutral;
internal class Pirate : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 15000;
    public static bool HasEnabled => CustomRoleManager.HasEnabled(CustomRoles.Pirate);
    public override CustomRoles ThisRoleBase => CustomRoles.Impostor;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.NeutralChaos;
    //==================================================================\\

    private static OptionItem SuccessfulDuelsToWin;
    private static OptionItem TryHideMsg;
    private static OptionItem DuelCooldown;

    private static readonly Dictionary<byte, bool> DuelDone = [];

    public static byte PirateTarget;
    private static int pirateChose, targetChose;
    public static int NumWin = 0;

    public override void SetupCustomOption()
    {
        Options.SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Pirate);
        DuelCooldown = FloatOptionItem.Create(Id + 12, "DuelCooldown", new(0f, 180f, 2.5f), 22.5f, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pirate])
                .SetValueFormat(OptionFormat.Seconds);
        TryHideMsg = BooleanOptionItem.Create(Id + 10, "PirateTryHideMsg", true, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pirate])
            .SetColor(Color.green);
        SuccessfulDuelsToWin = IntegerOptionItem.Create(Id + 11, "SuccessfulDuelsToWin", new(1, 20, 1), 2, TabGroup.NeutralRoles, false).SetParent(Options.CustomRoleSpawnChances[CustomRoles.Pirate])
            .SetValueFormat(OptionFormat.Times);
    }

    public override void Init()
    {
        PirateTarget = byte.MaxValue;
        DuelDone.Clear();
        pirateChose = -1;
        targetChose = -1;
        NumWin = 0;
    }
    public override void Add(byte playerId)
    {
        DuelDone.Add(playerId, false);

        if (!Main.ResetCamPlayerList.Contains(playerId))
            Main.ResetCamPlayerList.Add(playerId);
    }
    public override void MeetingHudClear()
    {
        if (!HasEnabled || PirateTarget == byte.MaxValue) return;

        var pc = _Player;
        var tpc = Utils.GetPlayerById(PirateTarget);
        if (!tpc.IsAlive()) return;
        _ = new LateTask(() =>
        {
            Utils.SendMessage(GetString("PirateMeetingMsg"), pc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pirate), GetString("PirateTitle")));
            Utils.SendMessage(GetString("PirateTargetMeetingMsg"), tpc.PlayerId, Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pirate), GetString("PirateTitle")));
        }, 3f, "Pirate Meeting Messages");
    }
    public override void SetKillCooldown(byte id) => Main.AllPlayerKillCooldown[id] = DuelCooldown.GetFloat();
    public override bool CanUseKillButton(PlayerControl pc) => true;
    public override string GetProgressText(byte playerId, bool comms)
            => ColorString(GetRoleColor(CustomRoles.Pirate).ShadeColor(0.25f), $"({Pirate.NumWin}/{Pirate.SuccessfulDuelsToWin.GetInt()})");
    
    public void SendRPC(int operate, byte target = byte.MaxValue, int points = -1)
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SyncRoleSkill, SendOption.Reliable, -1);
        writer.WriteNetObject(_Player);
        writer.Write(operate);
        writer.Write(target);
        if (operate == 1)
        {
            writer.Write(points);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    public override void ReceiveRPC(MessageReader reader, PlayerControl NaN)
    {
        int operate = reader.ReadInt32();
        byte target = reader.ReadByte();
        PirateTarget = target;
        if (operate == 1)
        {
            int points = reader.ReadInt32();
            NumWin = points;
        }
    }

    public override bool OnCheckMurderAsKiller(PlayerControl killer, PlayerControl target)
    {
        if (target.Is(CustomRoles.Pestilence)) return true;
        if (PirateTarget != byte.MaxValue)
        {
            killer.Notify(GetString("PirateTargetAlreadyChosen"));
            return false;
        }
        Logger.Msg($"{killer.GetNameWithRole()} chose a target {target.GetNameWithRole()}", "Pirate");
        PirateTarget = target.PlayerId;
        SendRPC(operate: 0, target: target.PlayerId, points: -1);
        DuelDone.Add(PirateTarget, false);
        if (!Options.DisableShieldAnimations.GetBool()) killer.RpcGuardAndKill(killer);
        else killer.SetKillCooldown();
        return false;
    }
    public override void SetAbilityButtonText(HudManager hud, byte playerId)
    {
        hud.KillButton.OverrideText(GetString("PirateDuelButtonText"));
    }
    public override Sprite GetKillButtonSprite(PlayerControl player, bool shapeshifting) => CustomButton.Get("Challenge");

    public override string GetMarkOthers(PlayerControl seer, PlayerControl target, bool isMeeting = false)
    {
        if (target != null && isMeeting && target.PlayerId == PirateTarget)
        {
            return Utils.ColorString(Utils.GetRoleColor(CustomRoles.Pirate), " ⦿");
        }
        return "";
    }
    public override void AfterMeetingTasks()
    {
        var pirateId = _state.PlayerId;
        if (PirateTarget != byte.MaxValue)
        {
            if (DuelDone[pirateId])
            {
                if (DuelDone[PirateTarget])
                {
                    if (targetChose == pirateChose)
                    {
                        NumWin++;
                        if (Utils.GetPlayerById(PirateTarget).IsAlive())
                        {
                            CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Pirate, PirateTarget);
                            Utils.GetPlayerById(PirateTarget).SetRealKiller(Utils.GetPlayerById(pirateId));
                        }
                    }
                }
                else
                if (Utils.GetPlayerById(PirateTarget).IsAlive())
                {
                    CheckForEndVotingPatch.TryAddAfterMeetingDeathPlayers(PlayerState.DeathReason.Pirate, PirateTarget);
                    Utils.GetPlayerById(PirateTarget).SetRealKiller(Utils.GetPlayerById(pirateId));
                }
            }
        }
        if (NumWin >= SuccessfulDuelsToWin.GetInt())
        {
            NumWin = SuccessfulDuelsToWin.GetInt();
            if (!CustomWinnerHolder.CheckForConvertedWinner(pirateId))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Pirate);
                CustomWinnerHolder.WinnerIds.Add(pirateId);
            }
        }
        DuelDone.Clear();
        PirateTarget = byte.MaxValue;
        SendRPC(operate: 1, target: byte.MaxValue, points: NumWin);
        foreach (byte playerId in Main.PlayerStates.Values.Where(x => x.MainRole == CustomRoles.Pirate).Select(x => x.PlayerId)) { DuelDone.Add(playerId, false); }
    }

    public static bool DuelCheckMsg(PlayerControl pc, string msg, bool isUI = false)
    {
        var originMsg = msg;
        if (!AmongUsClient.Instance.AmHost) return false;
        if (!GameStates.IsMeeting || pc == null || GameStates.IsExilling) return false;
        if (!pc.Is(CustomRoles.Pirate) && PirateTarget != pc.PlayerId) return false;


        msg = msg.ToLower().TrimStart().TrimEnd();
        bool operate = false;
        if (CheckCommond(ref msg, "duel")) operate = true;
        else return false;

        if (!pc.IsAlive())
        {
            Utils.SendMessage(GetString("PirateDead"), pc.PlayerId);
            return true;
        }

        if (operate)
        {

            if (TryHideMsg.GetBool())
            {
                //if (Options.NewHideMsg.GetBool()) ChatManager.SendPreviousMessagesToAll();
                //else TryHideMsgForDuel();
                TryHideMsgForDuel();
                ChatManager.SendPreviousMessagesToAll();
            }
            else if (pc.AmOwner) Utils.SendMessage(originMsg, 255, pc.GetRealName());

            if (!MsgToPlayerAndRole(msg, out int rpsOption, out string error))
            {
                Utils.SendMessage(error, pc.PlayerId);
                return true;
            }

            Logger.Info($"{pc.GetNameWithRole()} selected {rpsOption}", "Pirate");

            if (DuelDone[pc.PlayerId])
            {
                _ = new LateTask(() =>
                {
                    if (!isUI) Utils.SendMessage(GetString("DuelAlreadyDone"), pc.PlayerId);
                    else pc.ShowPopUp(GetString("DuelAlreadyDone"));
                    Logger.Msg("Duel attempted more than once", "Pirate");
                }, 0.2f, "Pirate Duel Already Done");
                return true;
            }

            else
            {
                if (pc.Is(CustomRoles.Pirate))
                {
                    pirateChose = rpsOption;

                }
                else
                {
                    targetChose = rpsOption;
                    //_ = new LateTask(() =>
                    //{
                    //    if (!isUI) Utils.SendMessage(String.Format(GetString("TargetDuelDone"), OptionList[pirateChose]), pc.PlayerId);
                    //    else pc.ShowPopUp(String.Format(GetString("TargetDuelDone"), OptionList[pirateChose]));
                    //    Logger.Msg($"Target chose {targetChose}", "Pirate");
                    //}, 0.2f, "Pirate");
                    //DuelDone[pc.PlayerId] = true;
                    //return true;
                }
                _ = new LateTask(() =>
                {
                    if (!isUI) Utils.SendMessage(String.Format(GetString("DuelDone"), rpsOption), pc.PlayerId);
                    else pc.ShowPopUp(String.Format(GetString("DuelDone"), rpsOption));
                }, 0.2f, "Pirate Duel Done");
                DuelDone[pc.PlayerId] = true;
                return true;

            }
        }
        return true;
    }


    private static bool MsgToPlayerAndRole(string msg, out int rpsOpt, out string error)
    {
        if (msg.StartsWith("/")) msg = msg.Replace("/", string.Empty);

        Regex r = new("\\d+");
        MatchCollection mc = r.Matches(msg);
        string result = string.Empty;
        for (int i = 0; i < mc.Count; i++)
        {
            result += mc[i];//匹配结果是完整的数字，此处可以不做拼接的
        }

        if (int.TryParse(result, out int num))
        {
            if (num < 0 || num > 2)
            {
                rpsOpt = -1;
                error = GetString("DuelHelp");
                return false;
            }
            else { rpsOpt = num; }
        }
        else
        {
            rpsOpt = -1;
            error = GetString("DuelHelp");
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool CheckCommond(ref string msg, string command)
    {
        var comList = command.Split('|');
        for (int i = 0; i < comList.Length; i++)
        {
            //if (exact)
            //{
            //    if (msg == "/" + comList[i]) return true;
            //}
            //else
            //{
            if (msg.StartsWith("/" + comList[i]))
            {
                msg = msg.Replace("/" + comList[i], string.Empty);
                return true;
            }
            //}
        }
        return false;
    }

    public static void TryHideMsgForDuel()
    {
        ChatUpdatePatch.DoBlockChat = true;
        List<CustomRoles> roles = CustomRolesHelper.AllRoles.Where(x => x is not CustomRoles.NotAssigned).ToList();
        var rd = IRandom.Instance;
        string msg;
        string[] command = ["duel", "rps"];
        for (int i = 0; i < 20; i++)
        {
            msg = "/";
            if (rd.Next(1, 100) < 20)
            {
                msg += "id";
            }
            else
            {
                msg += command[rd.Next(0, command.Length - 1)];
                msg += " ";
                msg += rd.Next(0, 3).ToString();
            }
            var player = Main.AllAlivePlayerControls.RandomElement();
            DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
            writer.StartMessage(-1);
            writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
                .Write(msg)
                .EndRpc();
            writer.EndMessage();
            writer.SendMessage();
        }
        ChatUpdatePatch.DoBlockChat = false;
    }


}
