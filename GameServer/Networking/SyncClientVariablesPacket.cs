﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 186, 长度 = 514, 注释 = "SyncClientVariablesPacket(物品快捷键)")]
	public sealed class SyncClientVariablesPacket : GamePacket
	{
		
		public SyncClientVariablesPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 512)]
		public byte[] 字节数据;
	}
}
