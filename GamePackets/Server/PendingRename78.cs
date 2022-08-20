﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(Source = PacketSource.Server, Id = 514, Length = 14, Description = "聊天服务器错误提示")]
	public sealed class 社交错误提示 : GamePacket
	{
		
		public 社交错误提示()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 错误编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 4)]
		public int 参数一;

		
		[WrappingFieldAttribute(SubScript = 10, Length = 4)]
		public int 参数二;
	}
}
