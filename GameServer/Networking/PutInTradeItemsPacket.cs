﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 216, 长度 = 0, 注释 = "PutInTradeItemsPacket")]
	public sealed class PutInTradeItemsPacket : GamePacket
	{
		
		public PutInTradeItemsPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 4, 长度 = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(下标 = 8, 长度 = 1)]
		public byte 放入位置;

		
		[WrappingFieldAttribute(下标 = 9, 长度 = 1)]
		public byte 放入物品;

		
		[WrappingFieldAttribute(下标 = 10, 长度 = 0)]
		public byte[] 物品描述;
	}
}
