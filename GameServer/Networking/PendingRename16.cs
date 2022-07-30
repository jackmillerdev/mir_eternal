﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 1002, 长度 = 40, 注释 = "创建角色")]
	public sealed class 客户创建角色 : GamePacket
	{
		
		public 客户创建角色()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 32)]
		public string 名字;

		
		[WrappingFieldAttribute(SubScript = 34, Length = 1)]
		public byte 性别;

		
		[WrappingFieldAttribute(SubScript = 35, Length = 1)]
		public byte 职业;

		
		[WrappingFieldAttribute(SubScript = 36, Length = 1)]
		public byte 发型;

		
		[WrappingFieldAttribute(SubScript = 37, Length = 1)]
		public byte 发色;

		
		[WrappingFieldAttribute(SubScript = 38, Length = 1)]
		public byte 脸型;
	}
}
