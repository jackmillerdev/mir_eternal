﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 53, 长度 = 4, 注释 = "商店修理单件")]
	public sealed class 商店修理单件 : GamePacket
	{
		
		public 商店修理单件()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 1)]
		public byte 背包类型;

		
		[WrappingFieldAttribute(下标 = 3, 长度 = 1)]
		public byte 物品位置;
	}
}
