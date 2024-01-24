using Hazel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TOHE.Roles.Impostor;
using UnityEngine;
using static TOHE.Translator;

namespace TOHE.Modules.ChatManager
{
    public class ChatManager
    {
        public static bool cancel = false;
        private static List<Dictionary<byte, string>> chatHistory = [];
        private static Dictionary<byte, string> LastSystemChatMsg = [];
        private const int maxHistorySize = 20;
        public static List<string> ChatSentBySystem = [];
        public static void ResetHistory()
        {
            chatHistory = [];
            LastSystemChatMsg = [];
        }
        public static void ClearLastSysMsg()
        {
            LastSystemChatMsg.Clear();
        }
        public static void AddSystemChatHistory(byte playerId, string msg)
        {
            LastSystemChatMsg[playerId] = msg;
        }
        public static bool CheckCommond(ref string msg, string command, bool exact = true)
        {
            var comList = command.Split('|');
            foreach (string comm in comList)
            {
                if (exact)
                {
                    if (msg == "/" + comm) return true;
                }
                else
                {
                    if (msg.StartsWith("/" + comm))
                    {
                        msg = msg.Replace("/" + comm, string.Empty);
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool CheckName(ref string msg, string command, bool exact = true)
        {
            var comList = command.Split('|');
            foreach (var com in comList)
            {
                if (exact)
                {
                    if (msg.Contains(com))
                    {
                        return true;
                    }
                }
                else
                {
                    int index = msg.IndexOf(com);
                    if (index != -1)
                    {
                        msg = msg.Remove(index, com.Length);
                        return true;
                    }
                }
            }
            return false;
        }

        public static string getTextHash(string text)
        {
            using SHA256 sha256 = SHA256.Create();

            // get sha-256 hash
            byte[] sha256Bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            string sha256Hash = BitConverter.ToString(sha256Bytes).Replace("-", "").ToLower();

            // pick front 5 and last 4
            return string.Concat(sha256Hash.AsSpan(0, 5), sha256Hash.AsSpan(sha256Hash.Length - 4));
        }

        public static void AddToHostMessage(string text)
        {
            if (text != "")
            {
                ChatSentBySystem.Add(getTextHash(text));
            }
        }
        public static void SendMessage(PlayerControl player, string message)
        {
            int operate = 0; // 1:ID 2:猜测
            string msg = message;
            string playername = player.GetNameWithRole();
            message = message.ToLower().TrimStart().TrimEnd();

            if (GameStates.IsInGame) operate = 3;
            if (CheckCommond(ref msg, "id|guesslist|gl编号|玩家编号|玩家id|id列表|玩家列表|列表|所有id|全部id|編號|玩家編號")) operate = 1;
            else if (CheckCommond(ref msg, "shoot|guess|bet|st|gs|bt|猜|赌|賭|sp|jj|tl|trial|审判|判|审|審判|審|compare|cmp|比较|比較|duel|sw|swap|st|换票|换|換票|換|finish|结束|结束会议|結束|結束會議|reveal|展示", false)) operate = 2;
            else if (ChatSentBySystem.Contains(getTextHash(msg))) operate = 5;

            if ((operate == 1 || Blackmailer.ForBlackmailer.Contains(player.PlayerId)) && player.IsAlive())
            {
                Logger.Info($"包含特殊信息，不记录", "ChatManager");
                message = msg;
                cancel = true;
            }
            else if (operate == 2)
            {
                Logger.Info($"指令{msg}，不记录", "ChatManager");
                message = msg;
                cancel = false;
            }
            else if (operate == 4)
            {
                Logger.Info($"指令{msg}，不记录", "ChatManager");
                message = msg;
                SendPreviousMessagesToAll();
            }
            else if (operate == 5)
            {
                Logger.Info($"system message{msg}，不记录", "ChatManager");
                message = msg;
                cancel = true;
            }
            else if (operate == 3)
            {
                if (GameStates.IsExilling)
                {
                    if (Options.HideExileChat.GetBool())
                    {
                        Logger.Info($"Message sent in exiling screen, spamming the chat", "ChatManager");
                        _ = new LateTask(SendPreviousMessagesToAll, 0.3f, "Spamming the chat");
                    }
                    return;
                }
                if (!player.IsAlive()) return;
                message = msg;
                //Logger.Warn($"Logging msg : {message}","Checking Exile");
                Dictionary<byte, string> newChatEntry = new()
                    {
                        { player.PlayerId, message }
                    };
                chatHistory.Add(newChatEntry);

                if (chatHistory.Count > maxHistorySize)
                {
                    chatHistory.RemoveAt(0);
                }
                cancel = false;
            }
        }

        public static void SendPreviousMessagesToAll()
        {
            if (!AmongUsClient.Instance.AmHost || !GameStates.IsModHost) return;
            //This should never function for non host
            if (GameStates.IsExilling && chatHistory.Count < 20)
            {
                var firstAlivePlayer = Main.AllAlivePlayerControls.OrderBy(x => x.PlayerId).FirstOrDefault();
                if (firstAlivePlayer == null) return;

                var title = "<color=#aaaaff>" + GetString("DefaultSystemMessageTitle") + "</color>";
                var name = firstAlivePlayer?.Data?.PlayerName;
                string spamMsg = GetString("ExileSpamMsg");

                for (int i = 0; i < 20 - chatHistory.Count; i++)
                {
                    int clientId = -1; //sendTo == byte.MaxValue ? -1 : Utils.GetPlayerById(sendTo).GetClientId();
                    //if (clientId == -1)
                    //{
                    firstAlivePlayer.SetName(title);
                    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(firstAlivePlayer, spamMsg);
                    firstAlivePlayer.SetName(name);
                    //}
                    //var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);
                    //writer.StartMessage(clientId);
                    //writer.StartRpc(firstAlivePlayer.NetId, (byte)RpcCalls.SetName)
                    //    .Write(title)
                    //    .EndRpc();
                    //writer.StartRpc(firstAlivePlayer.NetId, (byte)RpcCalls.SendChat)
                    //    .Write(spamMsg)
                    //    .EndRpc();
                    //writer.StartRpc(firstAlivePlayer.NetId, (byte)RpcCalls.SetName)
                    //    .Write(name)
                    //    .EndRpc();
                    //writer.EndMessage();
                    //writer.SendMessage();
                    //DestroyableSingleton<HudManager>.Instance.Chat.AddChat(firstAlivePlayer, spamMsg);
                    //var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);

                    //writer.StartMessage(-1);
                    //writer.StartRpc(firstAlivePlayer.NetId, (byte)RpcCalls.SendChat)
                    //    .Write(spamMsg)
                    //    .EndRpc()
                    //    .EndMessage()
                    //    .SendMessage();
                }
            }
            //var rd = IRandom.Instance;
            //CustomRoles[] roles = (CustomRoles[])Enum.GetValues(typeof(CustomRoles));
            //string[] specialTexts = new string[] { "bet", "bt", "guess", "gs", "shoot", "st", "赌", "猜", "审判", "tl", "判", "审", "trial" };
            //int numPlayers = Main.AllAlivePlayerControls.Count();
            //var allAlivePlayers = Main.AllAlivePlayerControls.ToArray();
            //int roleCount = roles.Length;

            //for (int i = chatHistory.Count; i < 30; i++)
            //{
            //    StringBuilder msgBuilder = new();
            //    msgBuilder.Append('/');
            //    if (rd.Next(1, 100) < 20)
            //    {
            //        msgBuilder.Append("id");
            //    }
            //    else
            //    {
            //        msgBuilder.Append(specialTexts[rd.Next(specialTexts.Length)]);
            //        msgBuilder.Append(rd.Next(1, 100) < 50 ? string.Empty : " ");
            //        msgBuilder.Append(rd.Next(15));
            //        msgBuilder.Append(rd.Next(1, 100) < 50 ? string.Empty : " ");
            //        CustomRoles role = roles[rd.Next(roleCount)];
            //        msgBuilder.Append(rd.Next(1, 100) < 50 ? string.Empty : " ");
            //        msgBuilder.Append(Utils.GetRoleName(role));
            //    }
            //    string msg = msgBuilder.ToString();

            //    var player = allAlivePlayers[rd.Next(numPlayers)];
            //    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(player, msg);
            //    var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);

            //    writer.StartMessage(-1);
            //    writer.StartRpc(player.NetId, (byte)RpcCalls.SendChat)
            //        .Write(msg)
            //        .EndRpc()
            //        .EndMessage()
            //        .SendMessage();
            //}

            for (int i = 0; i < chatHistory.Count; i++)
            {
                var entry = chatHistory[i];
                var senderId = entry.Keys.First();
                var senderMessage = entry[senderId];
                var senderPlayer = Utils.GetPlayerById(senderId);
                if (senderPlayer == null) continue;

                var playerDead = !senderPlayer.IsAlive();
                if (playerDead)
                {
                    senderPlayer.Revive();
                }

                DestroyableSingleton<HudManager>.Instance.Chat.AddChat(senderPlayer, senderMessage);
                //var writer = CustomRpcSender.Create("MessagesToSend", SendOption.None);

                //writer.StartMessage(-1);
                //writer.StartRpc(senderPlayer.NetId, (byte)RpcCalls.SendChat)
                //    .Write(senderMessage)
                //    .EndRpc()
                //    .EndMessage()
                //    .SendMessage();

                if (playerDead)
                {
                    senderPlayer.Die(DeathReason.Kill, true);
                }
            }
            foreach (var playerId in LastSystemChatMsg.Keys.ToArray())
            {
                var pc = Utils.GetPlayerById(playerId);
                if (pc == null && playerId != byte.MaxValue) continue;
                var title = "<color=#FF0000>" + GetString("LastMessageReplay") + "</color>";
                Utils.SendMessage(LastSystemChatMsg[playerId], playerId, title: title, replay: true);
            }
        }
    }
    public class PublicChatManager
    {
        public static List<(string, byte, string)> MessagesToSend = new List<(string, byte, string)>();
        private static float timesincelastsend = 0f;
        private static float sendlimit = 1.2f;
        private static int maxlength = 100;

        public static void AddChat(string msg, byte sendTo, string title)
        {
            // Remove color and size tags while keeping line breaks and spaces
            string cleanedMessage = Regex.Replace(msg, "<[^>]+>", "");
            cleanedMessage = Regex.Replace(cleanedMessage, "\r\n|\r|\n", "  ");
            // Create a new message with cleaned title and message
            string fullMessage = $"{title}\n{cleanedMessage}";

            if (title.ToLower() == "host")
            {
                bool addedToExistingHost = false;
                for (int i = 0; i < MessagesToSend.Count; i++)
                {
                    if (MessagesToSend[i].Item3.ToLower() == "host")
                    {
                        string combinedMsg = $"{MessagesToSend[i].Item1}\n{fullMessage}";

                        if (combinedMsg.Length <= maxlength)
                        {
                            MessagesToSend[i] = (combinedMsg, sendTo, title);
                            addedToExistingHost = true;
                            break;
                        }
                        else
                        {
                            List<string> splitMessages = SplitText(fullMessage, maxlength - MessagesToSend[i].Item1.Length - 1);
                            if (splitMessages.Count > 1)
                            {
                                for (int j = 1; j < splitMessages.Count; j++)
                                {
                                    MessagesToSend.Insert(i + j, (splitMessages[j], sendTo, title));
                                }
                            }

                            MessagesToSend[i] = (splitMessages[0], sendTo, title);
                            addedToExistingHost = true;
                            // Remove the break statement to continue checking for additional "host" messages
                        }
                    }
                }

                if (!addedToExistingHost)
                {
                    MessagesToSend.Insert(0, (fullMessage, sendTo, title));
                }

                return;
            }

            if (fullMessage.Length > maxlength)
            {
                List<string> splitMessages = SplitText(fullMessage, maxlength);
                foreach (var splitMsg in splitMessages)
                {
                    MessagesToSend.Add((splitMsg, sendTo, title));
                }
            }
            else
            {
                bool replaced = false;
                for (int i = 0; i < MessagesToSend.Count; i++)
                {
                    if (MessagesToSend[i].Item2 == sendTo && MessagesToSend[i].Item3.ToLower() != "host")
                    {
                        string combinedMsg = $"{MessagesToSend[i].Item1}\n{fullMessage}";

                        if (combinedMsg.Length <= maxlength)
                        {
                            MessagesToSend[i] = (combinedMsg, sendTo, title);
                            replaced = true;
                            break;
                        }
                    }
                }

                if (!replaced)
                {
                    MessagesToSend.Add((fullMessage, sendTo, title));
                }
            }
        }
        public static void OnFixedUpdate(PlayerControl player)
        {
            if (player.PlayerId != PlayerControl.LocalPlayer.PlayerId) return;
            if (!AmongUsClient.Instance.AmHost || !Main.HostPublic.Value) return;
            timesincelastsend += Time.fixedDeltaTime;
            if (timesincelastsend < sendlimit) return;

            if (MessagesToSend.Count > 0)
            {
                var sender = PlayerControl.LocalPlayer;
                bool revived = false;
                if (sender.Data.IsDead)
                {
                    sender.Data.IsDead = false;
                    AntiBlackout.SendGameData();
                    revived = true;
                }

                (string msg, byte sendTo, string title) = MessagesToSend[0];
                MessagesToSend.RemoveAt(0);
                msg = Regex.Replace(msg, "<[^>]+>", "");
                msg = Regex.Replace(msg, "\r\n|\r|\n", "  ");

                title = sender.Data.PlayerName;
                title = title.ToLower() == "host" ? sender.Data.PlayerName : $"<color=#aaaaff>{GetString("DefaultSystemMessageTitle")}</color>";

                int clientId = sendTo == byte.MaxValue ? -1 : Utils.GetPlayerById(sendTo)?.GetClientId() ?? -1;
                var name = sender.Data.PlayerName;

                if (clientId == -1)
                {
                    sender.SetName(title);
                    DestroyableSingleton<HudManager>.Instance.Chat.AddChat(sender, msg);
                    sender.SetName(name);
                }

                var writer = CustomRpcSender.Create("PublicMessagesToSend", SendOption.None);
                writer.StartMessage(clientId);
                writer.StartRpc(sender.NetId, (byte)RpcCalls.SetName)
                    .Write(title)
                    .EndRpc();
                writer.StartRpc(sender.NetId, (byte)RpcCalls.SendChat)
                    .Write(msg)
                    .EndRpc();
                Logger.Info(msg, null);
                writer.StartRpc(sender.NetId, (byte)RpcCalls.SetName)
                    .Write(sender.Data.PlayerName)
                    .EndRpc();
                writer.EndMessage();
                writer.SendMessage();

                if (revived)
                {
                    sender.Data.IsDead = true;
                    AntiBlackout.SendGameData();
                }
                timesincelastsend = 0;
            }
        }
        public static List<string> SplitText(string text, int maxLength)
        {
            List<string> splitText = [];

            for (int i = 0; i < text.Length; i += maxLength)
            {
                int length = Math.Min(maxLength, text.Length - i);
                splitText.Add(text.Substring(i, length));
            }

            return splitText;
        }
    }

}