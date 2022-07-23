﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 551, 长度 = 6, 注释 = "同意拜师申请")]
	public sealed class 拜师申请通过 : GamePacket
	{
		
		public 拜师申请通过()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 对象编号;
	}
}
