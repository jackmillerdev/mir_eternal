﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(Source = PacketSource.Server, Id = 518, Length = 45, Description = "AddMembersToTeamPacket")]
	public sealed class AddMembersToTeamPacket : GamePacket
	{
		
		public AddMembersToTeamPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 4)]
		public int 队伍编号;

		
		[WrappingFieldAttribute(SubScript = 6, Length = 4)]
		public int 对象编号;

		
		[WrappingFieldAttribute(SubScript = 10, Length = 32)]
		public string 对象名字;

		
		[WrappingFieldAttribute(SubScript = 42, Length = 1)]
		public byte 对象性别;

		
		[WrappingFieldAttribute(SubScript = 43, Length = 1)]
		public byte 对象职业;

		
		[WrappingFieldAttribute(SubScript = 44, Length = 1)]
		public byte 在线离线;
	}
}
