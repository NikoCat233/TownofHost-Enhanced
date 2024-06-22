﻿using AmongUs.GameOptions;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Neutral;

internal class Terrorist : RoleBase
{
    //===========================SETUP================================\\
    private const int id = 15400;
    private static readonly HashSet<byte> PlayerIds = [];
    public static bool HasEnabled = PlayerIds.Any();
    
    public override CustomRoles ThisRoleBase => CustomRoles.Engineer;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.NeutralChaos;
    //==================================================================\\
    public override bool HasTasks(NetworkedPlayerInfo player, CustomRoles role, bool ForRecompute) => !ForRecompute;

    public static OptionItem CanTerroristSuicideWin;
    public static OptionItem TerroristCanGuess;

    public override void SetupCustomOption()
    {

        SetupRoleOptions(15400, TabGroup.NeutralRoles, CustomRoles.Terrorist);
        CanTerroristSuicideWin = BooleanOptionItem.Create(15402, "CanTerroristSuicideWin", false, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
        TerroristCanGuess = BooleanOptionItem.Create(15403, "CanGuess", true, TabGroup.NeutralRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Terrorist]);
        OverrideTasksData.Create(15404, TabGroup.NeutralRoles, CustomRoles.Terrorist);
    }
    public override void Init()
    {
        PlayerIds.Clear();
    }
    public override void Add(byte playerId)
    {
        PlayerIds.Add(playerId);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.EngineerCooldown = 1f;
        AURoleOptions.EngineerInVentMaxTime = 0f;
    }
    public override void OnMurderPlayerAsTarget(PlayerControl killer, PlayerControl target, bool inMeeting, bool isSuicide)
    {
        Logger.Info(target?.Data?.PlayerName + " was Terrorist", "AfterPlayerDeathTasks");
        CheckTerroristWin(target.Data);
    }
    public override void CheckExile(NetworkedPlayerInfo exiled, ref bool DecidedWinner, bool isMeetingHud, ref string name)
    {
        CheckTerroristWin(exiled);
    }
    private static void CheckTerroristWin(NetworkedPlayerInfo terrorist)
    {
        var taskState = Utils.GetPlayerById(terrorist.PlayerId).GetPlayerTaskState();
        if (taskState.IsTaskFinished && (!Main.PlayerStates[terrorist.PlayerId].IsSuicide || CanTerroristSuicideWin.GetBool()))
        {
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc.Is(CustomRoles.Terrorist))
                {
                    if (Main.PlayerStates[pc.PlayerId].deathReason == PlayerState.DeathReason.Vote)
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.etc;
                    }
                    else
                    {
                        Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Suicide;
                    }
                }
                else if (!pc.Data.IsDead)
                {
                    Main.PlayerStates[pc.PlayerId].deathReason = PlayerState.DeathReason.Bombed;
                    Main.PlayerStates[pc.PlayerId].SetDead();
                    pc.RpcMurderPlayer(pc);
                    pc.SetRealKiller(terrorist.Object);
                }
            }
            if (!CustomWinnerHolder.CheckForConvertedWinner(terrorist.PlayerId))
            {
                CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Terrorist);
                CustomWinnerHolder.WinnerIds.Add(terrorist.PlayerId);
            }
        }
    }
    public override bool GuessCheck(bool isUI, PlayerControl guesser, PlayerControl pc, CustomRoles role, ref bool guesserSuicide)
    {
        if (!TerroristCanGuess.GetBool())
        {
            if (!isUI) Utils.SendMessage(GetString("GuessDisabled"), pc.PlayerId);
            else pc.ShowPopUp(GetString("GuessDisabled"));
            return true;
        }
        return false;
    }
}
