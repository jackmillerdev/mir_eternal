﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 568, 长度 = 29, 注释 = "申请行会外交")]
	public sealed class 申请行会外交 : GamePacket
	{
		
		public 申请行会外交()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 1)]
		public byte 外交类型;

		
		[WrappingFieldAttribute(下标 = 3, 长度 = 1)]
		public byte 外交时间;

		
		[WrappingFieldAttribute(下标 = 4, 长度 = 25)]
		public string 行会名字;
	}
}
