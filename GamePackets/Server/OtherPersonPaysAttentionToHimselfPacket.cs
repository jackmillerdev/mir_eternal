﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 534, 长度 = 38, 注释 = "OtherPersonPaysAttentionToHimselfPacket")]
	public sealed class OtherPersonPaysAttentionToHimselfPacket : GamePacket
	{
		
		public OtherPersonPaysAttentionToHimselfPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 32)]
		public string 对象名字;
	}
}
