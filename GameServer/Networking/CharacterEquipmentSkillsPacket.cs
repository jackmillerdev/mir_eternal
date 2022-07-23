﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.客户端, 编号 = 39, 长度 = 5, 注释 = "装备技能")]
	public sealed class CharacterEquipmentSkillsPacket : GamePacket
	{
		
		public CharacterEquipmentSkillsPacket()
		{
			
			
		}

		
		[WrappingFieldAttribute(下标 = 2, 长度 = 1)]
		public byte 技能栏位;

		
		[WrappingFieldAttribute(下标 = 3, 长度 = 2)]
		public ushort 技能编号;
	}
}
