﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 314, 长度 = 0, 注释 = "AcceptWantedListPacket")]
	public sealed class AcceptWantedListPacket : GamePacket
	{
		
		public AcceptWantedListPacket()
		{
			
			
		}
	}
}
