﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 79, 长度 = 14, 注释 = "升级武器普通")]
	public sealed class 升级武器普通 : GamePacket
	{
		
		public 升级武器普通()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 6)]
		public byte[] 首饰组;

		
		[WrappingFieldAttribute(SubScript = 8, Length = 6)]
		public byte[] 材料组;
	}
}
