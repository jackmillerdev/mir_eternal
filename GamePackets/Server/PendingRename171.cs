﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(Source = PacketSource.Server, Id = 522, Length = 51, Description = "同步队员信息")]
	public sealed class 同步队员信息 : GamePacket
	{
		
		public 同步队员信息()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 队伍编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(SubScript = 10, Length = 4)]
		public int 对象等级;

		
		[WrappingFieldAttribute(SubScript = 14, Length = 4)]
		public int MaxHP;

		
		[WrappingFieldAttribute(SubScript = 18, Length = 4)]
		public int MaxMP;

		
		[WrappingFieldAttribute(SubScript = 22, Length = 4)]
		public int CurrentHP;

		
		[WrappingFieldAttribute(SubScript = 26, Length = 4)]
		public int CurrentMP;

		
		[WrappingFieldAttribute(SubScript = 30, Length = 4)]
		public int CurrentMap;

		
		[WrappingFieldAttribute(SubScript = 34, Length = 4)]
		public int 当前线路;

		
		[WrappingFieldAttribute(SubScript = 38, Length = 4)]
		public int 横向坐标;

		
		[WrappingFieldAttribute(SubScript = 42, Length = 4)]
		public int 纵向坐标;

		
		[WrappingFieldAttribute(SubScript = 46, Length = 4)]
		public int 坐标高度;

		
		[WrappingFieldAttribute(SubScript = 50, Length = 4)]
		public byte AttackMode;
	}
}
