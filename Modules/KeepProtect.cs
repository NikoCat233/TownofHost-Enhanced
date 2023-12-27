using HarmonyLib;
using Hazel;
using System.Linq;
using InnerNet;

namespace TOHE.Modules
{
    public static class KeepProtect
    {
        public static long LastFixUpdate = 0;
        public static void SendKeepProtect(this PlayerControl target)
        {
            if (!target.Data.IsDead)
            {
                //Host side
                if (!target.AmOwner)
                    target.ProtectPlayer(target, 18);

                //Client side
                /*
                MessageWriter messageWriter = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)RpcCalls.ProtectPlayer, SendOption.Reliable, -1);
                messageWriter.WriteNetObject(target);
                messageWriter.Write(18);
                AmongUsClient.Instance.FinishRpcImmediately(messageWriter);
                */
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
                    (byte)RpcCalls.ProtectPlayer, SendOption.Reliable, -1);
                writer.WriteNetObject(target);
                writer.Write(PlayerControl.LocalPlayer.CurrentOutfit.ColorId);
                AmongUsClient.Instance.FinishRpcImmediately(writer);

            }
            //Host ignore this rpc so ability cooldown wont get reset
        }
        public static void OnFixedUpdate()
        {
            if (Main.UseVersionProtocol.Value) return;
            if (LastFixUpdate + 24 < Utils.GetTimeStamp())
            {
                LastFixUpdate = Utils.GetTimeStamp();
                Main.AllAlivePlayerControls.ToArray()
                    .Where(x => !x.AmOwner && !x.IsProtected())
                    .Do(x => x.SendKeepProtect());
                PlayerControl.LocalPlayer.SendKeepProtect();
            }
        }

        public static void SendToAll()
        {
            if (Main.UseVersionProtocol.Value) return;
            LastFixUpdate = Utils.GetTimeStamp();
            Main.AllAlivePlayerControls.ToArray().Do(x => x.SendKeepProtect());
        }

    }
}
