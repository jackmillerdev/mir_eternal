﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 566, 长度 = 6, 注释 = "ExpelMembersPacket")]
	public sealed class ExpelMembersPacket : GamePacket
	{
		
		public ExpelMembersPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 对象编号;
	}
}
