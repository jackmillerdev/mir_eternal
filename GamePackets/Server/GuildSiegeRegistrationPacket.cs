﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 663, 长度 = 6, 注释 = "GuildSiegeRegistrationPacket")]
	public sealed class GuildSiegeRegistrationPacket : GamePacket
	{
		
		public GuildSiegeRegistrationPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 行会编号;
	}
}
