﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 589, 长度 = 0, 注释 = "查看申请名单")]
	public sealed class 查看申请名单 : GamePacket
	{
		
		public 查看申请名单()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 4, Length = 0)]
		public byte[] 字节描述;
	}
}
