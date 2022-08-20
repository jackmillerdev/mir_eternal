﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(Source = PacketSource.Server, Id = 22, Length = 338, Description = "SyncReputationListPacket(不同步会导致怪物不能ActiveAttack)")]
	public sealed class SyncReputationListPacket : GamePacket
	{
		
		public SyncReputationListPacket()
		{
			
			this.字节数据 = new byte[]
			{
				196,
				79,
				128,
				80,
				151,
				0,
				0,
				0,
				0,
				0,
				0,
				64,
				18,
				0,
				0,
				0,
				0,
				0,
				0,
				1,
				0,
				0,
				0,
				1,
				1,
				0,
				0,
				0,
				2,
				240,
				216,
				0,
				0,
				3,
				1,
				0,
				0,
				0,
				4,
				1,
				0,
				0,
				0,
				5,
				1,
				0,
				234,
				91,
				6,
				240,
				216,
				0,
				0,
				7,
				240,
				216,
				0,
				0,
				8,
				1,
				0,
				122,
				176,
				9,
				240,
				216,
				0,
				20,
				10,
				240,
				216,
				7,
				0,
				11,
				1,
				0,
				0,
				108,
				12,
				240,
				216,
				0,
				148,
				13,
				240,
				216,
				16,
				48,
				14,
				1,
				0,
				0,
				0,
				15,
				240,
				216,
				0,
				0,
				16,
				1,
				0,
				0,
				0,
				17,
				1,
				0,
				0,
				0,
				18,
				1,
				0,
				0,
				0,
				19,
				240,
				216,
				71,
				97,
				20,
				240,
				216,
				0,
				0,
				21,
				240,
				216,
				0,
				0,
				22,
				240,
				216,
				86,
				60,
				23,
				2,
				0,
				1,
				0,
				24,
				240,
				216,
				0,
				12,
				25,
				1,
				0,
				0,
				0,
				26,
				1,
				0,
				0,
				0,
				27,
				240,
				216,
				0,
				0,
				28,
				1,
				0,
				0,
				0,
				29,
				240,
				216,
				0,
				136,
				30,
				251,
				0,
				224,
				125,
				31,
				240,
				216,
				39,
				143,
				32,
				232,
				216,
				207,
				154,
				33,
				245,
				1,
				153,
				11,
				34,
				228,
				216,
				77,
				241,
				35,
				240,
				216,
				46,
				203,
				36,
				245,
				1,
				9,
				104,
				37,
				240,
				216,
				94,
				147,
				38,
				240,
				216,
				125,
				208,
				39,
				198,
				2,
				150,
				145,
				40,
				1,
				0,
				158,
				161,
				41,
				1,
				0,
				113,
				110,
				42,
				1,
				0,
				11,
				2,
				43,
				1,
				0,
				73,
				201,
				44,
				1,
				0,
				159,
				160,
				45,
				240,
				216,
				1,
				62,
				46,
				240,
				216,
				42,
				13,
				47,
				240,
				216,
				226,
				180,
				48,
				1,
				0,
				226,
				143,
				49,
				240,
				216,
				223,
				144,
				50,
				240,
				216,
				146,
				99,
				51,
				1,
				0,
				149,
				190,
				52,
				240,
				216,
				144,
				144,
				53,
				1,
				0,
				83,
				7,
				54,
				240,
				216,
				165,
				52,
				55,
				240,
				216,
				213,
				155,
				56,
				240,
				216,
				153,
				176,
				57,
				1,
				0,
				99,
				165,
				58,
				1,
				0,
				205,
				158,
				59,
				240,
				216,
				12,
				171,
				60,
				240,
				216,
				244,
				84,
				61,
				240,
				216,
				143,
				156,
				62,
				240,
				216,
				144,
				129,
				63,
				1,
				0
			};
			
		}

		
		[WrappingFieldAttribute(SubScript = 2, Length = 336)]
		public byte[] 字节数据;
	}
}
