﻿using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Il2CppSystem.CodeDom;
using TOHE.Roles.Core;
using TOHE.Roles.Double;
using UnityEngine;
using static TOHE.Options;
using static TOHE.Modules.HazelExtensions;
using static TOHE.Translator;
using Hazel;
using static UnityEngine.GraphicsBuffer;

namespace TOHE.Roles.Impostor;

//EHR - https://github.com/Gurge44/EndlessHostRoles/blob/main/Roles/Impostor/Abyssbringer.cs
internal class AbyssBringer : RoleBase
{
    public override CustomRoles ThisRoleBase => CustomRoles.Shapeshifter;
    public override Custom_RoleType ThisRoleType => Custom_RoleType.ImpostorConcealing;
    private static OptionItem BlackHolePlaceCooldown;
    private static OptionItem BlackHoleDespawnMode;
    private static OptionItem BlackHoleDespawnTime;
    private static OptionItem BlackHoleMovesTowardsNearestPlayer;
    private static OptionItem BlackHoleMoveSpeed;
    private static OptionItem BlackHoleRadius;

    private readonly List<BlackHoleData> BlackHoles = [];

    public override void SetupCustomOption()
    {
        int id = 29100;
        const TabGroup tab = TabGroup.ImpostorRoles;
        const CustomRoles role = CustomRoles.Abyssbringer;
        Options.SetupRoleOptions(id++, tab, role);
        BlackHolePlaceCooldown = IntegerOptionItem.Create(++id, "BlackHolePlaceCooldown", new(1, 180, 1), 30, tab, false)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Seconds);
        BlackHoleDespawnMode = StringOptionItem.Create(++id, "BlackHoleDespawnMode", Enum.GetNames<DespawnMode>(), 0, tab, false)
            .SetParent(Options.CustomRoleSpawnChances[role]);
        BlackHoleDespawnTime = IntegerOptionItem.Create(++id, "BlackHoleDespawnTime", new(1, 60, 1), 15, tab, false)
            .SetParent(BlackHoleDespawnMode)
            .SetValueFormat(OptionFormat.Seconds);
        BlackHoleMovesTowardsNearestPlayer = BooleanOptionItem.Create(++id, "BlackHoleMovesTowardsNearestPlayer", true, tab, false)
            .SetParent(Options.CustomRoleSpawnChances[role]);
        BlackHoleMoveSpeed = FloatOptionItem.Create(++id, "BlackHoleMoveSpeed", new(0.25f, 10f, 0.25f), 1f, tab, false)
            .SetParent(BlackHoleMovesTowardsNearestPlayer);
        BlackHoleRadius = FloatOptionItem.Create(++id, "BlackHoleRadius", new(0.1f, 5f, 0.1f), 1.2f, tab, false)
            .SetParent(Options.CustomRoleSpawnChances[role])
            .SetValueFormat(OptionFormat.Multiplier);
    }

    public override void ApplyGameOptions(IGameOptions opt, byte playerId)
    {
        AURoleOptions.ShapeshifterCooldown = BlackHolePlaceCooldown.GetInt();
        AURoleOptions.ShapeshifterDuration = 1f;
    }

    public override void UnShapeShiftButton(PlayerControl shapeshifter)
    {
        var pos = shapeshifter.GetCustomPosition();
        var room = shapeshifter.GetPlainShipRoom();
        var roomName = room == null ? string.Empty : Translator.GetString($"{room.RoomId}");
        BlackHoles.Add(new(new(pos, _state.PlayerId), Utils.TimeStamp, pos, roomName, 0));
        Utils.SendRPC(CustomRPC.SyncRoleSkill, _Player, 1, pos, roomName);
    }

    public override void OnFixedUpdate(PlayerControl pc, bool lowLoad, long nowTime)
    {
        var abyssbringer = _Player;
        int count = BlackHoles.Count;
        for (int i = 0; i < count; i++)
        {
            var blackHole = BlackHoles[i];

            var despawnMode = (DespawnMode)BlackHoleDespawnMode.GetValue();
            switch (despawnMode)
            {
                case DespawnMode.AfterTime when Utils.TimeStamp - blackHole.PlaceTimeStamp > BlackHoleDespawnTime.GetInt():
                case DespawnMode.AfterTime when Utils.TimeStamp - blackHole.PlaceTimeStamp > BlackHoleDespawnTime.GetInt():
                    RemoveBlackHole();
                    continue;
                case DespawnMode.AfterMeeting when Main.MeetingIsStarted:
                    RemoveBlackHole();
                    continue;
            }

            var nearestPlayer = Main.AllAlivePlayerControls.Where(x => x != pc).MinBy(x => Vector2.Distance(x.GetCustomPosition(), blackHole.Position));
            if (nearestPlayer != null)
            {
                var pos = nearestPlayer.GetCustomPosition();

                if (BlackHoleMovesTowardsNearestPlayer.GetBool() && GameStates.IsInTask && !ExileController.Instance)
                {
                    var direction = (pos - blackHole.Position).normalized;
                    var newPosition = blackHole.Position + direction * BlackHoleMoveSpeed.GetFloat() * Time.fixedDeltaTime;
                    blackHole.NetObject.TP(newPosition);
                    blackHole.Position = newPosition;
                }

                if (Vector2.Distance(pos, blackHole.Position) <= BlackHoleRadius.GetFloat())
                {
                    nearestPlayer.RpcExileV2();
                    blackHole.PlayersConsumed++;
                    Utils.SendRPC(CustomRPC.SyncRoleSkill, _Player, 2, i);
                    Notify();

                    var state = Main.PlayerStates[nearestPlayer.PlayerId];
                    state.deathReason = PlayerState.DeathReason.Consumed;
                    state.RealKiller = (DateTime.Now, _state.PlayerId);
                    state.SetDead();

                    if (despawnMode == DespawnMode.After1PlayerEaten)
                    {
                        RemoveBlackHole();
                    }
                }
            }

            continue;

            void RemoveBlackHole()
            {
                BlackHoles.RemoveAt(i);
                blackHole.NetObject.Despawn();
                Utils.SendRPC(CustomRPC.SyncRoleSkill, _Player, 3, i);
                Notify();
            }

            void Notify() => Utils.NotifyRoles(SpecifySeer: abyssbringer, SpecifyTarget: abyssbringer);
        }
    }

    public override void ReceiveRPC(MessageReader reader, PlayerControl pc)
    {
        switch (reader.ReadPackedInt32())
        {
            case 1:
                var pos = reader.ReadVector2();
                var roomName = reader.ReadString();
                BlackHoles.Add(new(new(pos, _state.PlayerId), Utils.TimeStamp, pos, roomName, 0));
                break;
            case 2:
                var blackHole = BlackHoles[reader.ReadPackedInt32()];
                blackHole.PlayersConsumed++;
                break;
            case 3:
                BlackHoles.RemoveAt(reader.ReadPackedInt32());
                break;
        }
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl target = null, bool isMeeting = false, bool isForHud = false)
    {
        if (seer.PlayerId != target.PlayerId || seer.PlayerId != _state.PlayerId || (seer.IsModded() && !isForHud) || isMeeting || BlackHoles.Count == 0) return string.Empty;
        return string.Format(Translator.GetString("Abyssbringer.Suffix"), BlackHoles.Count, string.Join('\n', BlackHoles.Select(x => GetBlackHoleFormatText(x.RoomName, x.PlayersConsumed))));

        static string GetBlackHoleFormatText(string roomName, int playersConsumed)
        {
            var rn = roomName == string.Empty ? Translator.GetString("Outside") : roomName;
            return string.Format(Translator.GetString("Abyssbringer.Suffix.BlackHole"), rn, playersConsumed);
        }
    }

    enum DespawnMode
    {
        None,
        AfterTime,
        After1PlayerEaten,
        AfterMeeting
    }

    class BlackHoleData(BlackHole NetObject, long PlaceTimeStamp, Vector2 Position, string RoomName, int PlayersConsumed)
    {
        public BlackHole NetObject { get; } = NetObject;
        public long PlaceTimeStamp { get; } = PlaceTimeStamp;
        public Vector2 Position { get; set; } = Position;
        public string RoomName { get; } = RoomName;
        public int PlayersConsumed { get; set; } = PlayersConsumed;
    }
}