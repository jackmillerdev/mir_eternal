﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 1006, 长度 = 6, 注释 = "进入游戏")]
	public sealed class 客户进入游戏 : GamePacket
	{
		
		public 客户进入游戏()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 角色编号;
	}
}
