﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.服务器, 编号 = 65, 长度 = 16, 注释 = "同步Npcc数据")]
	public sealed class 同步Npcc数据 : GamePacket
	{
		
		public 同步Npcc数据()
		{
			
			this.对象质量 = 3;
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 2)]
		public ushort 对象模板;

		
		[WrappingFieldAttribute(SubScript = 10, Length = 1)]
		public byte 对象质量;

		
		[WrappingFieldAttribute(SubScript = 11, Length = 1)]
		public byte 对象等级;

		
		[WrappingFieldAttribute(SubScript = 12, Length = 4)]
		public int 体力上限;
	}
}
