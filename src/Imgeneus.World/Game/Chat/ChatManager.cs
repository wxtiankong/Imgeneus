﻿using System;
using System.Linq;
using Imgeneus.Database.Entities;
using Imgeneus.Network.Data;
using Imgeneus.Network.Packets;
using Imgeneus.World.Game.Player;
using Microsoft.Extensions.Logging;

namespace Imgeneus.World.Game.Chat
{
    public class ChatManager : IChatManager
    {
        private readonly ILogger<IChatManager> _logger;
        private readonly IGameWorld _gameWorld;

        public ChatManager(ILogger<IChatManager> logger, IGameWorld gameWorld)
        {
            _logger = logger;
            _gameWorld = gameWorld;
        }

        public void SendMessage(Character sender, MessageType messageType, string message, string targetName = "")
        {
            switch (messageType)
            {
                case MessageType.Normal:
                    var players = sender.Map.GetPlayers(sender.PosX, sender.PosZ, 50, Fraction.NotSelected, true);
                    foreach (var player in players)
                    {
                        SendNormal((Character)player, sender.Id, message);
                    }
                    break;

                case MessageType.Whisper:
                    var target = _gameWorld.Players.Values.FirstOrDefault(p => p.Name == targetName);
                    if (target != null)
                    {
                        SendWhisper(sender, sender.Name, message);
                        SendWhisper(target, sender.Name, message);
                    }
                    break;

                case MessageType.Party:
                    if (sender.Party != null)
                        foreach (var player in sender.Party.Members.ToList())
                        {
                            SendParty(player, sender.Id, message);
                        }
                    break;

                default:
                    _logger.LogError("Not implemented message type.");
                    break;
            }
        }

        #region Senders

        /// <summary>
        /// Send normal message.
        /// </summary>
        /// <param name="character">To whom we are sending.</param>
        /// <param name="senderId">Message creator id.</param>
        /// <param name="message">Message text.</param>
        private void SendNormal(Character character, int senderId, string message)
        {
            using var packet = new Packet(PacketType.CHAT_NORMAL_ADMIN);
            packet.Write(senderId);
            packet.WriteByte((byte)message.Length);
            packet.Write(message);
            character.Client.SendPacket(packet);
        }

        /// <summary>
        /// Send wisper to someone.
        /// </summary>
        /// <param name="character">To whom we are sending.</param>
        /// <param name="senderName">Message creator name.</param>
        /// <param name="message">Message text.</param>
        private void SendWhisper(Character character, string senderName, string message)
        {
            using var packet = new Packet(PacketType.CHAT_WHISPER_ADMIN);
            packet.Write(false); // false == use sender name, if set to true, sender name will be ignored
            packet.WriteString(senderName, 21);
            packet.WriteByte((byte)message.Length);
            packet.Write(message);
            character.Client.SendPacket(packet);
        }

        /// <summary>
        /// Send message to party members.
        /// </summary>
        /// <param name="character">To whom we are sending.</param>
        /// <param name="senderId">Message creator id.</param>
        /// <param name="message">Message text.</param>
        private void SendParty(Character character, int senderId, string message)
        {
            using var packet = new Packet(PacketType.CHAT_PARTY);
            packet.Write(senderId);
            packet.WriteByte((byte)message.Length);
            packet.Write(message);
            character.Client.SendPacket(packet);
        }

        #endregion
    }
}