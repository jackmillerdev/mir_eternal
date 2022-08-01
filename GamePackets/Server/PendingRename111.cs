﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 56, 长度 = 7, 注释 = "角色复活")]
	public sealed class 玩家角色复活 : GamePacket
	{
		
		public 玩家角色复活()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 1)]
		public byte 复活方式;
	}
}
