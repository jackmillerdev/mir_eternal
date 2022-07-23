﻿using System;
using GameServer.Data;
using GameServer.Networking;

namespace GameServer
{
	
	public sealed class AddCoins : GMCommand
	{
		
		// (get) Token: 0x0600005A RID: 90 RVA: 0x00002865 File Offset: 0x00000A65
		public override ExecutionWay ExecutionWay
		{
			get
			{
				return ExecutionWay.优先后台执行;
			}
		}

		
		public override void Execute()
		{
			GameData GameData;
			if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(this.角色名字, out GameData))
			{
				CharacterData CharacterData = GameData as CharacterData;
				if (CharacterData != null)
				{
					CharacterData.金币数量 += this.金币数量;
					客户网络 网络连接 = CharacterData.网络连接;
					if (网络连接 != null)
					{
						网络连接.发送封包(new 货币数量变动
						{
							货币类型 = 1,
							货币数量 = CharacterData.金币数量
						});
					}
					MainForm.添加命令日志(string.Format("<= @{0} command has been executed, current coin count: {1}", base.GetType().Name, CharacterData.金币数量));
					return;
				}
			}
			MainForm.添加命令日志("<= @" + base.GetType().Name + " Command execution failed, role does not exist");
		}

		
		public AddCoins()
		{
			
			
		}

		
		[FieldAttribute(0, 排序 = 0)]
		public string 角色名字;

		
		[FieldAttribute(0, 排序 = 1)]
		public int 金币数量;
	}
}
