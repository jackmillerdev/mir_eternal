﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 55, 长度 = 7, 注释 = "对象死亡")]
	public sealed class ObjectCharacterDiesPacket : GamePacket
	{
		
		public ObjectCharacterDiesPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;
	}
}
