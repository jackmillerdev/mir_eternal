﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 24, 长度 = 0, 注释 = "SyncTitleInfoPacket")]
	public sealed class SyncTitleInfoPacket : GamePacket
	{
		
		public SyncTitleInfoPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 4, 长度 = 0)]
		public byte[] 字节描述;
	}
}
