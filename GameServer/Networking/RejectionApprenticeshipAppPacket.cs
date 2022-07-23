﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 540, 长度 = 6, 注释 = "RejectionApprenticeshipAppPacket")]
	public sealed class RejectionApprenticeshipAppPacket : GamePacket
	{
		
		public RejectionApprenticeshipAppPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 对象编号;
	}
}
