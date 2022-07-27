﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 1010, 长度 = 6, 注释 = "同步网关ping")]
	public sealed class LoginQueryResponsePacket : GamePacket
	{
		
		public LoginQueryResponsePacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 当前时间;
	}
}
