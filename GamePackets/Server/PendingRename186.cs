﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 601, 长度 = 3, 注释 = "脱离行会应答")]
	public sealed class 脱离行会应答 : GamePacket
	{
		
		public 脱离行会应答()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public byte 脱离方式;
	}
}
