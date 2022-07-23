﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 137, 长度 = 0, 注释 = "上传游戏设置")]
	public sealed class 上传游戏设置 : GamePacket
	{
		
		public 上传游戏设置()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 0)]
		public byte[] 字节描述;
	}
}
