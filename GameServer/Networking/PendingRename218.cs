﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 570, 长度 = 7, 注释 = "CongratsToApprenticeForUpgradePacket")]
	public sealed class 恭喜徒弟升级 : GamePacket
	{
		
		public 恭喜徒弟升级()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 徒弟编号;

		
		[WrappingFieldAttribute(下标 = 6, 长度 = 1)]
		public int 祝贺等级;
	}
}
