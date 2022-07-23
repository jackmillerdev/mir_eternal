﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 526, 长度 = 43, 注释 = "查询对象队伍信息回应")]
	public sealed class 查询队伍应答 : GamePacket
	{
		
		public 查询队伍应答()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 4)]
		public int 队伍编号;

		
		[WrappingFieldAttribute(下标 = 6, 长度 = 32)]
		public string 队伍名字;

		
		[WrappingFieldAttribute(下标 = 39, 长度 = 4)]
		public int 队长编号;
	}
}
