﻿using System;
using System.IO;
using System.Text;

namespace GameServer.Data
{
	
	public sealed class MailData : GameData
	{
		
		public MailData()
		{
			
			
		}

		
		public MailData(CharacterData 作者, string 标题, string 正文, ItemData 附件)
		{
			
			
			this.邮件作者.V = 作者;
			this.邮件标题.V = 标题;
			this.邮件正文.V = 正文;
			this.邮件附件.V = 附件;
			this.未读邮件.V = true;
			this.系统邮件.V = (作者 == null);
			this.CreatedDate.V = MainProcess.CurrentTime;
			GameDataGateway.MailData表.AddData(this, true);
		}

		
		public int 邮件编号
		{
			get
			{
				return this.数据索引.V;
			}
		}

		
		public int 邮件时间
		{
			get
			{
				return ComputingClass.TimeShift(this.CreatedDate.V);
			}
		}

		
		public int 物品数量
		{
			get
			{
				if (this.邮件附件.V == null)
				{
					return 0;
				}
				if (this.邮件附件.V.能否堆叠)
				{
					return this.邮件附件.V.当前持久.V;
				}
				return 1;
			}
		}

		
		public override string ToString()
		{
			DataMonitor<string> DataMonitor = this.邮件标题;
			if (DataMonitor == null)
			{
				return null;
			}
			return DataMonitor.V;
		}

		
		public override void Delete()
		{
			ItemData v = this.邮件附件.V;
			if (v != null)
			{
				v.Delete();
			}
			base.Delete();
		}

		
		public byte[] 邮件检索描述()
		{
			byte[] result;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(this.邮件编号);
					binaryWriter.Write(0);
					binaryWriter.Write(this.邮件时间);
					binaryWriter.Write(this.系统邮件.V);
					binaryWriter.Write(this.未读邮件.V);
					binaryWriter.Write(this.邮件附件.V != null);
					byte[] array = new byte[32];
					if (!this.系统邮件.V)
					{
						Encoding.UTF8.GetBytes(this.邮件作者.V.CharName.V + "\0").CopyTo(array, 0);
					}
					binaryWriter.Write(array);
					byte[] array2 = new byte[61];
					Encoding.UTF8.GetBytes(this.邮件标题.V + "\0").CopyTo(array2, 0);
					binaryWriter.Write(array2);
					result = memoryStream.ToArray();
				}
			}
			return result;
		}

		
		public byte[] 邮件内容描述()
		{
			byte[] result;
			using (MemoryStream memoryStream = new MemoryStream(new byte[672]))
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(0);
					binaryWriter.Write(邮件时间);
					binaryWriter.Write(系统邮件.V);
					binaryWriter.Write(邮件附件.V?.Id ?? -1);
					binaryWriter.Write(物品数量);
					byte[] array = new byte[32];
					if (!this.系统邮件.V)
						Encoding.UTF8.GetBytes(邮件作者.V.CharName.V + "\0").CopyTo(array, 0);
					binaryWriter.Write(array);
					byte[] array2 = new byte[61];
					Encoding.UTF8.GetBytes(this.邮件标题.V + "\0").CopyTo(array2, 0);
					binaryWriter.Write(array2);
					byte[] array3 = new byte[554];
					Encoding.UTF8.GetBytes(this.邮件正文.V + "\0").CopyTo(array3, 0);
					binaryWriter.Write(array3);
					binaryWriter.Write(邮件编号);
					binaryWriter.Write(0);
					result = memoryStream.ToArray();
				}
			}
			return result;
		}

		
		public readonly DataMonitor<bool> 系统邮件;

		
		public readonly DataMonitor<bool> 未读邮件;

		
		public readonly DataMonitor<string> 邮件标题;

		
		public readonly DataMonitor<string> 邮件正文;

		
		public readonly DataMonitor<DateTime> CreatedDate;

		
		public readonly DataMonitor<ItemData> 邮件附件;

		
		public readonly DataMonitor<CharacterData> 邮件作者;

		
		public readonly DataMonitor<CharacterData> 收件地址;
	}
}
