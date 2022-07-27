﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 82, 长度 = 7, 注释 = "玩家名字变灰")]
	public sealed class 玩家名字变灰 : GamePacket
	{
		
		public 玩家名字变灰()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 1)]
		public bool 是否灰名;
	}
}
