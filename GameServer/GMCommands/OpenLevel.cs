﻿using System;
using System.Windows.Forms;
using GameServer.Properties;

namespace GameServer
{
	
	public sealed class OpenLevel : GMCommand
	{
		
		// (get) Token: 0x06000046 RID: 70 RVA: 0x00002940 File Offset: 0x00000B40
		public override ExecutionWay ExecutionWay
		{
			get
			{
				return ExecutionWay.只能后台执行;
			}
		}

		
		public override void Execute()
		{
			if (this.最高等级 <= CustomClass.游戏OpenLevelCommand)
			{
				MainForm.添加命令日志("<= @" + base.GetType().Name + " Command execution failed, the level is lower than the current OpenLevelCommand");
				return;
			}
			Settings.Default.游戏OpenLevelCommand = (CustomClass.游戏OpenLevelCommand = this.最高等级);
			Settings.Default.Save();
			MainForm.Singleton.BeginInvoke(new MethodInvoker(delegate()
			{
				MainForm.Singleton.S_游戏OpenLevelCommand.Value = this.最高等级;
			}));
			MainForm.添加命令日志(string.Format("<= @{0} The command has been executed, the current OpenLevelCommand: {1}", base.GetType().Name, CustomClass.游戏OpenLevelCommand));
		}

		
		public OpenLevel()
		{
			
			
		}

		
		[FieldAttribute(0, 排序 = 0)]
		public byte 最高等级;
	}
}
