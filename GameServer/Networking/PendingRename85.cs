﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 567, 长度 = 6, 注释 = "收徒成功提示")]
	public sealed class 收徒成功提示 : GamePacket
	{
		
		public 收徒成功提示()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 对象编号;
	}
}
