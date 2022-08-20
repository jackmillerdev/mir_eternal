﻿using System;
using System.Drawing;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(Source = PacketSource.Server, Id = 39, Length = 17, Description = "进入场景(包括商店/随机卷)")]
	public sealed class 玩家进入场景 : GamePacket
	{
		
		public 玩家进入场景()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int MapId;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 4)]
		public int 路线编号;

		
		[WrappingFieldAttribute(SubScript = 10, Length = 1)]
		public byte RouteStatus;

		
		[WrappingFieldAttribute(SubScript = 11, Length = 4)]
		public Point CurrentCoords;

		
		[WrappingFieldAttribute(SubScript = 15, Length = 2)]
		public ushort 当前高度;
	}
}
