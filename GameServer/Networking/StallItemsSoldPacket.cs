﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 163, 长度 = 11, 注释 = "StallItemsSoldPacket")]
	public sealed class StallItemsSoldPacket : GamePacket
	{
		
		public StallItemsSoldPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 1)]
		public byte 物品位置;

		
		[WrappingFieldAttribute(SubScript = 3, Length = 4)]
		public int 售出数量;

		
		[WrappingFieldAttribute(SubScript = 7, Length = 4)]
		public int 售出收益;
	}
}
