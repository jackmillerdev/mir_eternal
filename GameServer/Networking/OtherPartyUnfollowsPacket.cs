﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 535, 长度 = 38, 注释 = "OtherPartyUnfollowsPacket")]
	public sealed class OtherPartyUnfollowsPacket : GamePacket
	{
		
		public OtherPartyUnfollowsPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(下标 = 6, 长度 = 32)]
		public string 对象名字;
	}
}
