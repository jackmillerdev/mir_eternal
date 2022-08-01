﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 593, 长度 = 6, 注释 = "GuildRemoveMemberPacket")]
	public sealed class GuildRemoveMemberPacket : GamePacket
	{
		
		public GuildRemoveMemberPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;
	}
}
