﻿using System;

namespace GameServer.Networking
{
	
	[PacketInfoAttribute(来源 = PacketSource.Client, 编号 = 551, 长度 = 14, 注释 = "提取邮件附件")]
	public sealed class 提取邮件附件 : GamePacket
	{
		
		public 提取邮件附件()
		{
			
			
		}

		
		[WrappingFieldAttribute(SubScript = 6, Length = 4)]
		public int 邮件编号;
	}
}
