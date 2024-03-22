using HarmonyLib;
using Hazel;
using InnerNet;
using System.Collections.Generic;
using System.Linq;
using TOHE.Roles.Core;
using static TOHE.Options;

namespace TOHE.Roles.Neutral;

internal class Executioner : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 14200;
    public static HashSet<byte> playerIdList = [];
    public static bool HasEnabled => playerIdList.Count > 0;
    public override bool IsEnable => HasEnabled;
    public override CustomRoles ThisRoleBase => CustomRoles.Crewmate;

    //==================================================================\\

    private static OptionItem CanTargetImpostor;
    private static OptionItem CanTargetNeutralKiller;
    private static OptionItem CanTargetNeutralBenign;
    private static OptionItem CanTargetNeutralEvil;
    private static OptionItem CanTargetNeutralChaos;
    private static OptionItem KnowTargetRole;
    private static OptionItem ChangeRolesAfterTargetKilled;

    public static Dictionary<byte, byte> Target = [];
    
    private static readonly string[] ChangeRoles =
    [
        "Role.Crewmate",
        "Role.Celebrity",
        "Role.Bodyguard",
        "Role.Dictator",
        "Role.Mayor",
        "Role.Doctor",
        "Role.Jester",
        "Role.Opportunist",
        "Role.Convict",
    ];
    public static readonly CustomRoles[] CRoleChangeRoles =
    [
        CustomRoles.CrewmateTOHE, CustomRoles.Celebrity, CustomRoles.Bodyguard, CustomRoles.Dictator, CustomRoles.Mayor, CustomRoles.Doctor, CustomRoles.Jester, CustomRoles.Opportunist, CustomRoles.Convict,
    ];

    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.NeutralRoles, CustomRoles.Executioner);
        CanTargetImpostor = BooleanOptionItem.Create(Id + 10, "ExecutionerCanTargetImpostor", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralKiller = BooleanOptionItem.Create(Id + 12, "ExecutionerCanTargetNeutralKiller", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralBenign = BooleanOptionItem.Create(Id + 14, "CanTargetNeutralBenign", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralEvil = BooleanOptionItem.Create(Id + 15, "CanTargetNeutralEvil", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        CanTargetNeutralChaos = BooleanOptionItem.Create(Id + 16, "CanTargetNeutralChaos", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        KnowTargetRole = BooleanOptionItem.Create(Id + 13, "KnowTargetRole", false, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
        ChangeRolesAfterTargetKilled = StringOptionItem.Create(Id + 11, "ExecutionerChangeRolesAfterTargetKilled", ChangeRoles, 1, TabGroup.NeutralRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Executioner]);
    }
    public override void Init()
    {
        playerIdList = [];
        Target = [];
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);

        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.CheckDeadBodyOthers.Add(OnOthersDead);

            List<PlayerControl> targetList = [];
            var rand = IRandom.Instance;
            foreach (var target in Main.AllPlayerControls)
            {
                if (playerId == target.PlayerId) continue;
                else if (!CanTargetImpostor.GetBool() && target.Is(CustomRoleTypes.Impostor)) continue;
                else if (!CanTargetNeutralKiller.GetBool() && target.GetCustomRole().IsNK()) continue;
                else if (!CanTargetNeutralBenign.GetBool() && target.GetCustomRole().IsNB()) continue;
                else if (!CanTargetNeutralEvil.GetBool() && target.GetCustomRole().IsNE()) continue;
                else if (!CanTargetNeutralChaos.GetBool() && target.GetCustomRole().IsNC()) continue;
                if (target.GetCustomRole() is CustomRoles.GM or CustomRoles.SuperStar or CustomRoles.NiceMini or CustomRoles.EvilMini) continue;
                if (Utils.GetPlayerById(playerId).Is(CustomRoles.Lovers) && target.Is(CustomRoles.Lovers)) continue;

                targetList.Add(target);
            }
            var SelectedTarget = targetList[rand.Next(targetList.Count)];
            Target.Add(playerId, SelectedTarget.PlayerId);
            SendRPC(playerId, SelectedTarget.PlayerId, "SetTarget");
            Logger.Info($"{Utils.GetPlayerById(playerId)?.GetNameWithRole()}:{SelectedTarget.GetNameWithRole()}", "Executioner");
        }
    }
    public static void SendRPC(byte executionerId, byte targetId = 0x73, string Progress = "")
    {
        MessageWriter writer;
        switch (Progress)
        {
            case "SetTarget":
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetExecutionerTarget, SendOption.Reliable);
                writer.Write(executionerId);
                writer.Write(targetId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
            case "":
                if (!AmongUsClient.Instance.AmHost) return;
                writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RemoveExecutionerTarget, SendOption.Reliable);
                writer.Write(executionerId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                break;
            case "WinCheck":
                if (CustomWinnerHolder.WinnerTeam != CustomWinner.Default) break;
                if (!CustomWinnerHolder.CheckForConvertedWinner(executionerId))
                {           //まだ勝者が設定されていない場合
                    CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Executioner);
                    CustomWinnerHolder.WinnerIds.Add(executionerId);
                }
                break;
        }
    }
    public static void ReceiveRPC(MessageReader reader, bool SetTarget)
    {
        if (SetTarget)
        {
            byte ExecutionerId = reader.ReadByte();
            byte TargetId = reader.ReadByte();
            Target[ExecutionerId] = TargetId;
        }
        else
            Target.Remove(reader.ReadByte());
    }
    public static void ChangeRoleByTarget(PlayerControl target)
    {
        byte Executioner = 0x73;
        Target.Do(x =>
        {
            if (x.Value == target.PlayerId)
                Executioner = x.Key;
        });
        Utils.GetPlayerById(Executioner).RpcSetCustomRole(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()]);
        Target.Remove(Executioner);
        SendRPC(Executioner);
        Utils.NotifyRoles(SpecifySeer: Utils.GetPlayerById(Executioner));
    }
    public static void ChangeRole(PlayerControl executioner)
    {
        executioner.RpcSetCustomRole(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()]);
        Target.Remove(executioner.PlayerId);
        SendRPC(executioner.PlayerId);
        var text = Utils.ColorString(Utils.GetRoleColor(CustomRoles.Executioner), Translator.GetString(""));
        text = string.Format(text, Utils.ColorString(Utils.GetRoleColor(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()]), Translator.GetString(CRoleChangeRoles[ChangeRolesAfterTargetKilled.GetValue()].ToString())));
        executioner.Notify(text);
    }

    public static bool CheckTarget(byte targetId) => Target.ContainsValue(targetId);
    public static bool IsTarget(byte executionerId, byte targetId) => Target.TryGetValue(executionerId, out var exeTargetId) && exeTargetId == targetId;

    public override void OnMurderPlayerAsTarget(PlayerControl killer, PlayerControl target, bool inMeeting)
    {
        ExecutionerWasDead(target.PlayerId);
    }
    private void OnOthersDead(PlayerControl killer, PlayerControl target, bool inMeeting)
    {
        if (CheckTarget(target.PlayerId))
            ChangeRoleByTarget(target);
    }

    public override void OnPlayerLeft(ClientData clientData)
    {
        if (Target.ContainsKey(clientData.Character.PlayerId))
        {
            ChangeRole(clientData.Character);
        }

        else if (CheckTarget(clientData.Character.PlayerId))
            ChangeRoleByTarget(clientData.Character);
    }

    private static void ExecutionerWasDead(byte targetId)
    {
        if (Target.ContainsKey(targetId))
        {
            Target.Remove(targetId);
            SendRPC(targetId);
        }
    }

    public override bool KnowRoleTarget(PlayerControl player, PlayerControl target)
    {
        if (!KnowTargetRole.GetBool()) return false;
        return player.Is(CustomRoles.Executioner) && Target.TryGetValue(player.PlayerId, out var tar) && tar == target.PlayerId;
    }

    public override string GetMark(PlayerControl seer, PlayerControl target = null, bool isForMeeting = false)
    {
        if (target == null || !seer.IsAlive()) return string.Empty;

        var GetValue = Target.TryGetValue(seer.PlayerId, out var targetId);
        return GetValue && targetId == target.PlayerId ? Utils.ColorString(Utils.GetRoleColor(CustomRoles.Executioner), "♦") : string.Empty;
    }

    public override void CheckExileTarget(GameData.PlayerInfo exiled, ref bool DecidedWinner, bool isMeetingHud, ref string name)
    {
        foreach (var kvp in Target.Where(x => x.Value == exiled.PlayerId).ToArray())
        {
            var executioner = Utils.GetPlayerById(kvp.Key);
            if (executioner == null || !executioner.IsAlive() || executioner.Data.Disconnected) continue;
            if (!isMeetingHud) ExeWin(kvp.Key, DecidedWinner);

            if (isMeetingHud)
            {
                name = string.Format(Translator.GetString("ExiledExeTarget"), Main.LastVotedPlayer, Utils.GetDisplayRoleAndSubName(exiled.PlayerId, exiled.PlayerId, true));
                DecidedWinner = true;
            }
        }
    }
    private static void ExeWin(byte playerId, bool DecidedWinner)
    {
        if (!DecidedWinner)
        {
            SendRPC(playerId, Progress: "WinCheck");
        }
        else
        {
            CustomWinnerHolder.AdditionalWinnerTeams.Add(AdditionalWinners.Executioner);
            CustomWinnerHolder.WinnerIds.Add(playerId);
        }
    }
}