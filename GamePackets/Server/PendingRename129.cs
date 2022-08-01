﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Server, 编号 = 255, 长度 = 6, 注释 = "OrdinaryInscriptionRefinementPacket")]
	public sealed class 玩家普通洗练 : GamePacket
	{
		
		public 玩家普通洗练()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 2)]
		public ushort 铭文位一;

		
		[WrappingFieldAttribute(SubScript = 4, Length = 2)]
		public ushort 铭文位二;
	}
}
