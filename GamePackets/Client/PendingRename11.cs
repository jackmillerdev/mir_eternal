﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Client, 编号 = 22, 长度 = 6, 注释 = "进入传送门触发")]
	public sealed class 客户进入法阵 : GamePacket
	{
		
		public 客户进入法阵()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int TeleportGateNumber;
	}
}
