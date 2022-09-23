﻿using GamePackets.Server;
using GameServer.Data;
using GameServer.Networking;
using GameServer.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.PlayerCommands
{
    public class PlayerCommandSS : PlayerCommand
    {
        [Field(Position = 0)]
        public int Step;

        public override void Execute()
        {
            switch (Step)
            {
                case 0: // talent system 
                    SendPacket(359, 70, new byte[] { 
                        8, 0, 0, 0, // player id
                        5, 0, 0, 0, // Talents count

                        // Stamina (unlock at level 30)
                        1, 0, 0, 0, // Level
                        0, 0, 0, 0, // Exp
                        255, 255, 255, 255, // Max exp??

                        // Physical Defense (unlock at level 35)
                        1, 0, 0, 0, // Level
                        0, 0, 0, 0, // Exp
                        255, 255, 255, 255, // Max exp??
                        
                        // Magic Defense (unlock at level 40)
                        1, 0, 0, 0, // Level
                        0, 0, 0, 0, // Exp
                        255, 255, 255, 255, // Max exp??

                        // Defense Breaking (unlock at level 45)
                        1, 0, 0, 0, // Level
                        0, 0, 0, 0, // Exp
                        255, 255, 255, 255, 

                        // Attack (unlock at level 50)
                        1, 0, 0, 0, // Talent attack 
                        0, 0, 0, 0, // Talent attack exp
                        255, 255, 255, 255, // Max exp??
                    });
                    break;
                case 1:
                    SendPacket(187, 13, new byte[] { 1, 112, 0, 6, 0, 0, 0, 0, 0, 0, 128 });
                    break;
                case 2:
                    SendPacket(187, 13, new byte[] { 6, 0, 0, 6, 0, 0, 0, 50, 0, 0, 0 });
                    break;
                case 3:
                    SendPacket(187, 13, new byte[] { 1, 171, 3, 6, 0, 0, 0, 14, 0, 0, 0 });
                    break;
                case 4:
                    SendPacket(187, 13, new byte[] { 1, 207, 3, 6, 0, 0, 0, 0, 0, 0, 128 });
                    break;
                case 5:
                    SendPacket(187, 13, new byte[] { 1, 50, 0, 8, 0, 0, 0, 32, 14, 0, 0 });
                    break;
                case 6: // dragon system guard
                    SendPacket(323, 0, new byte[] { 1, 8, 0, 232, 174, 176, 229, 189, 149, 49, 0, 0, 0, 0, 0, 0, 232, 174, 176, 229, 189, 149, 50, 0, 0, 0, 0, 0, 0, 232, 174, 176, 229, 189, 149, 51, 0, 0, 0, 0, 0, 0, 232, 174, 176, 229, 189, 149, 52, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 9, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                    break;
                case 7:
                    SendPacket(325, 27, new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
                    break;
                default:
                    break;
            }
        }

        private void SendPacket(ushort type, int length, byte[] data)
        {
            var sb = new StringBuilder();

            for (var i = 0; i < data.Length; i++)
            {
                sb.AppendLine(data[i].ToString());
            }

            var output = sb.ToString();

            var buffer = new byte[2 + (length == 0 ? 2 : 0) + data.Length];

            using var ms = new MemoryStream(buffer);
            using var bw = new BinaryWriter(ms);

            bw.Write((ushort)type);
            if (length == 0) bw.Write((ushort)buffer.Length);
            bw.Write(data);

            for (var i = 4; i < buffer.Length; i++)
                buffer[i] ^= GamePacket.EncryptionKey;

            Player.ActiveConnection.Connection.Client.Send(buffer);
        }
    }
}
