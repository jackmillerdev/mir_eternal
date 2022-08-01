﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 572, 长度 = 6, 注释 = "ApprenticeSuccessfullyPacket")]
	public sealed class ApprenticeSuccessfullyPacket : GamePacket
	{
		
		public ApprenticeSuccessfullyPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;
	}
}
