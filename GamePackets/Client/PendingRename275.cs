﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Client, 编号 = 563, 长度 = 0, 注释 = "更改行会宣言")]
	public sealed class 更改行会宣言 : GamePacket
	{
		
		public 更改行会宣言()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 0)]
		public byte[] 行会宣言;
	}
}
