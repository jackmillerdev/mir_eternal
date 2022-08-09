﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameServer.Data
{
	
	public static class DataLinkTable
	{
		
		public static void 添加任务(GameData 数据, DataField 字段, object 监视器, Type Data型, int 数据索引)
		{
			DataLinkTable.数据任务表.Enqueue(new DataLinkTable.数据关联参数(数据, 字段, 监视器, Data型, 数据索引));
		}

		
		public static void 添加任务(GameData 数据, DataField 字段, IList 内部列表, Type Data型, int 数据索引)
		{
			DataLinkTable.列表任务表.Enqueue(new DataLinkTable.列表关联参数(数据, 字段, 内部列表, Data型, 数据索引));
		}

		
		public static void 添加任务<T>(GameData 数据, DataField 字段, ISet<T> 内部列表, int 数据索引)
		{
			ISet<PetData> set = 内部列表 as ISet<PetData>;
			if (set != null)
			{
				DataLinkTable.哈希宠物表.Enqueue(new DataLinkTable.哈希关联参数<PetData>(数据, 字段, set, 数据索引));
				return;
			}
			ISet<CharacterData> set2 = 内部列表 as ISet<CharacterData>;
			if (set2 != null)
			{
				DataLinkTable.哈希角色表.Enqueue(new DataLinkTable.哈希关联参数<CharacterData>(数据, 字段, set2, 数据索引));
				return;
			}
			ISet<MailData> set3 = 内部列表 as ISet<MailData>;
			if (set3 != null)
			{
				DataLinkTable.哈希邮件表.Enqueue(new DataLinkTable.哈希关联参数<MailData>(数据, 字段, set3, 数据索引));
				return;
			}
			SEnvir.AddSystemError("Failed to add hash association task");
		}

		
		public static void 添加任务(GameData 数据, DataField 字段, IDictionary 内部字典, object 字典键, object 字典值, Type 键类型, Type 值类型, int 键索引, int 值索引)
		{
			DataLinkTable.字典任务表.Enqueue(new DataLinkTable.字典关联参数(数据, 字段, 内部字典, 字典键, 字典值, 键类型, 值类型, 键索引, 值索引));
		}

		
		public static void 处理任务()
		{
			int num = 0;
			Dictionary<Type, Dictionary<string, int>> dictionary = new Dictionary<Type, Dictionary<string, int>>();
			SEnvir.AddSystemLog("Start processing data association tasks......");
			while (!DataLinkTable.数据任务表.IsEmpty)
			{
				DataLinkTable.数据关联参数 数据关联参数;
				if (DataLinkTable.数据任务表.TryDequeue(out 数据关联参数) && 数据关联参数.数据索引 != 0)
				{
					GameData GameData = GameDataGateway.Data型表[数据关联参数.Data型][数据关联参数.数据索引];
					if (GameData == null)
					{
						if (!dictionary.ContainsKey(数据关联参数.数据.Data型))
						{
							dictionary[数据关联参数.数据.Data型] = new Dictionary<string, int>();
						}
						if (!dictionary[数据关联参数.数据.Data型].ContainsKey(数据关联参数.字段.字段名字))
						{
							dictionary[数据关联参数.数据.Data型][数据关联参数.字段.字段名字] = 0;
						}
						Dictionary<string, int> dictionary2 = dictionary[数据关联参数.数据.Data型];
						string 字段名字 = 数据关联参数.字段.字段名字;
						int num2 = dictionary2[字段名字];
						dictionary2[字段名字] = num2 + 1;
					}
					else
					{
						数据关联参数.监视器.GetType().GetField("v", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(数据关联参数.监视器, GameData);
						num++;
					}
				}
			}
			while (!DataLinkTable.列表任务表.IsEmpty)
			{
				DataLinkTable.列表关联参数 列表关联参数;
				if (DataLinkTable.列表任务表.TryDequeue(out 列表关联参数) && 列表关联参数.数据索引 != 0)
				{
					GameData GameData2 = GameDataGateway.Data型表[列表关联参数.Data型][列表关联参数.数据索引];
					if (GameData2 == null)
					{
						if (!dictionary.ContainsKey(列表关联参数.数据.Data型))
						{
							dictionary[列表关联参数.数据.Data型] = new Dictionary<string, int>();
						}
						if (!dictionary[列表关联参数.数据.Data型].ContainsKey(列表关联参数.字段.字段名字))
						{
							dictionary[列表关联参数.数据.Data型][列表关联参数.字段.字段名字] = 0;
						}
						Dictionary<string, int> dictionary3 = dictionary[列表关联参数.数据.Data型];
						string 字段名字 = 列表关联参数.字段.字段名字;
						int num2 = dictionary3[字段名字];
						dictionary3[字段名字] = num2 + 1;
					}
					else
					{
						列表关联参数.内部列表.Add(GameData2);
						num++;
					}
				}
			}
			while (!DataLinkTable.字典任务表.IsEmpty)
			{
				DataLinkTable.字典关联参数 字典关联参数;
				if (DataLinkTable.字典任务表.TryDequeue(out 字典关联参数) && (字典关联参数.字典键 != null || 字典关联参数.键索引 != 0) && (字典关联参数.字典值 != null || 字典关联参数.值索引 != 0))
				{
					object obj = 字典关联参数.字典键 ?? GameDataGateway.Data型表[字典关联参数.键类型][字典关联参数.键索引];
					object obj2 = 字典关联参数.字典值 ?? GameDataGateway.Data型表[字典关联参数.值类型][字典关联参数.值索引];
					if (obj != null && obj2 != null)
					{
						字典关联参数.内部字典[obj] = obj2;
						num++;
					}
					else
					{
						if (!dictionary.ContainsKey(字典关联参数.数据.Data型))
						{
							dictionary[字典关联参数.数据.Data型] = new Dictionary<string, int>();
						}
						if (!dictionary[字典关联参数.数据.Data型].ContainsKey(字典关联参数.字段.字段名字))
						{
							dictionary[字典关联参数.数据.Data型][字典关联参数.字段.字段名字] = 0;
						}
						Dictionary<string, int> dictionary4 = dictionary[字典关联参数.数据.Data型];
						string 字段名字 = 字典关联参数.字段.字段名字;
						int num2 = dictionary4[字段名字];
						dictionary4[字段名字] = num2 + 1;
					}
				}
			}
			while (!DataLinkTable.哈希宠物表.IsEmpty)
			{
				DataLinkTable.哈希关联参数<PetData> 哈希关联参数;
				if (DataLinkTable.哈希宠物表.TryDequeue(out 哈希关联参数) && 哈希关联参数.数据索引 != 0)
				{
					PetData PetData = GameDataGateway.Data型表[typeof(PetData)][哈希关联参数.数据索引] as PetData;
					if (PetData == null)
					{
						if (!dictionary.ContainsKey(哈希关联参数.数据.Data型))
						{
							dictionary[哈希关联参数.数据.Data型] = new Dictionary<string, int>();
						}
						if (!dictionary[哈希关联参数.数据.Data型].ContainsKey(哈希关联参数.字段.字段名字))
						{
							dictionary[哈希关联参数.数据.Data型][哈希关联参数.字段.字段名字] = 0;
						}
						Dictionary<string, int> dictionary5 = dictionary[哈希关联参数.数据.Data型];
						string 字段名字 = 哈希关联参数.字段.字段名字;
						int num2 = dictionary5[字段名字];
						dictionary5[字段名字] = num2 + 1;
					}
					else
					{
						哈希关联参数.内部列表.Add(PetData);
						num++;
					}
				}
			}
			while (!DataLinkTable.哈希角色表.IsEmpty)
			{
				DataLinkTable.哈希关联参数<CharacterData> 哈希关联参数2;
				if (DataLinkTable.哈希角色表.TryDequeue(out 哈希关联参数2) && 哈希关联参数2.数据索引 != 0)
				{
					CharacterData CharacterData = GameDataGateway.Data型表[typeof(CharacterData)][哈希关联参数2.数据索引] as CharacterData;
					if (CharacterData == null)
					{
						if (!dictionary.ContainsKey(哈希关联参数2.数据.Data型))
						{
							dictionary[哈希关联参数2.数据.Data型] = new Dictionary<string, int>();
						}
						if (!dictionary[哈希关联参数2.数据.Data型].ContainsKey(哈希关联参数2.字段.字段名字))
						{
							dictionary[哈希关联参数2.数据.Data型][哈希关联参数2.字段.字段名字] = 0;
						}
						Dictionary<string, int> dictionary6 = dictionary[哈希关联参数2.数据.Data型];
						string 字段名字 = 哈希关联参数2.字段.字段名字;
						int num2 = dictionary6[字段名字];
						dictionary6[字段名字] = num2 + 1;
					}
					else
					{
						哈希关联参数2.内部列表.Add(CharacterData);
						num++;
					}
				}
			}
			while (!DataLinkTable.哈希邮件表.IsEmpty)
			{
				DataLinkTable.哈希关联参数<MailData> 哈希关联参数3;
				if (DataLinkTable.哈希邮件表.TryDequeue(out 哈希关联参数3) && 哈希关联参数3.数据索引 != 0)
				{
					MailData MailData = GameDataGateway.Data型表[typeof(MailData)][哈希关联参数3.数据索引] as MailData;
					if (MailData == null)
					{
						if (!dictionary.ContainsKey(哈希关联参数3.数据.Data型))
						{
							dictionary[哈希关联参数3.数据.Data型] = new Dictionary<string, int>();
						}
						if (!dictionary[哈希关联参数3.数据.Data型].ContainsKey(哈希关联参数3.字段.字段名字))
						{
							dictionary[哈希关联参数3.数据.Data型][哈希关联参数3.字段.字段名字] = 0;
						}
						Dictionary<string, int> dictionary7 = dictionary[哈希关联参数3.数据.Data型];
						string 字段名字 = 哈希关联参数3.字段.字段名字;
						int num2 = dictionary7[字段名字];
						dictionary7[字段名字] = num2 + 1;
					}
					else
					{
						哈希关联参数3.内部列表.Add(MailData);
						num++;
					}
				}
			}
			SEnvir.AddSystemLog(string.Format("Data linkage tasks completed, total number of tasks: {0}", num));
			dictionary.Sum((KeyValuePair<Type, Dictionary<string, int>> x) => x.Value.Sum((KeyValuePair<string, int> o) => o.Value));
			foreach (KeyValuePair<Type, Dictionary<string, int>> keyValuePair in dictionary)
			{
				foreach (KeyValuePair<string, int> keyValuePair2 in keyValuePair.Value)
				{
					SEnvir.AddSystemLog(string.Format("Data type:[{0}], Internal field: [{1}], Total [{2}] data association failed", keyValuePair.Key.Name, keyValuePair2.Key, keyValuePair2.Value));
				}
			}
		}

		
		static DataLinkTable()
		{
			
			DataLinkTable.数据任务表 = new ConcurrentQueue<DataLinkTable.数据关联参数>();
			DataLinkTable.列表任务表 = new ConcurrentQueue<DataLinkTable.列表关联参数>();
			DataLinkTable.字典任务表 = new ConcurrentQueue<DataLinkTable.字典关联参数>();
			DataLinkTable.哈希宠物表 = new ConcurrentQueue<DataLinkTable.哈希关联参数<PetData>>();
			DataLinkTable.哈希角色表 = new ConcurrentQueue<DataLinkTable.哈希关联参数<CharacterData>>();
			DataLinkTable.哈希邮件表 = new ConcurrentQueue<DataLinkTable.哈希关联参数<MailData>>();
		}

		
		private static readonly ConcurrentQueue<DataLinkTable.数据关联参数> 数据任务表;

		
		private static readonly ConcurrentQueue<DataLinkTable.列表关联参数> 列表任务表;

		
		private static readonly ConcurrentQueue<DataLinkTable.字典关联参数> 字典任务表;

		
		private static readonly ConcurrentQueue<DataLinkTable.哈希关联参数<PetData>> 哈希宠物表;

		
		private static readonly ConcurrentQueue<DataLinkTable.哈希关联参数<CharacterData>> 哈希角色表;

		
		private static readonly ConcurrentQueue<DataLinkTable.哈希关联参数<MailData>> 哈希邮件表;

		
		private struct 数据关联参数
		{
			
			public 数据关联参数(GameData 数据, DataField 字段, object 监视器, Type Data型, int 数据索引)
			{
				
				this.数据 = 数据;
				this.字段 = 字段;
				this.监视器 = 监视器;
				this.Data型 = Data型;
				this.数据索引 = 数据索引;
			}

			
			public GameData 数据;

			
			public DataField 字段;

			
			public object 监视器;

			
			public Type Data型;

			
			public int 数据索引;
		}

		
		private struct 列表关联参数
		{
			
			public 列表关联参数(GameData 数据, DataField 字段, IList 内部列表, Type Data型, int 数据索引)
			{
				
				this.数据 = 数据;
				this.字段 = 字段;
				this.内部列表 = 内部列表;
				this.Data型 = Data型;
				this.数据索引 = 数据索引;
			}

			
			public GameData 数据;

			
			public DataField 字段;

			
			public IList 内部列表;

			
			public Type Data型;

			
			public int 数据索引;
		}

		
		private struct 哈希关联参数<T>
		{
			
			public 哈希关联参数(GameData 数据, DataField 字段, ISet<T> 内部列表, int 数据索引)
			{
				
				this.数据 = 数据;
				this.字段 = 字段;
				this.内部列表 = 内部列表;
				this.数据索引 = 数据索引;
			}

			
			public GameData 数据;

			
			public DataField 字段;

			
			public ISet<T> 内部列表;

			
			public int 数据索引;
		}

		
		private struct 字典关联参数
		{
			
			public 字典关联参数(GameData 数据, DataField 字段, IDictionary 内部字典, object 字典键, object 字典值, Type 键类型, Type 值类型, int 键索引, int 值索引)
			{
				
				this.数据 = 数据;
				this.字段 = 字段;
				this.内部字典 = 内部字典;
				this.字典键 = 字典键;
				this.字典值 = 字典值;
				this.键类型 = 键类型;
				this.值类型 = 值类型;
				this.键索引 = 键索引;
				this.值索引 = 值索引;
			}

			
			public GameData 数据;

			
			public DataField 字段;

			
			public Type 键类型;

			
			public Type 值类型;

			
			public int 键索引;

			
			public int 值索引;

			
			public object 字典键;

			
			public object 字典值;

			
			public IDictionary 内部字典;
		}
	}
}
