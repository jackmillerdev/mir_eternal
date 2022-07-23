﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 179, 长度 = 0, 注释 = "ReceiveChatMessagesNearbyPacket(附近)")]
	public sealed class ReceiveChatMessagesNearbyPacket : GamePacket
	{
		
		public ReceiveChatMessagesNearbyPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 4, 长度 = 0)]
		public byte[] 字节描述;
	}
}
