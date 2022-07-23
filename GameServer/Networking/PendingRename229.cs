﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 1007, 长度 = 6, 注释 = "帧同步, 请求Ping")]
	public sealed class 测试网关网速 : GamePacket
	{
		
		public 测试网关网速()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 客户时间;
	}
}
