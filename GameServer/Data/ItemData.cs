﻿using System;
using System.Collections.Generic;
using System.IO;
using GameServer.Templates;

namespace GameServer.Data
{
	
	public class ItemData : GameData
	{
		
		// (get) Token: 0x06000569 RID: 1385 RVA: 0x00005180 File Offset: 0x00003380
		public GameItems 物品模板
		{
			get
			{
				return this.对应模板.V;
			}
		}

		
		// (get) Token: 0x0600056A RID: 1386 RVA: 0x0000518D File Offset: 0x0000338D
		public ItemsForSale 出售类型
		{
			get
			{
				return this.物品模板.StoreType;
			}
		}

		
		// (get) Token: 0x0600056B RID: 1387 RVA: 0x0000519A File Offset: 0x0000339A
		public ItemType 物品类型
		{
			get
			{
				return this.物品模板.Type;
			}
		}

		
		// (get) Token: 0x0600056C RID: 1388 RVA: 0x000051A7 File Offset: 0x000033A7
		public PersistentItemType PersistType
		{
			get
			{
				return this.物品模板.PersistType;
			}
		}

		
		// (get) Token: 0x0600056D RID: 1389 RVA: 0x000051B4 File Offset: 0x000033B4
		public GameObjectRace NeedRace
		{
			get
			{
				return this.物品模板.NeedRace;
			}
		}

		
		// (get) Token: 0x0600056E RID: 1390 RVA: 0x000051C1 File Offset: 0x000033C1
		public GameObjectGender NeedGender
		{
			get
			{
				return this.物品模板.NeedGender;
			}
		}

		
		// (get) Token: 0x0600056F RID: 1391 RVA: 0x000051CE File Offset: 0x000033CE
		public string Name
		{
			get
			{
				return this.物品模板.Name;
			}
		}

		
		// (get) Token: 0x06000570 RID: 1392 RVA: 0x000051DB File Offset: 0x000033DB
		public int NeedLevel
		{
			get
			{
				return this.物品模板.NeedLevel;
			}
		}

		
		// (get) Token: 0x06000571 RID: 1393 RVA: 0x000051E8 File Offset: 0x000033E8
		public int Id
		{
			get
			{
				return this.对应模板.V.Id;
			}
		}

		
		// (get) Token: 0x06000572 RID: 1394 RVA: 0x000051FA File Offset: 0x000033FA
		public int Weight
		{
			get
			{
				if (this.PersistType != PersistentItemType.堆叠)
				{
					return this.物品模板.Weight;
				}
				return this.当前持久.V * this.物品模板.Weight;
			}
		}

		
		// (get) Token: 0x06000573 RID: 1395 RVA: 0x00025024 File Offset: 0x00023224
		public int SalePrice
		{
			get
			{
				switch (对应模板.V.PersistType)
				{
					default:
						return 0;
					case PersistentItemType.无:
						return 1;
					case PersistentItemType.装备:
						{
							EquipmentData EquipmentData2 = this as EquipmentData;
							EquipmentItem obj = 对应模板.V as EquipmentItem;
							int v3 = EquipmentData2.当前持久.V;
							int num2 = obj.ItemLast * 1000;
							int num3 = obj.SalePrice;
							int num4 = Math.Max((sbyte)0, EquipmentData2.幸运等级.V);
							int num5 = EquipmentData2.升级Attack.V * 100 + EquipmentData2.升级Magic.V * 100 + EquipmentData2.升级Taoism.V * 100 + EquipmentData2.升级Needle.V * 100 + EquipmentData2.升级Archery.V * 100;
							int num6 = 0;
							foreach (InscriptionSkill value in EquipmentData2.铭文技能.Values)
							{
								if (value != null)
								{
									num6++;
								}
							}
							int num7 = 0;
							foreach (RandomStats item in EquipmentData2.随机Stat)
							{
								num7 += item.CombatBonus * 100;
							}
							int num8 = 0;
							using (IEnumerator<GameItems> enumerator3 = EquipmentData2.镶嵌灵石.Values.GetEnumerator())
							{
								while (enumerator3.MoveNext())
								{
									switch (enumerator3.Current.Name)
									{
										case "驭朱灵石8级":
										case "精绿灵石8级":
										case "韧紫灵石8级":
										case "抵御幻彩灵石8级":
										case "进击幻彩灵石8级":
										case "盈绿灵石8级":
										case "狂热幻彩灵石8级":
										case "透蓝灵石8级":
										case "守阳灵石8级":
										case "新阳灵石8级":
										case "命朱灵石8级":
										case "蔚蓝灵石8级":
										case "赤褐灵石8级":
										case "橙黄灵石8级":
										case "纯紫灵石8级":
										case "深灰灵石8级":
											num8 += 8000;
											break;
										case "精绿灵石5级":
										case "新阳灵石5级":
										case "命朱灵石5级":
										case "蔚蓝灵石5级":
										case "橙黄灵石5级":
										case "进击幻彩灵石5级":
										case "深灰灵石5级":
										case "盈绿灵石5级":
										case "透蓝灵石5级":
										case "韧紫灵石5级":
										case "抵御幻彩灵石5级":
										case "驭朱灵石5级":
										case "赤褐灵石5级":
										case "守阳灵石5级":
										case "狂热幻彩灵石5级":
										case "纯紫灵石5级":
											num8 += 5000;
											break;
										case "精绿灵石2级":
										case "蔚蓝灵石2级":
										case "驭朱灵石2级":
										case "橙黄灵石2级":
										case "守阳灵石2级":
										case "纯紫灵石2级":
										case "透蓝灵石2级":
										case "抵御幻彩灵石2级":
										case "命朱灵石2级":
										case "深灰灵石2级":
										case "赤褐灵石2级":
										case "新阳灵石2级":
										case "进击幻彩灵石2级":
										case "狂热幻彩灵石2级":
										case "盈绿灵石2级":
										case "韧紫灵石2级":
											num8 += 2000;
											break;
										case "抵御幻彩灵石7级":
										case "命朱灵石7级":
										case "赤褐灵石7级":
										case "狂热幻彩灵石7级":
										case "精绿灵石7级":
										case "纯紫灵石7级":
										case "韧紫灵石7级":
										case "驭朱灵石7级":
										case "深灰灵石7级":
										case "盈绿灵石7级":
										case "新阳灵石7级":
										case "蔚蓝灵石7级":
										case "橙黄灵石7级":
										case "守阳灵石7级":
										case "进击幻彩灵石7级":
										case "透蓝灵石7级":
											num8 += 7000;
											break;
										case "精绿灵石9级":
										case "驭朱灵石9级":
										case "蔚蓝灵石9级":
										case "橙黄灵石9级":
										case "抵御幻彩灵石9级":
										case "透蓝灵石9级":
										case "纯紫灵石9级":
										case "命朱灵石9级":
										case "赤褐灵石9级":
										case "深灰灵石9级":
										case "守阳灵石9级":
										case "新阳灵石9级":
										case "盈绿灵石9级":
										case "进击幻彩灵石9级":
										case "狂热幻彩灵石9级":
										case "韧紫灵石9级":
											num8 += 9000;
											break;
										case "驭朱灵石4级":
										case "深灰灵石4级":
										case "新阳灵石4级":
										case "盈绿灵石4级":
										case "蔚蓝灵石4级":
										case "命朱灵石4级":
										case "橙黄灵石4级":
										case "进击幻彩灵石4级":
										case "抵御幻彩灵石4级":
										case "透蓝灵石4级":
										case "守阳灵石4级":
										case "精绿灵石4级":
										case "赤褐灵石4级":
										case "纯紫灵石4级":
										case "韧紫灵石4级":
										case "狂热幻彩灵石4级":
											num8 += 4000;
											break;
										case "透蓝灵石6级":
										case "抵御幻彩灵石6级":
										case "命朱灵石6级":
										case "盈绿灵石6级":
										case "深灰灵石6级":
										case "蔚蓝灵石6级":
										case "进击幻彩灵石6级":
										case "橙黄灵石6级":
										case "赤褐灵石6级":
										case "驭朱灵石6级":
										case "精绿灵石6级":
										case "新阳灵石6级":
										case "韧紫灵石6级":
										case "守阳灵石6级":
										case "纯紫灵石6级":
										case "狂热幻彩灵石6级":
											num8 += 6000;
											break;
										case "透蓝灵石1级":
										case "驭朱灵石1级":
										case "韧紫灵石1级":
										case "守阳灵石1级":
										case "赤褐灵石1级":
										case "纯紫灵石1级":
										case "狂热幻彩灵石1级":
										case "精绿灵石1级":
										case "新阳灵石1级":
										case "盈绿灵石1级":
										case "蔚蓝灵石1级":
										case "橙黄灵石1级":
										case "深灰灵石1级":
										case "命朱灵石1级":
										case "进击幻彩灵石1级":
										case "抵御幻彩灵石1级":
											num8 += 1000;
											break;
										case "蔚蓝灵石10级":
										case "狂热幻彩灵石10级":
										case "精绿灵石10级":
										case "透蓝灵石10级":
										case "橙黄灵石10级":
										case "抵御幻彩灵石10级":
										case "进击幻彩灵石10级":
										case "命朱灵石10级":
										case "守阳灵石10级":
										case "赤褐灵石10级":
										case "盈绿灵石10级":
										case "深灰灵石10级":
										case "韧紫灵石10级":
										case "纯紫灵石10级":
										case "新阳灵石10级":
										case "驭朱灵石10级":
											num8 += 10000;
											break;
										case "驭朱灵石3级":
										case "韧紫灵石3级":
										case "精绿灵石3级":
										case "新阳灵石3级":
										case "守阳灵石3级":
										case "盈绿灵石3级":
										case "蔚蓝灵石3级":
										case "命朱灵石3级":
										case "橙黄灵石3级":
										case "进击幻彩灵石3级":
										case "抵御幻彩灵石3级":
										case "透蓝灵石3级":
										case "赤褐灵石3级":
										case "深灰灵石3级":
										case "狂热幻彩灵石3级":
										case "纯紫灵石3级":
											num8 += 3000;
											break;
									}
								}
							}
							int num9 = num3 + num4 + num5 + num6 + num7 + num8;
							decimal num10 = (decimal)v3 / (decimal)num2 * 0.9m * (decimal)num9;
							decimal num11 = (decimal)num9 * 0.1m;
							return (int)(num10 + num11);
						}
					case PersistentItemType.消耗:
						{
							int v2 = 当前持久.V;
							int ItemLast = 对应模板.V.ItemLast;
							int num = 对应模板.V.SalePrice;
							return (int)((decimal)v2 / (decimal)ItemLast * (decimal)num);
						}
					case PersistentItemType.堆叠:
						{
							int v = 当前持久.V;
							return 对应模板.V.SalePrice * v;
						}
					case PersistentItemType.回复:
						return 1;
					case PersistentItemType.容器:
						return 对应模板.V.SalePrice;
					case PersistentItemType.纯度:
						return 对应模板.V.SalePrice;
				}
			}
		}

		
		// (get) Token: 0x06000574 RID: 1396 RVA: 0x00005228 File Offset: 0x00003428
		public int 堆叠上限
		{
			get
			{
				return this.对应模板.V.ItemLast;
			}
		}

		
		// (get) Token: 0x06000575 RID: 1397 RVA: 0x0000523A File Offset: 0x0000343A
		public int 默认持久
		{
			get
			{
				if (this.PersistType != PersistentItemType.装备)
				{
					return this.对应模板.V.ItemLast;
				}
				return this.对应模板.V.ItemLast * 1000;
			}
		}

		
		// (get) Token: 0x06000576 RID: 1398 RVA: 0x0000526C File Offset: 0x0000346C
		// (set) Token: 0x06000577 RID: 1399 RVA: 0x00005279 File Offset: 0x00003479
		public byte 当前位置
		{
			get
			{
				return this.物品位置.V;
			}
			set
			{
				this.物品位置.V = value;
			}
		}

		
		// (get) Token: 0x06000578 RID: 1400 RVA: 0x00005287 File Offset: 0x00003487
		public bool IsBound
		{
			get
			{
				return this.物品模板.IsBound;
			}
		}

		
		// (get) Token: 0x06000579 RID: 1401 RVA: 0x00005294 File Offset: 0x00003494
		public bool Resource
		{
			get
			{
				return this.对应模板.V.Resource;
			}
		}

		
		// (get) Token: 0x0600057A RID: 1402 RVA: 0x000052A6 File Offset: 0x000034A6
		public bool CanSold
		{
			get
			{
				return this.物品模板.CanSold;
			}
		}

		
		// (get) Token: 0x0600057B RID: 1403 RVA: 0x000052B3 File Offset: 0x000034B3
		public bool 能否堆叠
		{
			get
			{
				return this.对应模板.V.PersistType == PersistentItemType.堆叠;
			}
		}

		
		// (get) Token: 0x0600057C RID: 1404 RVA: 0x000052C8 File Offset: 0x000034C8
		public bool CanDrop
		{
			get
			{
				return this.物品模板.CanDrop;
			}
		}

		
		// (get) Token: 0x0600057D RID: 1405 RVA: 0x000052D5 File Offset: 0x000034D5
		public ushort SkillId
		{
			get
			{
				return this.物品模板.AdditionalSkill;
			}
		}

		
		// (get) Token: 0x0600057E RID: 1406 RVA: 0x000052E2 File Offset: 0x000034E2
		public byte GroupId
		{
			get
			{
				return this.物品模板.Group;
			}
		}

		
		// (get) Token: 0x0600057F RID: 1407 RVA: 0x000052EF File Offset: 0x000034EF
		public int GroupCooling
		{
			get
			{
				return this.物品模板.GroupCooling;
			}
		}

		
		// (get) Token: 0x06000580 RID: 1408 RVA: 0x000052FC File Offset: 0x000034FC
		public int Cooldown
		{
			get
			{
				return this.物品模板.Cooldown;
			}
		}

		
		public ItemData()
		{
			
			
		}

		
		public ItemData(GameItems 模板, CharacterData 来源, byte 容器, byte 位置, int 持久)
		{
			
			
			this.对应模板.V = 模板;
			this.生成来源.V = 来源;
			this.物品容器.V = 容器;
			this.物品位置.V = 位置;
			this.生成时间.V = MainProcess.CurrentTime;
			this.最大持久.V = this.物品模板.ItemLast;
			this.当前持久.V = Math.Min(持久, this.最大持久.V);
			GameDataGateway.ItemData表.AddData(this, true);
		}

		
		public override string ToString()
		{
			return this.Name;
		}

		
		public virtual byte[] 字节描述()
		{
			byte[] result;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(ItemData.数据版本);
					BinaryWriter binaryWriter2 = binaryWriter;
					CharacterData v = this.生成来源.V;
					binaryWriter2.Write((v != null) ? v.数据索引.V : 0);
					binaryWriter.Write(ComputingClass.TimeShift(this.生成时间.V));
					binaryWriter.Write(this.对应模板.V.Id);
					binaryWriter.Write(this.物品容器.V);
					binaryWriter.Write(this.物品位置.V);
					binaryWriter.Write(this.当前持久.V);
					binaryWriter.Write(this.最大持久.V);
					binaryWriter.Write((byte)(this.IsBound ? 10 : 0));
					binaryWriter.Write((ushort)0);
					binaryWriter.Write(0);
					result = memoryStream.ToArray();
				}
			}
			return result;
		}

		
		public virtual byte[] 字节描述(int 数量)
		{
			byte[] result;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(ItemData.数据版本);
					BinaryWriter binaryWriter2 = binaryWriter;
					CharacterData v = this.生成来源.V;
					binaryWriter2.Write((v != null) ? v.数据索引.V : 0);
					binaryWriter.Write(ComputingClass.TimeShift(this.生成时间.V));
					binaryWriter.Write(this.对应模板.V.Id);
					binaryWriter.Write(this.物品容器.V);
					binaryWriter.Write(this.物品位置.V);
					binaryWriter.Write(数量);
					binaryWriter.Write(this.最大持久.V);
					binaryWriter.Write((byte)(this.IsBound ? 10 : 0));
					binaryWriter.Write((ushort)0);
					binaryWriter.Write(0);
					result = memoryStream.ToArray();
				}
			}
			return result;
		}

		
		static ItemData()
		{
			
			ItemData.数据版本 = 14;
		}

		
		public static byte 数据版本;

		
		public readonly DataMonitor<GameItems> 对应模板;

		
		public readonly DataMonitor<DateTime> 生成时间;

		
		public readonly DataMonitor<CharacterData> 生成来源;

		
		public readonly DataMonitor<int> 当前持久;

		
		public readonly DataMonitor<int> 最大持久;

		
		public readonly DataMonitor<byte> 物品容器;

		
		public readonly DataMonitor<byte> 物品位置;

		
		public int PurchaseId;
	}
}
