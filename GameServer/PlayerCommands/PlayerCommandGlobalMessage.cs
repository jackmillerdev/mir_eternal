using GameServer.Maps;
using GameServer.Networking;
using GameServer.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.PlayerCommands
{
    public class PlayerCommandGlobalMessage : PlayerCommand
    {
                [Field(Position = 0)]
        public string GlobalMessageText;
        public override void Execute()
        {
            NetworkServiceGateway.SendAnnouncement($"{Player.ObjectName}: {GlobalMessageText}");
            Player.SendMessage("Message sent to all players:");
        }
    }
}
