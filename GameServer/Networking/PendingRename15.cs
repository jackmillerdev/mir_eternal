﻿using System;
using System.Drawing;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 18, 长度 = 6, 注释 = "角色走动")]
	public sealed class 客户角色走动 : GamePacket
	{
		
		public 客户角色走动()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public Point 坐标;
	}
}
