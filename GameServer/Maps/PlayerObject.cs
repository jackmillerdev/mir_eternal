using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using GameServer.Data;
using GameServer.Templates;
using GameServer.Networking;
using GameServer.Enums;

namespace GameServer.Maps
{
    public sealed class PlayerObject : MapObject
    {

        // (get) Token: 0x06000880 RID: 2176 RVA: 0x00006DDA File Offset: 0x00004FDA
        public 客户网络 网络连接
        {
            get
            {
                return this.CharacterData.ActiveConnection;
            }
        }


        // (get) Token: 0x06000881 RID: 2177 RVA: 0x00006DE7 File Offset: 0x00004FE7
        public byte 交易状态
        {
            get
            {
                if (this.当前交易 == null)
                {
                    return 0;
                }
                if (this.当前交易.交易申请方 == this)
                {
                    return this.当前交易.申请方状态;
                }
                return this.当前交易.接收方状态;
            }
        }


        // (get) Token: 0x06000882 RID: 2178 RVA: 0x00006E18 File Offset: 0x00005018
        // (set) Token: 0x06000883 RID: 2179 RVA: 0x00006E2F File Offset: 0x0000502F
        public byte 摆摊状态
        {
            get
            {
                if (this.当前摊位 != null)
                {
                    return this.当前摊位.摊位状态;
                }
                return 0;
            }
            set
            {
                if (this.当前摊位 != null)
                {
                    this.当前摊位.摊位状态 = value;
                }
            }
        }


        // (get) Token: 0x06000884 RID: 2180 RVA: 0x00006E45 File Offset: 0x00005045
        public string 摊位名字
        {
            get
            {
                if (this.当前摊位 != null)
                {
                    return this.当前摊位.摊位名字;
                }
                return "";
            }
        }


        public PlayerObject(CharacterData CharacterData, 客户网络 网络连接)
        {
            this.CharacterData = CharacterData;
            this.宠物列表 = new List<PetObject>();
            this.PassiveSkill = new Dictionary<ushort, SkillData>();
            this.Stat加成[this] = 角色成长.获取数据(this.角色职业, this.当前等级);
            Dictionary<object, int> dictionary = new Dictionary<object, int>();
            dictionary[this] = (int)(this.当前等级 * 10);
            this.CombatBonus = dictionary;
            this.称号时间 = DateTime.MaxValue;
            this.拾取时间 = MainProcess.CurrentTime.AddSeconds(1.0);
            base.恢复时间 = MainProcess.CurrentTime.AddSeconds(5.0);
            this.特权时间 = ((this.本期特权 > 0) ? this.本期日期.AddDays(30.0) : DateTime.MaxValue);
            foreach (EquipmentData EquipmentData in this.角色装备.Values)
            {
                this.CombatBonus[EquipmentData] = EquipmentData.装备战力;
                if (EquipmentData.当前持久.V > 0)
                {
                    this.Stat加成[EquipmentData] = EquipmentData.装备Stat;
                }
                SkillData SkillData;
                if (EquipmentData.第一铭文 != null && this.MainSkills表.TryGetValue(EquipmentData.第一铭文.SkillId, out SkillData))
                {
                    SkillData.Id = EquipmentData.第一铭文.Id;
                }
                SkillData SkillData2;
                if (EquipmentData.第二铭文 != null && this.MainSkills表.TryGetValue(EquipmentData.第二铭文.SkillId, out SkillData2))
                {
                    SkillData2.Id = EquipmentData.第二铭文.Id;
                }
            }
            foreach (SkillData SkillData3 in this.MainSkills表.Values)
            {
                this.CombatBonus[SkillData3] = SkillData3.CombatBonus;
                this.Stat加成[SkillData3] = SkillData3.Stat加成;
                foreach (ushort key in SkillData3.PassiveSkill.ToList<ushort>())
                {
                    this.PassiveSkill.Add(key, SkillData3);
                }
            }
            foreach (BuffData BuffData in this.Buff列表.Values)
            {
                if ((BuffData.Effect & BuffEffectType.StatsIncOrDec) != BuffEffectType.SkillSign)
                {
                    this.Stat加成.Add(BuffData, BuffData.Stat加成);
                }
            }
            foreach (KeyValuePair<byte, DateTime> keyValuePair in this.称号列表.ToList<KeyValuePair<byte, DateTime>>())
            {
                if (MainProcess.CurrentTime >= keyValuePair.Value)
                {
                    if (this.称号列表.Remove(keyValuePair.Key) && this.当前称号 == keyValuePair.Key)
                    {
                        this.当前称号 = 0;
                    }
                }
                else if (keyValuePair.Value < this.称号时间)
                {
                    this.称号时间 = keyValuePair.Value;
                }
            }
            GameTitle 游戏称号;
            if (this.当前称号 > 0 && GameTitle.DataSheet.TryGetValue(this.当前称号, out 游戏称号))
            {
                this.CombatBonus[this.当前称号] = 游戏称号.Combat;
                this.Stat加成[this.当前称号] = 游戏称号.Attributes;
            }
            if (this.当前体力 == 0)
            {
                this.当前地图 = MapGatewayProcess.分配地图(this.重生地图);
                this.当前坐标 = (this.红名玩家 ? this.当前地图.红名区域.RandomCoords : this.当前地图.复活区域.RandomCoords);
                this.当前体力 = (int)((float)this[GameObjectStats.MaxPhysicalStrength] * 0.3f);
                this.当前魔力 = (int)((float)this[GameObjectStats.MaxMagic2] * 0.3f);
            }
            else if (GameMap.DataSheet[(byte)CharacterData.当前地图.V].NoReconnect)
            {
                if (CharacterData.当前地图.V == 152)
                {
                    this.当前地图 = MapGatewayProcess.沙城地图;
                    if (this.所属行会 != null && this.所属行会 == SystemData.数据.占领行会.V)
                    {
                        this.当前坐标 = MapGatewayProcess.守方传送区域.RandomCoords;
                    }
                    else
                    {
                        this.当前坐标 = MapGatewayProcess.外城复活区域.RandomCoords;
                    }
                }
                else if (GameMap.DataSheet[(byte)CharacterData.当前地图.V].NoReconnectMapId == 0)
                {
                    this.当前地图 = MapGatewayProcess.分配地图(this.重生地图);
                    this.当前坐标 = this.当前地图.复活区域.RandomCoords;
                }
                else
                {
                    this.当前地图 = MapGatewayProcess.分配地图((int)GameMap.DataSheet[(byte)CharacterData.当前地图.V].NoReconnectMapId);
                    MapAreas 传送区域 = this.当前地图.传送区域;
                    this.当前坐标 = ((传送区域 != null) ? 传送区域.RandomCoords : this.当前地图.地图区域.First<MapAreas>().RandomCoords);
                }
            }
            else
            {
                this.当前地图 = MapGatewayProcess.分配地图(CharacterData.当前地图.V);
            }
            this.更新玩家战力();
            this.更新对象Stat();
            this.对象死亡 = false;
            this.阻塞网格 = true;
            MapGatewayProcess.添加MapObject(this);
            this.激活对象 = true;
            MapGatewayProcess.添加激活对象(this);
            CharacterData.登录日期.V = MainProcess.CurrentTime;
            CharacterData.角色上线(网络连接);
            if (网络连接 != null)
            {
                网络连接.发送封包(new 同步CharacterData
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度,
                    当前经验 = this.当前经验,
                    双倍经验 = this.双倍经验,
                    所需经验 = this.所需经验,
                    PK值惩罚 = this.PK值惩罚,
                    对象朝向 = (ushort)this.当前方向,
                    当前地图 = this.当前地图.MapId,
                    当前线路 = this.当前地图.路线编号,
                    对象职业 = (byte)this.角色职业,
                    对象性别 = (byte)this.角色性别,
                    对象等级 = this.当前等级,
                    AttackMode = (byte)this.AttackMode,
                    当前时间 = ComputingClass.TimeShift(MainProcess.CurrentTime),
                    OpenLevelCommand = (ushort)Config.游戏OpenLevelCommand,
                    特修折扣 = (ushort)(Config.装备特修折扣 * 10000m)
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncSupplementaryVariablesPacket
                {
                    变量类型 = 1,
                    对象编号 = this.MapId,
                    变量索引 = 112,
                    变量内容 = ComputingClass.TimeShift(CharacterData.补给日期.V)
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncSupplementaryVariablesPacket
                {
                    变量类型 = 1,
                    对象编号 = this.MapId,
                    变量索引 = 975,
                    变量内容 = ComputingClass.TimeShift(CharacterData.战备日期.V)
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncBackpackSizePacket
                {
                    背包大小 = this.背包大小,
                    仓库大小 = this.仓库大小
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncSkillInfoPacket
                {
                    技能描述 = this.全部技能描述()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncSkillFieldsPacket
                {
                    栏位描述 = this.快捷栏位描述()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncBackpackInfoPacket
                {
                    物品描述 = this.全部物品描述()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new 同步角色Stat
                {
                    StatDescription = this.玩家StatDescription()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncReputationListPacket());
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncClientVariablesPacket
                {
                    字节数据 = CharacterData.角色设置()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncCurrencyQuantityPacket
                {
                    字节描述 = this.全部货币描述()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new 同步签到信息());
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncPrivilegedInfoPacket
                {
                    字节数组 = this.玛法特权描述()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new EndSyncDataPacket
                {
                    角色编号 = this.MapId
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncTeacherInfoPacket
                {
                    师门参数 = this.师门参数
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new SyncTitleInfoPacket
                {
                    字节描述 = this.全部称号描述()
                });
            }
            if (网络连接 != null)
            {
                网络连接.发送封包(new 玩家名字变灰
                {
                    对象编号 = this.MapId,
                    是否灰名 = this.灰名玩家
                });
            }
            foreach (CharacterData CharacterData2 in this.粉丝列表)
            {
                客户网络 网络连接2 = CharacterData2.ActiveConnection;
                if (网络连接2 != null)
                {
                    网络连接2.发送封包(new 好友上线下线
                    {
                        对象编号 = this.MapId,
                        对象名字 = this.对象名字,
                        对象职业 = (byte)this.角色职业,
                        对象性别 = (byte)this.角色性别,
                        上线下线 = 0
                    });
                }
            }
            foreach (CharacterData CharacterData3 in this.仇恨列表)
            {
                客户网络 网络连接3 = CharacterData3.ActiveConnection;
                if (网络连接3 != null)
                {
                    网络连接3.发送封包(new 好友上线下线
                    {
                        对象编号 = this.MapId,
                        对象名字 = this.对象名字,
                        对象职业 = (byte)this.角色职业,
                        对象性别 = (byte)this.角色性别,
                        上线下线 = 0
                    });
                }
            }
            if ((this.偶像列表.Count != 0 || this.仇人列表.Count != 0) && 网络连接 != null)
            {
                网络连接.发送封包(new SyncFriendsListPacket
                {
                    字节描述 = this.社交列表描述()
                });
            }
            if (this.黑名单表.Count != 0 && 网络连接 != null)
            {
                网络连接.发送封包(new SyncBlacklistPacket
                {
                    字节描述 = this.社交屏蔽描述()
                });
            }
            if (this.未读邮件.Count >= 1 && 网络连接 != null)
            {
                网络连接.发送封包(new 未读邮件提醒
                {
                    邮件数量 = this.未读邮件.Count
                });
            }
            if (this.所属队伍 != null && 网络连接 != null)
            {
                网络连接.发送封包(new 玩家加入队伍
                {
                    字节描述 = this.所属队伍.队伍描述()
                });
            }
            if (this.所属行会 != null)
            {
                if (网络连接 != null)
                {
                    网络连接.发送封包(new GuildInfoAnnouncementPacket
                    {
                        字节数据 = this.所属行会.行会信息描述()
                    });
                }
                this.所属行会.发送封包(new SyncMemberInfoPacket
                {
                    对象编号 = this.MapId,
                    对象信息 = this.当前地图.MapId,
                    当前等级 = this.当前等级
                });
                if (this.所属行会.行会成员[this.CharacterData] <= GuildJobs.理事 && this.所属行会.申请列表.Count > 0 && 网络连接 != null)
                {
                    网络连接.发送封包(new SendGuildNoticePacket
                    {
                        提醒类型 = 1
                    });
                }
                if (this.所属行会.行会成员[this.CharacterData] <= GuildJobs.副长 && this.所属行会.结盟申请.Count > 0 && 网络连接 != null)
                {
                    网络连接.发送封包(new SendGuildNoticePacket
                    {
                        提醒类型 = 2
                    });
                }
                if (this.所属行会.行会成员[this.CharacterData] <= GuildJobs.副长 && this.所属行会.解除申请.Count > 0 && 网络连接 != null)
                {
                    网络连接.发送封包(new GuildDiplomaticAnnouncementPacket
                    {
                        字节数据 = this.所属行会.解除申请描述()
                    });
                }
            }
            if (SystemData.数据.占领行会.V != null && 网络连接 != null)
            {
                网络连接.发送封包(new 同步占领行会
                {
                    行会编号 = SystemData.数据.占领行会.V.行会编号
                });
            }
            if (this.所属行会 != null && this.所属行会 == SystemData.数据.占领行会.V && this.所属行会.行会成员[CharacterData] == GuildJobs.会长)
            {
                NetworkServiceGateway.发送公告("沙巴克城主 [" + this.对象名字 + "] 进入了游戏", false);
            }
            TeamData 所属队伍 = this.所属队伍;
            if (所属队伍 == null)
            {
                return;
            }
            所属队伍.发送封包(new 同步队员状态
            {
                对象编号 = this.MapId
            });
        }


        public override void 处理对象数据()
        {
            if (this.绑定地图)
            {
                if (this.当前地图.MapId == 183 && MainProcess.CurrentTime.Hour != (int)Config.武斗场时间一 && MainProcess.CurrentTime.Hour != (int)Config.武斗场时间二)
                {
                    if (this.对象死亡)
                    {
                        this.玩家请求复活();
                        return;
                    }
                    this.玩家切换地图(this.复活地图, AreaType.复活区域, default(Point));
                    return;
                }
                else
                {
                    foreach (SkillData SkillData in this.MainSkills表.Values.ToList<SkillData>())
                    {
                        if (SkillData.SkillCount > 0 && SkillData.剩余次数.V < SkillData.SkillCount)
                        {
                            if (SkillData.计数时间 == default(DateTime))
                            {
                                SkillData.计数时间 = MainProcess.CurrentTime.AddMilliseconds((double)SkillData.PeriodCount);
                            }
                            else if (MainProcess.CurrentTime > SkillData.计数时间)
                            {
                                DataMonitor<byte> 剩余次数 = SkillData.剩余次数;
                                if ((剩余次数.V += 1) >= SkillData.SkillCount)
                                {
                                    SkillData.计数时间 = default(DateTime);
                                }
                                else
                                {
                                    SkillData.计数时间 = MainProcess.CurrentTime.AddMilliseconds((double)SkillData.PeriodCount);
                                }
                                客户网络 网络连接 = this.网络连接;
                                if (网络连接 != null)
                                {
                                    网络连接.发送封包(new SyncSkillCountPacket
                                    {
                                        SkillId = SkillData.SkillId.V,
                                        SkillCount = SkillData.剩余次数.V,
                                        技能冷却 = (int)SkillData.PeriodCount
                                    });
                                }
                            }
                        }
                    }
                    foreach (技能实例 技能实例 in this.技能任务.ToList<技能实例>())
                    {
                        技能实例.处理任务();
                    }
                    foreach (KeyValuePair<ushort, BuffData> keyValuePair in this.Buff列表.ToList<KeyValuePair<ushort, BuffData>>())
                    {
                        base.轮询Buff时处理(keyValuePair.Value);
                    }
                    if (MainProcess.CurrentTime >= this.称号时间)
                    {
                        DateTime t = DateTime.MaxValue;
                        foreach (KeyValuePair<byte, DateTime> keyValuePair2 in this.称号列表.ToList<KeyValuePair<byte, DateTime>>())
                        {
                            if (MainProcess.CurrentTime >= keyValuePair2.Value)
                            {
                                this.玩家称号到期(keyValuePair2.Key);
                            }
                            else if (keyValuePair2.Value < t)
                            {
                                t = keyValuePair2.Value;
                            }
                        }
                        this.称号时间 = t;
                    }
                    if (MainProcess.CurrentTime >= this.特权时间)
                    {
                        this.玩家特权到期();
                        int num;
                        if (this.剩余特权.TryGetValue(this.预定特权, out num) && num >= 30)
                        {
                            this.玩家激活特权(this.预定特权);
                            MonitorDictionary<byte, int> 剩余特权 = this.剩余特权;
                            byte 预定特权 = this.预定特权;
                            if ((剩余特权[预定特权] -= 30) <= 0)
                            {
                                this.预定特权 = 0;
                            }
                        }
                        if (this.本期特权 == 0)
                        {
                            客户网络 网络连接2 = this.网络连接;
                            if (网络连接2 != null)
                            {
                                网络连接2.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 65553
                                });
                            }
                        }
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 != null)
                        {
                            网络连接3.发送封包(new SyncPrivilegedInfoPacket
                            {
                                字节数组 = this.玛法特权描述()
                            });
                        }
                    }
                    if (this.灰名玩家)
                    {
                        this.灰名时间 -= MainProcess.CurrentTime - base.处理计时;
                    }
                    if (this.PK值惩罚 > 0)
                    {
                        this.减PK时间 -= MainProcess.CurrentTime - base.处理计时;
                    }
                    if (this.所属队伍 != null && MainProcess.CurrentTime > this.队伍时间)
                    {
                        TeamData 所属队伍 = this.所属队伍;
                        if (所属队伍 != null)
                        {
                            所属队伍.发送封包(new 同步队员信息
                            {
                                队伍编号 = this.所属队伍.队伍编号,
                                对象编号 = this.MapId,
                                对象等级 = (int)this.当前等级,
                                MaxPhysicalStrength = this[GameObjectStats.MaxPhysicalStrength],
                                MaxMagic2 = this[GameObjectStats.MaxMagic2],
                                当前体力 = this.当前体力,
                                当前魔力 = this.当前魔力,
                                当前地图 = this.当前地图.MapId,
                                当前线路 = this.当前地图.路线编号,
                                横向坐标 = this.当前坐标.X,
                                纵向坐标 = this.当前坐标.Y,
                                坐标高度 = (int)this.当前高度,
                                AttackMode = (byte)this.AttackMode
                            });
                        }
                        this.队伍时间 = MainProcess.CurrentTime.AddSeconds(5.0);
                    }
                    if (!this.对象死亡)
                    {
                        if (MainProcess.CurrentTime > this.拾取时间)
                        {
                            this.拾取时间 = this.拾取时间.AddMilliseconds(1000.0);
                            foreach (MapObject MapObject in this.当前地图[this.当前坐标].ToList<MapObject>())
                            {
                                ItemObject ItemObject = MapObject as ItemObject;
                                if (ItemObject != null)
                                {
                                    this.玩家拾取物品(ItemObject);
                                    break;
                                }
                            }
                        }
                        if (MainProcess.CurrentTime > base.恢复时间)
                        {
                            if (!this.检查状态(GameObjectState.Poisoned))
                            {
                                this.当前体力 += this[GameObjectStats.体力恢复];
                                this.当前魔力 += this[GameObjectStats.魔力恢复];
                            }
                            base.恢复时间 = base.恢复时间.AddSeconds(30.0);
                        }
                        EquipmentData EquipmentData;
                        if (MainProcess.CurrentTime > this.战具计时 && this.角色装备.TryGetValue(15, out EquipmentData) && EquipmentData.当前持久.V > 0)
                        {
                            if (EquipmentData.Id != 99999100)
                            {
                                if (EquipmentData.Id != 99999101)
                                {
                                    if (EquipmentData.Id != 99999102)
                                    {
                                        if (EquipmentData.Id != 99999103)
                                        {
                                            if (EquipmentData.Id == 99999110 || EquipmentData.Id == 99999111)
                                            {
                                                int num2 = Math.Min(10, Math.Min(EquipmentData.当前持久.V, this[GameObjectStats.MaxPhysicalStrength] - this.当前体力));
                                                if (num2 > 0)
                                                {
                                                    this.当前体力 += num2;
                                                    this.当前魔力 += num2;
                                                    this.战具损失持久(num2);
                                                }
                                                this.战具计时 = MainProcess.CurrentTime.AddMilliseconds(1000.0);
                                                goto IL_794;
                                            }
                                            goto IL_794;
                                        }
                                    }
                                    int num3 = Math.Min(15, Math.Min(EquipmentData.当前持久.V, this[GameObjectStats.MaxMagic2] - this.当前魔力));
                                    if (num3 > 0)
                                    {
                                        this.当前魔力 += num3;
                                        this.战具损失持久(num3);
                                    }
                                    this.战具计时 = MainProcess.CurrentTime.AddMilliseconds(1000.0);
                                    goto IL_794;
                                }
                            }
                            int num4 = Math.Min(10, Math.Min(EquipmentData.当前持久.V, this[GameObjectStats.MaxPhysicalStrength] - this.当前体力));
                            if (num4 > 0)
                            {
                                this.当前体力 += num4;
                                this.战具损失持久(num4);
                            }
                            this.战具计时 = MainProcess.CurrentTime.AddMilliseconds(1000.0);
                        }
                    IL_794:
                        if (base.治疗次数 > 0 && MainProcess.CurrentTime > base.治疗时间)
                        {
                            int 治疗次数 = base.治疗次数;
                            base.治疗次数 = 治疗次数 - 1;
                            this.当前体力 += base.治疗基数;
                            base.治疗时间 = MainProcess.CurrentTime.AddMilliseconds(500.0);
                        }
                        if (this.回血次数 > 0 && MainProcess.CurrentTime > this.药品回血)
                        {
                            this.回血次数--;
                            this.药品回血 = MainProcess.CurrentTime.AddMilliseconds(1000.0);
                            this.当前体力 += (int)Math.Max(0f, (float)this.回血基数 * (1f + (float)this[GameObjectStats.药品回血] / 10000f));
                        }
                        if (this.回魔次数 > 0 && MainProcess.CurrentTime > this.药品回魔)
                        {
                            this.回魔次数--;
                            this.药品回魔 = MainProcess.CurrentTime.AddMilliseconds(1000.0);
                            this.当前魔力 += (int)Math.Max(0f, (float)this.回魔基数 * (1f + (float)this[GameObjectStats.药品回魔] / 10000f));
                        }
                        if (this.当前地图.MapId == 183 && MainProcess.CurrentTime > this.经验计时)
                        {
                            this.经验计时 = MainProcess.CurrentTime.AddSeconds(5.0);
                            this.玩家增加经验(null, (this.当前地图[this.当前坐标].FirstOrDefault(delegate (MapObject O)
                            {
                                GuardInstance GuardInstance = O as GuardInstance;
                                return GuardInstance != null && GuardInstance.模板编号 == 6121;
                            }) == null) ? 500 : 2500);
                        }
                    }
                    GuildData 所属行会 = this.所属行会;
                    if (所属行会 != null)
                    {
                        所属行会.清理数据();
                    }
                }
            }
            base.处理对象数据();
        }


        public override void ItSelf死亡处理(MapObject 对象, bool 技能击杀)
        {
            base.ItSelf死亡处理(对象, 技能击杀);
            foreach (BuffData BuffData in this.Buff列表.Values.ToList<BuffData>())
            {
                if (BuffData.死亡消失)
                {
                    base.删除Buff时处理(BuffData.Id.V);
                }
            }
            this.回魔次数 = 0;
            this.回血次数 = 0;
            base.治疗次数 = 0;
            PlayerDeals PlayerDeals = this.当前交易;
            if (PlayerDeals != null)
            {
                PlayerDeals.结束交易();
            }
            foreach (PetObject PetObject in this.宠物列表.ToList<PetObject>())
            {
                PetObject.ItSelf死亡处理(null, false);
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 != null)
            {
                网络连接.发送封包(new 离开战斗姿态
                {
                    对象编号 = this.MapId
                });
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 != null)
            {
                网络连接2.发送封包(new SendResurrectionMessagePacket());
            }
            PlayerObject PlayerObject = null;
            PlayerObject PlayerObject2 = 对象 as PlayerObject;
            if (PlayerObject2 != null)
            {
                PlayerObject = PlayerObject2;
            }
            else
            {
                PetObject PetObject2 = 对象 as PetObject;
                if (PetObject2 != null)
                {
                    PlayerObject = PetObject2.宠物主人;
                }
                else
                {
                    TrapObject TrapObject = 对象 as TrapObject;
                    if (TrapObject != null)
                    {
                        PlayerObject PlayerObject3 = TrapObject.陷阱来源 as PlayerObject;
                        if (PlayerObject3 != null)
                        {
                            PlayerObject = PlayerObject3;
                        }
                    }
                }
            }
            if (PlayerObject != null && !this.当前地图.自由区内(this.当前坐标) && !this.灰名玩家 && !this.红名玩家 && (MapGatewayProcess.沙城节点 < 2 || (this.当前地图.MapId != 152 && this.当前地图.MapId != 178)))
            {
                PlayerObject.PK值惩罚 += 50;
                if (技能击杀)
                {
                    PlayerObject.武器幸运损失();
                }
            }
            if (PlayerObject != null)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 != null)
                {
                    网络连接3.发送封包(new 同步气泡提示
                    {
                        泡泡类型 = 1,
                        泡泡参数 = PlayerObject.MapId
                    });
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 != null)
                {
                    网络连接4.发送封包(new 同步对战结果
                    {
                        击杀方式 = 1,
                        胜方编号 = PlayerObject.MapId,
                        败方编号 = this.MapId,
                        PK值惩罚 = 50
                    });
                }
                string text = (this.所属行会 != null) ? string.Format("[{0}] of the Guild", this.所属行会) : "";
                string text2 = (PlayerObject.所属行会 != null) ? string.Format("[{0}] of the Guild", PlayerObject.所属行会) : "";
                NetworkServiceGateway.发送公告(string.Format("{0}[{1}] was killed by {3}[{4}] in {2}", new object[]
                {
                    text,
                    this,
                    this.当前地图,
                    text2,
                    PlayerObject
                }), false);
            }
            if (PlayerObject != null && this.当前地图.掉落装备(this.当前坐标, this.红名玩家))
            {
                foreach (EquipmentData EquipmentData in this.角色装备.Values.ToList<EquipmentData>())
                {
                    if (EquipmentData.CanDrop && ComputingClass.计算概率(0.05f))
                    {
                        new ItemObject(EquipmentData.物品模板, EquipmentData, this.当前地图, this.当前坐标, new HashSet<CharacterData>(), 0, false);
                        this.角色装备.Remove(EquipmentData.当前位置);
                        this.玩家穿卸装备((EquipmentWearingParts)EquipmentData.当前位置, EquipmentData, null);
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 != null)
                        {
                            网络连接5.发送封包(new 玩家掉落装备
                            {
                                物品描述 = EquipmentData.字节描述()
                            });
                        }
                        客户网络 网络连接6 = this.网络连接;
                        if (网络连接6 != null)
                        {
                            网络连接6.发送封包(new 删除玩家物品
                            {
                                背包类型 = 0,
                                物品位置 = EquipmentData.当前位置
                            });
                        }
                    }
                }
                foreach (ItemData ItemData in this.角色背包.Values.ToList<ItemData>())
                {
                    if (ItemData.CanDrop && ComputingClass.计算概率(0.1f))
                    {
                        if (ItemData.PersistType == PersistentItemType.堆叠 && ItemData.当前持久.V > 1)
                        {
                            ItemObject ItemObject = new ItemObject(ItemData.物品模板, new ItemData(ItemData.物品模板, this.CharacterData, 1, ItemData.当前位置, 1), this.当前地图, this.当前坐标, new HashSet<CharacterData>(), 0, false);
                            客户网络 网络连接7 = this.网络连接;
                            if (网络连接7 != null)
                            {
                                网络连接7.发送封包(new 玩家掉落装备
                                {
                                    物品描述 = ItemObject.ItemData.字节描述()
                                });
                            }
                            ItemData.当前持久.V--;
                            客户网络 网络连接8 = this.网络连接;
                            if (网络连接8 != null)
                            {
                                网络连接8.发送封包(new 玩家物品变动
                                {
                                    物品描述 = ItemData.字节描述()
                                });
                            }
                        }
                        else
                        {
                            new ItemObject(ItemData.物品模板, ItemData, this.当前地图, this.当前坐标, new HashSet<CharacterData>(), 0, false);
                            this.角色背包.Remove(ItemData.当前位置);
                            客户网络 网络连接9 = this.网络连接;
                            if (网络连接9 != null)
                            {
                                网络连接9.发送封包(new 玩家掉落装备
                                {
                                    物品描述 = ItemData.字节描述()
                                });
                            }
                            客户网络 网络连接10 = this.网络连接;
                            if (网络连接10 != null)
                            {
                                网络连接10.发送封包(new 删除玩家物品
                                {
                                    背包类型 = 1,
                                    物品位置 = ItemData.当前位置
                                });
                            }
                        }
                    }
                }
            }
        }


        // (get) Token: 0x06000888 RID: 2184 RVA: 0x00006E60 File Offset: 0x00005060
        public override string 对象名字
        {
            get
            {
                return this.CharacterData.CharName.V;
            }
        }


        // (get) Token: 0x06000889 RID: 2185 RVA: 0x00006E72 File Offset: 0x00005072
        public override int MapId
        {
            get
            {
                return this.CharacterData.数据索引.V;
            }
        }


        // (get) Token: 0x0600088A RID: 2186 RVA: 0x00006E84 File Offset: 0x00005084
        // (set) Token: 0x0600088B RID: 2187 RVA: 0x000453F4 File Offset: 0x000435F4
        public override int 当前体力
        {
            get
            {
                return this.CharacterData.当前血量.V;
            }
            set
            {
                value = Math.Min(this[GameObjectStats.MaxPhysicalStrength], Math.Max(0, value));
                if (this.当前体力 != value)
                {
                    this.CharacterData.当前血量.V = value;
                    base.发送封包(new 同步对象体力
                    {
                        对象编号 = this.MapId,
                        当前体力 = this.当前体力,
                        体力上限 = this[GameObjectStats.MaxPhysicalStrength]
                    });
                }
            }
        }


        // (get) Token: 0x0600088C RID: 2188 RVA: 0x00006E96 File Offset: 0x00005096
        // (set) Token: 0x0600088D RID: 2189 RVA: 0x00045464 File Offset: 0x00043664
        public override int 当前魔力
        {
            get
            {
                return this.CharacterData.当前蓝量.V;
            }
            set
            {
                value = Math.Min(this[GameObjectStats.MaxMagic2], Math.Max(0, value));
                if (this.当前魔力 != value)
                {
                    this.CharacterData.当前蓝量.V = Math.Max(0, value);
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 同步对象魔力
                    {
                        当前魔力 = this.当前魔力
                    });
                }
            }
        }


        // (get) Token: 0x0600088E RID: 2190 RVA: 0x00006EA8 File Offset: 0x000050A8
        // (set) Token: 0x0600088F RID: 2191 RVA: 0x00006EB5 File Offset: 0x000050B5
        public override byte 当前等级
        {
            get
            {
                return this.CharacterData.角色等级;
            }
            set
            {
                this.CharacterData.角色等级 = value;
            }
        }


        // (get) Token: 0x06000890 RID: 2192 RVA: 0x00006EC3 File Offset: 0x000050C3
        // (set) Token: 0x06000891 RID: 2193 RVA: 0x000454C8 File Offset: 0x000436C8
        public override Point 当前坐标
        {
            get
            {
                return this.CharacterData.当前坐标.V;
            }
            set
            {
                if (this.CharacterData.当前坐标.V != value)
                {
                    this.CharacterData.当前坐标.V = value;
                    MapInstance 当前地图 = this.当前地图;
                    bool? flag;
                    if (当前地图 == null)
                    {
                        flag = null;
                    }
                    else
                    {
                        MapAreas 复活区域 = 当前地图.复活区域;
                        flag = ((复活区域 != null) ? new bool?(复活区域.RangeCoords.Contains(this.当前坐标)) : null);
                    }
                    bool? flag2 = flag;
                    if (flag2.GetValueOrDefault())
                    {
                        this.重生地图 = this.当前地图.MapId;
                    }
                }
            }
        }


        // (get) Token: 0x06000892 RID: 2194 RVA: 0x00006167 File Offset: 0x00004367
        // (set) Token: 0x06000893 RID: 2195 RVA: 0x00045558 File Offset: 0x00043758
        public override MapInstance 当前地图
        {
            get
            {
                return base.当前地图;
            }
            set
            {
                if (this.当前地图 != value)
                {
                    MapInstance 当前地图 = base.当前地图;
                    if (当前地图 != null)
                    {
                        当前地图.移除对象(this);
                    }
                    base.当前地图 = value;
                    MapInstance 当前地图2 = base.当前地图;
                    if (当前地图2 != null)
                    {
                        当前地图2.添加对象(this);
                    }
                }
                if (this.CharacterData.当前地图.V != (int)value.地图模板.MapId)
                {
                    this.CharacterData.当前地图.V = (int)value.地图模板.MapId;
                    GuildData 所属行会 = this.所属行会;
                    if (所属行会 == null)
                    {
                        return;
                    }
                    所属行会.发送封包(new SyncMemberInfoPacket
                    {
                        对象编号 = this.MapId,
                        对象信息 = this.CharacterData.当前地图.V,
                        当前等级 = this.当前等级
                    });
                }
            }
        }


        // (get) Token: 0x06000894 RID: 2196 RVA: 0x00006ED5 File Offset: 0x000050D5
        // (set) Token: 0x06000895 RID: 2197 RVA: 0x00045614 File Offset: 0x00043814
        public override GameDirection 当前方向
        {
            get
            {
                return this.CharacterData.当前朝向.V;
            }
            set
            {
                if (this.CharacterData.当前朝向.V != value)
                {
                    this.CharacterData.当前朝向.V = value;
                    base.发送封包(new ObjectRotationDirectionPacket
                    {
                        对象编号 = this.MapId,
                        对象朝向 = (ushort)value
                    });
                }
            }
        }


        // (get) Token: 0x06000896 RID: 2198 RVA: 0x00002865 File Offset: 0x00000A65
        public override GameObjectType 对象类型
        {
            get
            {
                return GameObjectType.玩家;
            }
        }


        // (get) Token: 0x06000897 RID: 2199 RVA: 0x00002855 File Offset: 0x00000A55
        public override MonsterSize 对象体型
        {
            get
            {
                return MonsterSize.Single1x1;
            }
        }


        // (get) Token: 0x06000898 RID: 2200 RVA: 0x00006EE7 File Offset: 0x000050E7
        public override int 奔跑耗时
        {
            get
            {
                return (int)(base.奔跑速度 * 45);
            }
        }


        // (get) Token: 0x06000899 RID: 2201 RVA: 0x00006EF2 File Offset: 0x000050F2
        public override int 行走耗时
        {
            get
            {
                return (int)(base.行走速度 * 45);
            }
        }


        // (get) Token: 0x0600089A RID: 2202 RVA: 0x00006138 File Offset: 0x00004338
        // (set) Token: 0x0600089B RID: 2203 RVA: 0x00036440 File Offset: 0x00034640
        public override DateTime 忙碌时间
        {
            get
            {
                return base.忙碌时间;
            }
            set
            {
                if (base.忙碌时间 < value)
                {
                    this.硬直时间 = value;
                    base.忙碌时间 = value;
                }
            }
        }


        // (get) Token: 0x0600089C RID: 2204 RVA: 0x00006140 File Offset: 0x00004340
        // (set) Token: 0x0600089D RID: 2205 RVA: 0x00006EFD File Offset: 0x000050FD
        public override DateTime 硬直时间
        {
            get
            {
                return base.硬直时间;
            }
            set
            {
                if (base.硬直时间 < value)
                {
                    base.硬直时间 = value;
                    this.拾取时间 = value.AddMilliseconds(300.0);
                }
            }
        }


        // (get) Token: 0x0600089E RID: 2206 RVA: 0x00006F2A File Offset: 0x0000512A
        // (set) Token: 0x0600089F RID: 2207 RVA: 0x00006F32 File Offset: 0x00005132
        public override DateTime 行走时间
        {
            get
            {
                return base.行走时间;
            }
            set
            {
                if (base.行走时间 < value)
                {
                    base.行走时间 = value;
                }
            }
        }


        // (get) Token: 0x060008A0 RID: 2208 RVA: 0x00006F49 File Offset: 0x00005149
        // (set) Token: 0x060008A1 RID: 2209 RVA: 0x00006F51 File Offset: 0x00005151
        public override DateTime 奔跑时间
        {
            get
            {
                return base.奔跑时间;
            }
            set
            {
                if (base.奔跑时间 < value)
                {
                    base.奔跑时间 = value;
                }
            }
        }


        public override int this[GameObjectStats Stat]
        {
            get
            {
                return base[Stat];
            }
            set
            {
                if (base[Stat] != value)
                {
                    base[Stat] = value;
                    if ((byte)Stat <= 64)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new SyncPropChangePacket
                        {
                            StatId = (byte)Stat,
                            Value = value
                        });
                    }
                }
            }
        }


        // (get) Token: 0x060008A4 RID: 2212 RVA: 0x00006FA6 File Offset: 0x000051A6
        public override MonitorDictionary<ushort, BuffData> Buff列表
        {
            get
            {
                return this.CharacterData.BuffData;
            }
        }


        // (get) Token: 0x060008A5 RID: 2213 RVA: 0x00006FB3 File Offset: 0x000051B3
        public override MonitorDictionary<int, DateTime> 冷却记录
        {
            get
            {
                return this.CharacterData.冷却数据;
            }
        }


        // (get) Token: 0x060008A6 RID: 2214 RVA: 0x00006FC0 File Offset: 0x000051C0
        public MonitorDictionary<ushort, SkillData> MainSkills表
        {
            get
            {
                return this.CharacterData.SkillData;
            }
        }


        // (get) Token: 0x060008A7 RID: 2215 RVA: 0x00006FCD File Offset: 0x000051CD
        public int 最大负重
        {
            get
            {
                return this[GameObjectStats.最大负重];
            }
        }


        // (get) Token: 0x060008A8 RID: 2216 RVA: 0x00006FD7 File Offset: 0x000051D7
        public int 最大穿戴
        {
            get
            {
                return this[GameObjectStats.最大穿戴];
            }
        }


        // (get) Token: 0x060008A9 RID: 2217 RVA: 0x00006FE1 File Offset: 0x000051E1
        public int 最大腕力
        {
            get
            {
                return this[GameObjectStats.最大腕力];
            }
        }


        // (get) Token: 0x060008AA RID: 2218 RVA: 0x00045664 File Offset: 0x00043864
        public int 背包重量
        {
            get
            {
                int num = 0;
                foreach (ItemData ItemData in this.角色背包.Values.ToList<ItemData>())
                {
                    num += ((ItemData != null) ? ItemData.Weight : 0);
                }
                return num;
            }
        }


        // (get) Token: 0x060008AB RID: 2219 RVA: 0x000456D0 File Offset: 0x000438D0
        public int 装备重量
        {
            get
            {
                int num = 0;
                foreach (EquipmentData EquipmentData in this.角色装备.Values.ToList<EquipmentData>())
                {
                    num += ((EquipmentData == null || EquipmentData.物品类型 == ItemType.武器) ? 0 : EquipmentData.Weight);
                }
                return num;
            }
        }


        // (get) Token: 0x060008AC RID: 2220 RVA: 0x00006FEB File Offset: 0x000051EB
        // (set) Token: 0x060008AD RID: 2221 RVA: 0x00006FF8 File Offset: 0x000051F8
        public int 当前战力
        {
            get
            {
                return this.CharacterData.角色战力;
            }
            set
            {
                this.CharacterData.角色战力 = value;
            }
        }


        // (get) Token: 0x060008AE RID: 2222 RVA: 0x00007006 File Offset: 0x00005206
        // (set) Token: 0x060008AF RID: 2223 RVA: 0x00007013 File Offset: 0x00005213
        public int 当前经验
        {
            get
            {
                return this.CharacterData.角色经验;
            }
            set
            {
                this.CharacterData.角色经验 = value;
            }
        }


        // (get) Token: 0x060008B0 RID: 2224 RVA: 0x00007021 File Offset: 0x00005221
        // (set) Token: 0x060008B1 RID: 2225 RVA: 0x00045744 File Offset: 0x00043944
        public int 双倍经验
        {
            get
            {
                return this.CharacterData.双倍经验.V;
            }
            set
            {
                if (this.CharacterData.双倍经验.V != value)
                {
                    if (value > this.CharacterData.双倍经验.V)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 != null)
                        {
                            网络连接.发送封包(new DoubleExpChangePacket
                            {
                                双倍经验 = value
                            });
                        }
                    }
                    this.CharacterData.双倍经验.V = value;
                }
            }
        }


        // (get) Token: 0x060008B2 RID: 2226 RVA: 0x00007033 File Offset: 0x00005233
        public int 所需经验
        {
            get
            {
                return 角色成长.升级所需经验[this.当前等级];
            }
        }


        // (get) Token: 0x060008B3 RID: 2227 RVA: 0x00007045 File Offset: 0x00005245
        // (set) Token: 0x060008B4 RID: 2228 RVA: 0x00007052 File Offset: 0x00005252
        public int NumberGoldCoins
        {
            get
            {
                return this.CharacterData.NumberGoldCoins;
            }
            set
            {
                if (this.CharacterData.NumberGoldCoins != value)
                {
                    this.CharacterData.NumberGoldCoins = value;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 货币数量变动
                    {
                        CurrencyType = 1,
                        货币数量 = value
                    });
                }
            }
        }


        // (get) Token: 0x060008B5 RID: 2229 RVA: 0x00007091 File Offset: 0x00005291
        // (set) Token: 0x060008B6 RID: 2230 RVA: 0x0000709E File Offset: 0x0000529E
        public int 元宝数量
        {
            get
            {
                return this.CharacterData.元宝数量;
            }
            set
            {
                if (this.CharacterData.元宝数量 != value)
                {
                    this.CharacterData.元宝数量 = value;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 同步元宝数量
                    {
                        元宝数量 = value
                    });
                }
            }
        }


        // (get) Token: 0x060008B7 RID: 2231 RVA: 0x000070D6 File Offset: 0x000052D6
        // (set) Token: 0x060008B8 RID: 2232 RVA: 0x000070E3 File Offset: 0x000052E3
        public int 师门声望
        {
            get
            {
                return this.CharacterData.师门声望;
            }
            set
            {
                if (this.CharacterData.师门声望 != value)
                {
                    this.CharacterData.师门声望 = value;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 货币数量变动
                    {
                        CurrencyType = 6,
                        货币数量 = value
                    });
                }
            }
        }


        // (get) Token: 0x060008B9 RID: 2233 RVA: 0x00007122 File Offset: 0x00005322
        // (set) Token: 0x060008BA RID: 2234 RVA: 0x000457A8 File Offset: 0x000439A8
        public int PK值惩罚
        {
            get
            {
                return this.CharacterData.角色PK值;
            }
            set
            {
                value = Math.Max(0, value);
                if (this.CharacterData.角色PK值 == 0 && value > 0)
                {
                    this.减PK时间 = TimeSpan.FromMinutes(1.0);
                }
                if (this.CharacterData.角色PK值 != value)
                {
                    if (this.CharacterData.角色PK值 < 300 && value >= 300)
                    {
                        this.灰名时间 = TimeSpan.Zero;
                    }
                    base.发送封包(new 同步对象惩罚
                    {
                        对象编号 = this.MapId,
                        PK值惩罚 = (this.CharacterData.角色PK值 = value)
                    });
                }
            }
        }


        // (get) Token: 0x060008BB RID: 2235 RVA: 0x0000712F File Offset: 0x0000532F
        // (set) Token: 0x060008BC RID: 2236 RVA: 0x0000714F File Offset: 0x0000534F
        public int 重生地图
        {
            get
            {
                if (this.红名玩家)
                {
                    return 147;
                }
                return this.CharacterData.重生地图.V;
            }
            set
            {
                if (this.CharacterData.重生地图.V != value)
                {
                    this.CharacterData.重生地图.V = value;
                }
            }
        }


        // (get) Token: 0x060008BD RID: 2237 RVA: 0x00007175 File Offset: 0x00005375
        public bool 红名玩家
        {
            get
            {
                return this.PK值惩罚 >= 300;
            }
        }


        // (get) Token: 0x060008BE RID: 2238 RVA: 0x00007187 File Offset: 0x00005387
        public bool 灰名玩家
        {
            get
            {
                return this.灰名时间 > TimeSpan.Zero;
            }
        }


        // (get) Token: 0x060008BF RID: 2239 RVA: 0x00007199 File Offset: 0x00005399
        public bool 绑定地图
        {
            get
            {
                MapInstance 当前地图 = this.当前地图;
                return 当前地图 != null && 当前地图[this.当前坐标].Contains(this);
            }
        }


        // (get) Token: 0x060008C0 RID: 2240 RVA: 0x000071B8 File Offset: 0x000053B8
        // (set) Token: 0x060008C1 RID: 2241 RVA: 0x000071CA File Offset: 0x000053CA
        public byte 背包大小
        {
            get
            {
                return this.CharacterData.背包大小.V;
            }
            set
            {
                this.CharacterData.背包大小.V = value;
            }
        }


        // (get) Token: 0x060008C2 RID: 2242 RVA: 0x000071DD File Offset: 0x000053DD
        public byte 背包剩余
        {
            get
            {
                return (byte)((int)this.背包大小 - this.角色背包.Count);
            }
        }


        // (get) Token: 0x060008C3 RID: 2243 RVA: 0x000071F2 File Offset: 0x000053F2
        // (set) Token: 0x060008C4 RID: 2244 RVA: 0x00007204 File Offset: 0x00005404
        public byte 仓库大小
        {
            get
            {
                return this.CharacterData.仓库大小.V;
            }
            set
            {
                this.CharacterData.仓库大小.V = value;
            }
        }


        // (get) Token: 0x060008C5 RID: 2245 RVA: 0x00007217 File Offset: 0x00005417
        // (set) Token: 0x060008C6 RID: 2246 RVA: 0x0000721F File Offset: 0x0000541F
        public byte 宠物上限 { get; set; }


        // (get) Token: 0x060008C7 RID: 2247 RVA: 0x00007228 File Offset: 0x00005428
        public byte 宠物数量
        {
            get
            {
                return (byte)this.宠物列表.Count;
            }
        }


        // (get) Token: 0x060008C8 RID: 2248 RVA: 0x00007236 File Offset: 0x00005436
        public byte 师门参数
        {
            get
            {
                if (this.所属师门 != null)
                {
                    if (this.所属师门.师父编号 == this.MapId)
                    {
                        return 2;
                    }
                    return 1;
                }
                else
                {
                    if (this.当前等级 < 30)
                    {
                        return 0;
                    }
                    return 2;
                }
            }
        }


        // (get) Token: 0x060008C9 RID: 2249 RVA: 0x00007264 File Offset: 0x00005464
        // (set) Token: 0x060008CA RID: 2250 RVA: 0x00007276 File Offset: 0x00005476
        public byte 当前称号
        {
            get
            {
                return this.CharacterData.当前称号.V;
            }
            set
            {
                if (this.CharacterData.当前称号.V != value)
                {
                    this.CharacterData.当前称号.V = value;
                }
            }
        }


        // (get) Token: 0x060008CB RID: 2251 RVA: 0x0000729C File Offset: 0x0000549C
        // (set) Token: 0x060008CC RID: 2252 RVA: 0x000072AE File Offset: 0x000054AE
        public byte 本期特权
        {
            get
            {
                return this.CharacterData.本期特权.V;
            }
            set
            {
                if (this.CharacterData.本期特权.V != value)
                {
                    this.CharacterData.本期特权.V = value;
                }
            }
        }


        // (get) Token: 0x060008CD RID: 2253 RVA: 0x000072D4 File Offset: 0x000054D4
        // (set) Token: 0x060008CE RID: 2254 RVA: 0x000072E6 File Offset: 0x000054E6
        public byte 上期特权
        {
            get
            {
                return this.CharacterData.上期特权.V;
            }
            set
            {
                if (this.CharacterData.上期特权.V != value)
                {
                    this.CharacterData.上期特权.V = value;
                }
            }
        }


        // (get) Token: 0x060008CF RID: 2255 RVA: 0x0000730C File Offset: 0x0000550C
        // (set) Token: 0x060008D0 RID: 2256 RVA: 0x0000731E File Offset: 0x0000551E
        public byte 预定特权
        {
            get
            {
                return this.CharacterData.预定特权.V;
            }
            set
            {
                if (this.CharacterData.预定特权.V != value)
                {
                    this.CharacterData.预定特权.V = value;
                }
            }
        }


        // (get) Token: 0x060008D1 RID: 2257 RVA: 0x00007344 File Offset: 0x00005544
        // (set) Token: 0x060008D2 RID: 2258 RVA: 0x00007356 File Offset: 0x00005556
        public uint 本期记录
        {
            get
            {
                return this.CharacterData.本期记录.V;
            }
            set
            {
                if (this.CharacterData.本期记录.V != value)
                {
                    this.CharacterData.本期记录.V = value;
                }
            }
        }


        // (get) Token: 0x060008D3 RID: 2259 RVA: 0x0000737C File Offset: 0x0000557C
        // (set) Token: 0x060008D4 RID: 2260 RVA: 0x0000738E File Offset: 0x0000558E
        public uint 上期记录
        {
            get
            {
                return this.CharacterData.上期记录.V;
            }
            set
            {
                if (this.CharacterData.上期记录.V != value)
                {
                    this.CharacterData.上期记录.V = value;
                }
            }
        }


        // (get) Token: 0x060008D5 RID: 2261 RVA: 0x000073B4 File Offset: 0x000055B4
        // (set) Token: 0x060008D6 RID: 2262 RVA: 0x000073C6 File Offset: 0x000055C6
        public DateTime 本期日期
        {
            get
            {
                return this.CharacterData.本期日期.V;
            }
            set
            {
                if (this.CharacterData.本期日期.V != value)
                {
                    this.CharacterData.本期日期.V = value;
                }
            }
        }


        // (get) Token: 0x060008D7 RID: 2263 RVA: 0x000073F1 File Offset: 0x000055F1
        // (set) Token: 0x060008D8 RID: 2264 RVA: 0x00007403 File Offset: 0x00005603
        public DateTime 上期日期
        {
            get
            {
                return this.CharacterData.上期日期.V;
            }
            set
            {
                if (this.CharacterData.上期日期.V != value)
                {
                    this.CharacterData.上期日期.V = value;
                }
            }
        }


        // (get) Token: 0x060008D9 RID: 2265 RVA: 0x0000742E File Offset: 0x0000562E
        // (set) Token: 0x060008DA RID: 2266 RVA: 0x00045844 File Offset: 0x00043A44
        public TimeSpan 灰名时间
        {
            get
            {
                return this.CharacterData.灰名时间.V;
            }
            set
            {
                if (this.CharacterData.灰名时间.V <= TimeSpan.Zero && value > TimeSpan.Zero)
                {
                    base.发送封包(new 玩家名字变灰
                    {
                        对象编号 = this.MapId,
                        是否灰名 = true
                    });
                }
                else if (this.CharacterData.灰名时间.V > TimeSpan.Zero && value <= TimeSpan.Zero)
                {
                    base.发送封包(new 玩家名字变灰
                    {
                        对象编号 = this.MapId,
                        是否灰名 = false
                    });
                }
                if (this.CharacterData.灰名时间.V != value)
                {
                    this.CharacterData.灰名时间.V = value;
                }
            }
        }


        // (get) Token: 0x060008DB RID: 2267 RVA: 0x00007440 File Offset: 0x00005640
        // (set) Token: 0x060008DC RID: 2268 RVA: 0x0004590C File Offset: 0x00043B0C
        public TimeSpan 减PK时间
        {
            get
            {
                return this.CharacterData.减PK时间.V;
            }
            set
            {
                if (this.CharacterData.减PK时间.V > TimeSpan.Zero && value <= TimeSpan.Zero)
                {
                    this.PK值惩罚--;
                    this.CharacterData.减PK时间.V = TimeSpan.FromMinutes(1.0);
                    return;
                }
                if (this.CharacterData.减PK时间.V != value)
                {
                    this.CharacterData.减PK时间.V = value;
                }
            }
        }


        // (get) Token: 0x060008DD RID: 2269 RVA: 0x00007452 File Offset: 0x00005652
        public AccountData 所属账号
        {
            get
            {
                return this.CharacterData.所属账号.V;
            }
        }


        // (get) Token: 0x060008DE RID: 2270 RVA: 0x00007464 File Offset: 0x00005664
        // (set) Token: 0x060008DF RID: 2271 RVA: 0x00007476 File Offset: 0x00005676
        public GuildData 所属行会
        {
            get
            {
                return this.CharacterData.所属行会.V;
            }
            set
            {
                if (this.CharacterData.所属行会.V != value)
                {
                    this.CharacterData.所属行会.V = value;
                }
            }
        }


        // (get) Token: 0x060008E0 RID: 2272 RVA: 0x0000749C File Offset: 0x0000569C
        // (set) Token: 0x060008E1 RID: 2273 RVA: 0x000074AE File Offset: 0x000056AE
        public TeamData 所属队伍
        {
            get
            {
                return this.CharacterData.所属队伍.V;
            }
            set
            {
                if (this.CharacterData.所属队伍.V != value)
                {
                    this.CharacterData.所属队伍.V = value;
                }
            }
        }


        // (get) Token: 0x060008E2 RID: 2274 RVA: 0x000074D4 File Offset: 0x000056D4
        // (set) Token: 0x060008E3 RID: 2275 RVA: 0x000074E6 File Offset: 0x000056E6
        public TeacherData 所属师门
        {
            get
            {
                return this.CharacterData.所属师门.V;
            }
            set
            {
                if (this.CharacterData.所属师门.V != value)
                {
                    this.CharacterData.所属师门.V = value;
                }
            }
        }


        // (get) Token: 0x060008E4 RID: 2276 RVA: 0x0000750C File Offset: 0x0000570C
        // (set) Token: 0x060008E5 RID: 2277 RVA: 0x00045998 File Offset: 0x00043B98
        public AttackMode AttackMode
        {
            get
            {
                return this.CharacterData.AttackMode.V;
            }
            set
            {
                if (this.CharacterData.AttackMode.V != value)
                {
                    this.CharacterData.AttackMode.V = value;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 同步对战模式
                    {
                        对象编号 = this.MapId,
                        AttackMode = (byte)value
                    });
                }
            }
        }


        // (get) Token: 0x060008E6 RID: 2278 RVA: 0x0000751E File Offset: 0x0000571E
        // (set) Token: 0x060008E7 RID: 2279 RVA: 0x000459F4 File Offset: 0x00043BF4
        public PetMode PetMode
        {
            get
            {
                if (this.CharacterData.PetMode.V == PetMode.自动)
                {
                    this.CharacterData.PetMode.V = PetMode.Attack;
                    return PetMode.Attack;
                }
                return this.CharacterData.PetMode.V;
            }
            set
            {
                if (this.CharacterData.PetMode.V != value)
                {
                    this.CharacterData.PetMode.V = value;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 9473,
                        第一参数 = (int)value
                    });
                }
            }
        }


        // (get) Token: 0x060008E8 RID: 2280 RVA: 0x00045A4C File Offset: 0x00043C4C
        public MapInstance 复活地图
        {
            get
            {
                if (this.红名玩家)
                {
                    if (this.当前地图.MapId == 147)
                    {
                        return this.当前地图;
                    }
                    return MapGatewayProcess.分配地图(147);
                }
                else
                {
                    if (this.当前地图.MapId == this.重生地图)
                    {
                        return this.当前地图;
                    }
                    return MapGatewayProcess.分配地图(this.重生地图);
                }
            }
        }


        // (get) Token: 0x060008E9 RID: 2281 RVA: 0x00007555 File Offset: 0x00005755
        // (set) Token: 0x060008EA RID: 2282 RVA: 0x00007567 File Offset: 0x00005767
        public ObjectHairType 角色发型
        {
            get
            {
                return this.CharacterData.角色发型.V;
            }
            set
            {
                this.CharacterData.角色发型.V = value;
            }
        }


        // (get) Token: 0x060008EB RID: 2283 RVA: 0x0000757A File Offset: 0x0000577A
        // (set) Token: 0x060008EC RID: 2284 RVA: 0x0000758C File Offset: 0x0000578C
        public ObjectHairColorType 角色发色
        {
            get
            {
                return this.CharacterData.角色发色.V;
            }
            set
            {
                this.CharacterData.角色发色.V = value;
            }
        }


        // (get) Token: 0x060008ED RID: 2285 RVA: 0x0000759F File Offset: 0x0000579F
        // (set) Token: 0x060008EE RID: 2286 RVA: 0x000075B1 File Offset: 0x000057B1
        public ObjectFaceType 角色脸型
        {
            get
            {
                return this.CharacterData.角色脸型.V;
            }
            set
            {
                this.CharacterData.角色脸型.V = value;
            }
        }


        // (get) Token: 0x060008EF RID: 2287 RVA: 0x000075C4 File Offset: 0x000057C4
        public GameObjectGender 角色性别
        {
            get
            {
                return this.CharacterData.角色性别.V;
            }
        }


        // (get) Token: 0x060008F0 RID: 2288 RVA: 0x000075D6 File Offset: 0x000057D6
        public GameObjectRace 角色职业
        {
            get
            {
                return this.CharacterData.角色职业.V;
            }
        }


        // (get) Token: 0x060008F1 RID: 2289 RVA: 0x00045AAC File Offset: 0x00043CAC
        public ObjectNameColor 对象颜色
        {
            get
            {
                if (this.CharacterData.灰名时间.V > TimeSpan.Zero)
                {
                    return ObjectNameColor.灰名;
                }
                if (this.CharacterData.角色PK值 >= 800)
                {
                    return ObjectNameColor.深红;
                }
                if (this.CharacterData.角色PK值 >= 300)
                {
                    return ObjectNameColor.红名;
                }
                if (this.CharacterData.角色PK值 >= 99)
                {
                    return ObjectNameColor.黄名;
                }
                return ObjectNameColor.白名;
            }
        }


        // (get) Token: 0x060008F2 RID: 2290 RVA: 0x000075E8 File Offset: 0x000057E8
        public HashMonitor<PetData> PetData
        {
            get
            {
                return this.CharacterData.PetData;
            }
        }


        // (get) Token: 0x060008F3 RID: 2291 RVA: 0x000075F5 File Offset: 0x000057F5
        public HashMonitor<MailData> 未读邮件
        {
            get
            {
                return this.CharacterData.未读邮件;
            }
        }


        // (get) Token: 0x060008F4 RID: 2292 RVA: 0x00007602 File Offset: 0x00005802
        public HashMonitor<MailData> 全部邮件
        {
            get
            {
                return this.CharacterData.角色邮件;
            }
        }


        // (get) Token: 0x060008F5 RID: 2293 RVA: 0x0000760F File Offset: 0x0000580F
        public HashMonitor<CharacterData> 好友列表
        {
            get
            {
                return this.CharacterData.好友列表;
            }
        }


        // (get) Token: 0x060008F6 RID: 2294 RVA: 0x0000761C File Offset: 0x0000581C
        public HashMonitor<CharacterData> 粉丝列表
        {
            get
            {
                return this.CharacterData.粉丝列表;
            }
        }


        // (get) Token: 0x060008F7 RID: 2295 RVA: 0x00007629 File Offset: 0x00005829
        public HashMonitor<CharacterData> 偶像列表
        {
            get
            {
                return this.CharacterData.偶像列表;
            }
        }


        // (get) Token: 0x060008F8 RID: 2296 RVA: 0x00007636 File Offset: 0x00005836
        public HashMonitor<CharacterData> 仇人列表
        {
            get
            {
                return this.CharacterData.仇人列表;
            }
        }


        // (get) Token: 0x060008F9 RID: 2297 RVA: 0x00007643 File Offset: 0x00005843
        public HashMonitor<CharacterData> 仇恨列表
        {
            get
            {
                return this.CharacterData.仇恨列表;
            }
        }


        // (get) Token: 0x060008FA RID: 2298 RVA: 0x00007650 File Offset: 0x00005850
        public HashMonitor<CharacterData> 黑名单表
        {
            get
            {
                return this.CharacterData.黑名单表;
            }
        }


        // (get) Token: 0x060008FB RID: 2299 RVA: 0x0000765D File Offset: 0x0000585D
        public MonitorDictionary<byte, int> 剩余特权
        {
            get
            {
                return this.CharacterData.剩余特权;
            }
        }


        // (get) Token: 0x060008FC RID: 2300 RVA: 0x0000766A File Offset: 0x0000586A
        public MonitorDictionary<byte, SkillData> 快捷栏位
        {
            get
            {
                return this.CharacterData.快捷栏位;
            }
        }


        // (get) Token: 0x060008FD RID: 2301 RVA: 0x00007677 File Offset: 0x00005877
        public MonitorDictionary<byte, ItemData> 角色背包
        {
            get
            {
                return this.CharacterData.角色背包;
            }
        }


        // (get) Token: 0x060008FE RID: 2302 RVA: 0x00007684 File Offset: 0x00005884
        public MonitorDictionary<byte, ItemData> 角色仓库
        {
            get
            {
                return this.CharacterData.角色仓库;
            }
        }


        // (get) Token: 0x060008FF RID: 2303 RVA: 0x00007691 File Offset: 0x00005891
        public MonitorDictionary<byte, EquipmentData> 角色装备
        {
            get
            {
                return this.CharacterData.角色装备;
            }
        }


        // (get) Token: 0x06000900 RID: 2304 RVA: 0x0000769E File Offset: 0x0000589E
        public MonitorDictionary<byte, DateTime> 称号列表
        {
            get
            {
                return this.CharacterData.称号列表;
            }
        }


        public void 更新玩家战力()
        {
            int num = 0;
            foreach (int num2 in this.CombatBonus.Values.ToList<int>())
            {
                num += num2;
            }
            this.当前战力 = num;
        }


        public void 宠物死亡处理(PetObject 宠物)
        {
            this.PetData.Remove(宠物.PetData);
            this.宠物列表.Remove(宠物);
            if (this.宠物数量 == 0)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 9473
                });
            }
        }


        public void 玩家升级处理()
        {
            base.发送封包(new CharacterLevelUpPacket
            {
                对象编号 = this.MapId,
                对象等级 = this.当前等级
            });
            GuildData 所属行会 = this.所属行会;
            if (所属行会 != null)
            {
                所属行会.发送封包(new SyncMemberInfoPacket
                {
                    对象编号 = this.MapId,
                    对象信息 = this.当前地图.MapId,
                    当前等级 = this.当前等级
                });
            }
            this.CombatBonus[this] = (int)(this.当前等级 * 10);
            this.更新玩家战力();
            this.Stat加成[this] = 角色成长.获取数据(this.角色职业, this.当前等级);
            this.更新对象Stat();
            if (!this.对象死亡)
            {
                this.当前体力 = this[GameObjectStats.MaxPhysicalStrength];
                this.当前魔力 = this[GameObjectStats.MaxMagic2];
            }
            TeacherData 所属师门 = this.所属师门;
            if (所属师门 != null)
            {
                所属师门.发送封包(new SyncApprenticeshipLevelPacket
                {
                    对象编号 = this.MapId,
                    对象等级 = this.当前等级
                });
            }
            if (this.所属师门 != null && this.所属队伍 != null && this.所属师门.师父数据 != this.CharacterData && this.所属队伍.队伍成员.Contains(this.所属师门.师父数据))
            {
                MonitorDictionary<CharacterData, int> MonitorDictionary = this.所属师门.徒弟经验;
                CharacterData key = this.CharacterData;
                MonitorDictionary[key] += (int)((float)角色成长.升级所需经验[this.当前等级] * 0.05f);
                MonitorDictionary = this.所属师门.师父经验;
                key = this.CharacterData;
                MonitorDictionary[key] += (int)((float)角色成长.升级所需经验[this.当前等级] * 0.05f);
                if (this.本期特权 != 0)
                {
                    MonitorDictionary = this.所属师门.徒弟金币;
                    key = this.CharacterData;
                    MonitorDictionary[key] += (int)((float)角色成长.升级所需经验[this.当前等级] * 0.01f);
                    MonitorDictionary = this.所属师门.师父金币;
                    key = this.CharacterData;
                    MonitorDictionary[key] += (int)((float)角色成长.升级所需经验[this.当前等级] * 0.02f);
                    MonitorDictionary = this.所属师门.师父声望;
                    key = this.CharacterData;
                    MonitorDictionary[key] += (int)((float)角色成长.升级所需经验[this.当前等级] * 0.03f);
                }
            }
            if (this.当前等级 == 30 && this.所属师门 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new SyncTeacherInfoPacket
                    {
                        师门参数 = this.师门参数
                    });
                }
            }
            if (this.当前等级 >= 36 && this.所属师门 != null && this.所属师门.师父编号 != this.MapId)
            {
                this.提交出师申请();
            }
        }


        public void 玩家切换地图(MapInstance ToMapId, AreaType 指定区域, Point 坐标 = default(Point))
        {
            base.清空邻居时处理();
            base.解绑网格();
            客户网络 网络连接 = this.网络连接;
            if (网络连接 != null)
            {
                网络连接.发送封包(new 玩家离开场景());
            }
            this.当前坐标 = ((指定区域 == AreaType.未知区域) ? 坐标 : ToMapId.随机坐标(指定区域));
            if (this.当前地图.MapId != ToMapId.MapId)
            {
                bool CopyMap = this.当前地图.CopyMap;
                this.当前地图 = ToMapId;
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 != null)
                {
                    网络连接2.发送封包(new 玩家切换地图
                    {
                        MapId = this.当前地图.MapId,
                        路线编号 = this.当前地图.路线编号,
                        对象坐标 = this.当前坐标,
                        对象高度 = this.当前高度
                    });
                }
                if (!CopyMap)
                {
                    return;
                }
                using (List<PetObject>.Enumerator enumerator = this.宠物列表.GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        PetObject PetObject = enumerator.Current;
                        PetObject.宠物召回处理();
                    }
                    return;
                }
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 != null)
            {
                网络连接3.发送封包(new ObjectCharacterStopPacket
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度
                });
            }
            客户网络 网络连接4 = this.网络连接;
            if (网络连接4 != null)
            {
                网络连接4.发送封包(new 玩家进入场景
                {
                    MapId = this.当前地图.MapId,
                    当前坐标 = this.当前坐标,
                    当前高度 = this.当前高度,
                    路线编号 = this.当前地图.路线编号,
                    RouteStatus = this.当前地图.地图状态
                });
            }
            base.绑定网格();
            base.更新邻居时处理();
        }


        public void 玩家增加经验(MonsterObject 怪物, int 经验增加)
        {
            if (经验增加 > 0 && (this.当前等级 < Config.游戏OpenLevelCommand || this.当前经验 < this.所需经验))
            {
                int num = 经验增加;
                int num2 = 0;
                if (怪物 != null)
                {
                    num = (int)Math.Max(0.0, (double)num - Math.Round((double)((float)num * ComputingClass.收益衰减((int)this.当前等级, (int)怪物.当前等级))));
                    num = (int)(num * Config.怪物经验倍率);
                    if (this.当前等级 <= Config.NoobSupportCommand等级)
                    {
                        num *= 2;
                    }
                    num2 = Math.Min(this.双倍经验, num);
                }
                int num3 = num + num2;
                this.双倍经验 -= num2;
                if (num3 > 0)
                {
                    if ((this.当前经验 += num3) >= this.所需经验 && this.当前等级 < Config.游戏OpenLevelCommand)
                    {
                        while (this.当前经验 >= this.所需经验)
                        {
                            this.当前经验 -= this.所需经验;
                            this.当前等级 += 1;
                        }
                        this.玩家升级处理();
                    }
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new CharacterExpChangesPacket
                    {
                        经验增加 = num3,
                        今日增加 = 0,
                        经验上限 = 10000000,
                        双倍经验 = num2,
                        当前经验 = this.当前经验,
                        升级所需 = this.所需经验
                    });
                }
                return;
            }
        }


        public void 技能增加经验(ushort SkillId)
        {
            SkillData SkillData;
            if (!this.MainSkills表.TryGetValue(SkillId, out SkillData) || this.当前等级 < SkillData.升级等级)
            {
                return;
            }
            int num = MainProcess.RandomNumber.Next(4);
            if (num <= 0)
            {
                return;
            }
            EquipmentData EquipmentData;
            if (this.角色装备.TryGetValue(8, out EquipmentData) && EquipmentData.装备名字 == "技巧项链")
            {
                num += num;
            }
            DataMonitor<ushort> 技能经验 = SkillData.技能经验;
            if ((技能经验.V += (ushort)num) >= SkillData.升级经验)
            {
                DataMonitor<ushort> 技能经验2 = SkillData.技能经验;
                技能经验2.V -= SkillData.升级经验;
                DataMonitor<byte> 技能等级 = SkillData.技能等级;
                技能等级.V += 1;
                base.发送封包(new 玩家技能升级
                {
                    SkillId = SkillData.SkillId.V,
                    技能等级 = SkillData.技能等级.V
                });
                this.CombatBonus[SkillData] = SkillData.CombatBonus;
                this.更新玩家战力();
                this.Stat加成[SkillData] = SkillData.Stat加成;
                this.更新对象Stat();
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new SyncSkillLevelPacket
            {
                SkillId = SkillData.SkillId.V,
                当前经验 = SkillData.技能经验.V,
                当前等级 = SkillData.技能等级.V
            });
        }


        public bool LearnSkill(ushort SkillId)
        {
            if (this.MainSkills表.ContainsKey(SkillId))
            {
                return false;
            }
            this.MainSkills表[SkillId] = new SkillData(SkillId);
            客户网络 网络连接 = this.网络连接;
            if (网络连接 != null)
            {
                网络连接.发送封包(new CharacterLearningSkillPacket
                {
                    角色编号 = this.MapId,
                    SkillId = SkillId
                });
            }
            if (this.MainSkills表[SkillId].自动装配)
            {
                byte b = 0;
                while (b < 8)
                {
                    if (this.CharacterData.快捷栏位.ContainsKey(b))
                    {
                        b += 1;
                    }
                    else
                    {
                        this.CharacterData.快捷栏位[b] = this.MainSkills表[SkillId];
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            break;
                        }
                        网络连接2.发送封包(new CharacterDragSkillsPacket
                        {
                            技能栏位 = b,
                            Id = this.MainSkills表[SkillId].Id,
                            SkillId = this.MainSkills表[SkillId].SkillId.V,
                            技能等级 = this.MainSkills表[SkillId].技能等级.V
                        });
                        break;
                    }
                }
            }
            EquipmentData EquipmentData;
            if (this.角色装备.TryGetValue(0, out EquipmentData))
            {
                InscriptionSkill 第一铭文 = EquipmentData.第一铭文;
                ushort? num = (第一铭文 != null) ? new ushort?(第一铭文.SkillId) : null;
                int? num2 = (num != null) ? new int?((int)num.GetValueOrDefault()) : null;
                if (num2.GetValueOrDefault() == (int)SkillId & num2 != null)
                {
                    this.MainSkills表[SkillId].Id = EquipmentData.第一铭文.Id;
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 != null)
                    {
                        网络连接3.发送封包(new CharacterAssemblyInscriptionPacket
                        {
                            SkillId = SkillId,
                            Id = EquipmentData.第一铭文.Id
                        });
                    }
                }
                InscriptionSkill 第二铭文 = EquipmentData.第二铭文;
                num = ((第二铭文 != null) ? new ushort?(第二铭文.SkillId) : null);
                num2 = ((num != null) ? new int?((int)num.GetValueOrDefault()) : null);
                if (num2.GetValueOrDefault() == (int)SkillId & num2 != null)
                {
                    this.MainSkills表[SkillId].Id = EquipmentData.第二铭文.Id;
                    客户网络 网络连接4 = this.网络连接;
                    if (网络连接4 != null)
                    {
                        网络连接4.发送封包(new CharacterAssemblyInscriptionPacket
                        {
                            SkillId = SkillId,
                            Id = EquipmentData.第二铭文.Id
                        });
                    }
                }
            }
            foreach (ushort key in this.MainSkills表[SkillId].PassiveSkill)
            {
                this.PassiveSkill.Add(key, this.MainSkills表[SkillId]);
            }
            foreach (ushort 编号 in this.MainSkills表[SkillId].技能Buff)
            {
                base.添加Buff时处理(编号, this);
            }
            this.CombatBonus[this.MainSkills表[SkillId]] = this.MainSkills表[SkillId].CombatBonus;
            this.更新玩家战力();
            this.Stat加成[this.MainSkills表[SkillId]] = this.MainSkills表[SkillId].Stat加成;
            this.更新对象Stat();
            return true;
        }


        public void 玩家装卸铭文(ushort SkillId, byte Id)
        {
            SkillData SkillData;
            if (this.MainSkills表.TryGetValue(SkillId, out SkillData))
            {
                if (SkillData.Id == Id)
                {
                    return;
                }
                foreach (ushort key in SkillData.PassiveSkill)
                {
                    this.PassiveSkill.Remove(key);
                }
                foreach (ushort num in SkillData.技能Buff)
                {
                    if (this.Buff列表.ContainsKey(num))
                    {
                        base.删除Buff时处理(num);
                    }
                }
                foreach (PetObject PetObject in this.宠物列表.ToList<PetObject>())
                {
                    if (PetObject.绑定武器)
                    {
                        PetObject.ItSelf死亡处理(null, false);
                    }
                }
                SkillData.Id = Id;
                客户网络 网络连接 = this.网络连接;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new CharacterAssemblyInscriptionPacket
                    {
                        Id = Id,
                        SkillId = SkillId,
                        技能等级 = SkillData.技能等级.V
                    });
                }
                foreach (ushort key2 in SkillData.PassiveSkill)
                {
                    this.PassiveSkill.Add(key2, SkillData);
                }
                foreach (ushort 编号 in SkillData.技能Buff)
                {
                    base.添加Buff时处理(编号, this);
                }
                if (SkillData.SkillCount != 0)
                {
                    SkillData.剩余次数.V = 0;
                    SkillData.计数时间 = MainProcess.CurrentTime.AddMilliseconds((double)SkillData.PeriodCount);
                    this.冷却记录[(int)SkillId | 16777216] = MainProcess.CurrentTime.AddMilliseconds((double)SkillData.PeriodCount);
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 != null)
                    {
                        网络连接2.发送封包(new SyncSkillCountPacket
                        {
                            SkillId = SkillData.SkillId.V,
                            SkillCount = SkillData.剩余次数.V,
                            技能冷却 = (int)SkillData.PeriodCount
                        });
                    }
                }
                this.CombatBonus[SkillData] = SkillData.CombatBonus;
                this.更新玩家战力();
                this.Stat加成[SkillData] = SkillData.Stat加成;
                this.更新对象Stat();
            }
        }


        public void 玩家穿卸装备(EquipmentWearingParts itemType, EquipmentData 原有装备, EquipmentData 现有装备)
        {
            if (itemType == EquipmentWearingParts.武器 || itemType == EquipmentWearingParts.衣服 || itemType == EquipmentWearingParts.披风)
            {
                base.发送封包(new 同步角色外形
                {
                    对象编号 = this.MapId,
                    ItemType = (byte)itemType,
                    装备编号 = ((现有装备 != null) ? 现有装备.Id : 0),
                    升级次数 = ((byte)((现有装备 != null) ? 现有装备.升级次数.V : 0))
                });
            }
            if (原有装备 != null)
            {
                if (原有装备.物品类型 == ItemType.武器)
                {
                    foreach (BuffData BuffData in this.Buff列表.Values.ToList<BuffData>())
                    {
                        if (BuffData.绑定武器 && (BuffData.Buff来源 == null || BuffData.Buff来源.MapId == this.MapId))
                        {
                            base.删除Buff时处理(BuffData.Id.V);
                        }
                    }
                }
                if (原有装备.物品类型 == ItemType.武器)
                {
                    foreach (PetObject PetObject in this.宠物列表.ToList<PetObject>())
                    {
                        if (PetObject.绑定武器)
                        {
                            PetObject.ItSelf死亡处理(null, false);
                        }
                    }
                }
                if (原有装备.第一铭文 != null)
                {
                    this.玩家装卸铭文(原有装备.第一铭文.SkillId, 0);
                }
                if (原有装备.第二铭文 != null)
                {
                    this.玩家装卸铭文(原有装备.第二铭文.SkillId, 0);
                }
                this.CombatBonus.Remove(原有装备);
                this.Stat加成.Remove(原有装备);
            }
            if (现有装备 != null)
            {
                if (现有装备.第一铭文 != null)
                {
                    this.玩家装卸铭文(现有装备.第一铭文.SkillId, 现有装备.第一铭文.Id);
                }
                if (现有装备.第二铭文 != null)
                {
                    this.玩家装卸铭文(现有装备.第二铭文.SkillId, 现有装备.第二铭文.Id);
                }
                this.CombatBonus[现有装备] = 现有装备.装备战力;
                if (现有装备.当前持久.V > 0)
                {
                    this.Stat加成.Add(现有装备, 现有装备.装备Stat);
                }
            }
            if (原有装备 != null || 现有装备 != null)
            {
                this.更新玩家战力();
                this.更新对象Stat();
            }
        }


        public void 玩家诱惑目标(技能实例 技能, C_04_计算目标诱惑 参数, MapObject 诱惑目标)
        {
            if (诱惑目标 == null || 诱惑目标.对象死亡 || this.当前等级 + 2 < 诱惑目标.当前等级)
            {
                return;
            }
            if (!(诱惑目标 is MonsterObject) && !(诱惑目标 is PetObject))
            {
                return;
            }
            if (诱惑目标 is PetObject && (技能.技能等级 < 3 || this == (诱惑目标 as PetObject).宠物主人))
            {
                return;
            }
            SkillData SkillData;
            if (参数.检查铭文技能 && (!this.MainSkills表.TryGetValue((ushort)(参数.检查Id / 10), out SkillData) || (int)SkillData.Id != 参数.检查Id % 10))
            {
                return;
            }
            HashSet<string> 特定诱惑列表 = 参数.特定诱惑列表;
            bool flag;
            float num = (flag = (特定诱惑列表 != null && 特定诱惑列表.Contains(诱惑目标.对象名字))) ? 参数.特定诱惑概率 : 0f;
            float num2 = (诱惑目标 is MonsterObject) ? (诱惑目标 as MonsterObject).BaseTemptationProbability : (诱惑目标 as PetObject).BaseTemptationProbability;
            if ((num2 += num) <= 0f)
            {
                return;
            }
            byte[] 基础诱惑数量 = 参数.基础诱惑数量;
            int? num3 = (基础诱惑数量 != null) ? new int?(基础诱惑数量.Length) : null;
            int 技能等级 = (int)技能.技能等级;
            int num4 = (int)((num3.GetValueOrDefault() > 技能等级 & num3 != null) ? 参数.基础诱惑数量[(int)技能.技能等级] : 0);
            byte[] 初始宠物等级 = 参数.初始宠物等级;
            num3 = ((初始宠物等级 != null) ? new int?(初始宠物等级.Length) : null);
            技能等级 = (int)技能.技能等级;
            int num5 = (int)((num3.GetValueOrDefault() > 技能等级 & num3 != null) ? 参数.初始宠物等级[(int)技能.技能等级] : 0);
            byte 额外诱惑数量 = 参数.额外诱惑数量;
            float 额外诱惑概率 = 参数.额外诱惑概率;
            int 额外诱惑时长 = 参数.额外诱惑时长;
            float num6 = 0f;
            int num7 = 0;
            int num8 = 0;
            foreach (BuffData BuffData in this.Buff列表.Values)
            {
                if ((BuffData.Effect & BuffEffectType.TemptationBoost) != BuffEffectType.SkillSign)
                {
                    num6 += BuffData.Buff模板.TemptationIncreaseRate;
                    num7 += BuffData.Buff模板.TemptationIncreaseDuration;
                    num8 += (int)BuffData.Buff模板.TemptationIncreaseLevel;
                }
            }
            float num9 = (float)Math.Pow((this.当前等级 >= 诱惑目标.当前等级) ? 1.2 : 0.8, (double)ComputingClass.Value限制(0, Math.Abs((int)(诱惑目标.当前等级 - this.当前等级)), 2));
            if (ComputingClass.计算概率(num2 * num9 * (1f + 额外诱惑概率 + num6)))
            {
                if (诱惑目标.Buff列表.ContainsKey(参数.狂暴状态编号))
                {
                    if (this.宠物列表.Count < num4 + (int)额外诱惑数量)
                    {
                        int num10 = Math.Min(num5 + num8, 7);
                        int 宠物时长 = (int)Config.怪物诱惑时长 + 额外诱惑时长 + num7;
                        bool 绑定武器 = flag || num5 != 0 || 额外诱惑时长 != 0 || 额外诱惑概率 != 0f || this.宠物列表.Count >= num4;
                        MonsterObject MonsterObject = 诱惑目标 as MonsterObject;
                        PetObject PetObject = (MonsterObject != null) ? new PetObject(this, MonsterObject, (byte)Math.Max((int)MonsterObject.宠物等级, num10), 绑定武器, 宠物时长) : new PetObject(this, (PetObject)诱惑目标, (byte)num10, 绑定武器, 宠物时长);
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 != null)
                        {
                            网络连接.发送封包(new SyncPetLevelPacket
                            {
                                宠物编号 = PetObject.MapId,
                                宠物等级 = PetObject.宠物等级
                            });
                        }
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 9473,
                                第一参数 = (int)this.PetMode
                            });
                        }
                        this.PetData.Add(PetObject.PetData);
                        this.宠物列表.Add(PetObject);
                        return;
                    }
                }
                else
                {
                    诱惑目标.添加Buff时处理(参数.瘫痪状态编号, this);
                }
            }
        }


        public void 玩家瞬间移动(技能实例 技能, C_07_计算目标瞬移 参数)
        {
            if (ComputingClass.计算概率(参数.每级成功概率[(int)技能.技能等级]) && !(this.当前地图.随机传送(this.当前坐标) == default(Point)))
            {
                this.玩家切换地图(this.复活地图, AreaType.随机区域, default(Point));
            }
            else
            {
                base.添加Buff时处理(参数.瞬移失败提示, this);
                base.添加Buff时处理(参数.失败添加Buff, this);
            }
            if (参数.增加技能经验)
            {
                this.技能增加经验(参数.经验SkillId);
            }
        }


        public void 扣除护盾时间(int 技能伤害)
        {
            foreach (BuffData BuffData in this.Buff列表.Values.ToList<BuffData>())
            {
                if (BuffData.Buff分组 == 2535)
                {
                    if ((BuffData.剩余时间.V -= TimeSpan.FromSeconds((double)Math.Min(15f, (float)技能伤害 * 15f / 50f))) < TimeSpan.Zero)
                    {
                        base.删除Buff时处理(BuffData.Id.V);
                    }
                    else
                    {
                        base.发送封包(new ObjectStateChangePacket
                        {
                            对象编号 = this.MapId,
                            Id = BuffData.Id.V,
                            Buff索引 = (int)BuffData.Id.V,
                            当前层数 = BuffData.当前层数.V,
                            剩余时间 = (int)BuffData.剩余时间.V.TotalMilliseconds,
                            持续时间 = (int)BuffData.持续时间.V.TotalMilliseconds
                        });
                    }
                }
            }
        }


        public void 武器损失持久()
        {
            EquipmentData EquipmentData;
            if (this.角色装备.TryGetValue(0, out EquipmentData) && EquipmentData.当前持久.V > 0)
            {
                if (EquipmentData.当前持久.V <= 0)
                {
                    return;
                }
                if (this.本期特权 == 5 && EquipmentData.CanRepair)
                {
                    return;
                }
                if (this.本期特权 == 4 && ComputingClass.计算概率(0.5f))
                {
                    return;
                }
                if ((EquipmentData.当前持久.V = Math.Max(0, EquipmentData.当前持久.V - MainProcess.RandomNumber.Next(1, 6))) <= 0 && this.Stat加成.Remove(EquipmentData))
                {
                    this.更新对象Stat();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new EquipPermanentChangePacket
                {
                    装备容器 = EquipmentData.物品容器.V,
                    装备位置 = EquipmentData.物品位置.V,
                    当前持久 = EquipmentData.当前持久.V
                });
            }
        }


        public void 武器幸运损失()
        {
            EquipmentData EquipmentData;
            if (this.角色装备.TryGetValue(0, out EquipmentData) && EquipmentData.幸运等级.V > -9 && ComputingClass.计算概率(0.1f))
            {
                DataMonitor<sbyte> 幸运等级 = EquipmentData.幸运等级;
                幸运等级.V -= 1;
                this.网络连接.发送封包(new 玩家物品变动
                {
                    物品描述 = EquipmentData.字节描述()
                });
            }
        }


        public void 战具损失持久(int 损失持久)
        {
            EquipmentData EquipmentData;
            if (this.角色装备.TryGetValue(15, out EquipmentData))
            {
                if ((EquipmentData.当前持久.V -= 损失持久) <= 0)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new 删除玩家物品
                        {
                            背包类型 = EquipmentData.物品容器.V,
                            物品位置 = EquipmentData.物品位置.V
                        });
                    }
                    this.玩家穿卸装备(EquipmentWearingParts.战具, EquipmentData, null);
                    this.角色装备.Remove(EquipmentData.物品位置.V);
                    EquipmentData.Delete();
                    return;
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new EquipPermanentChangePacket
                {
                    装备容器 = EquipmentData.物品容器.V,
                    装备位置 = EquipmentData.物品位置.V,
                    当前持久 = EquipmentData.当前持久.V
                });
            }
        }


        public void 装备损失持久(int 损失持久)
        {
            损失持久 = Math.Min(10, 损失持久);
            foreach (EquipmentData EquipmentData in this.角色装备.Values)
            {
                if (EquipmentData.当前持久.V > 0 && (this.本期特权 != 5 || !EquipmentData.CanRepair) && (this.本期特权 != 4 || !ComputingClass.计算概率(0.5f)) && EquipmentData.PersistType == PersistentItemType.装备 && ComputingClass.计算概率((EquipmentData.物品类型 == ItemType.衣服) ? 1f : 0.1f))
                {
                    if ((EquipmentData.当前持久.V = Math.Max(0, EquipmentData.当前持久.V - 损失持久)) <= 0 && this.Stat加成.Remove(EquipmentData))
                    {
                        this.更新对象Stat();
                    }
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new EquipPermanentChangePacket
                        {
                            装备容器 = EquipmentData.物品容器.V,
                            装备位置 = EquipmentData.物品位置.V,
                            当前持久 = EquipmentData.当前持久.V
                        });
                    }
                }
            }
        }


        public void 玩家特权到期()
        {
            if (this.本期特权 == 3)
            {
                this.玩家称号到期(61);
            }
            else if (this.本期特权 == 4)
            {
                this.玩家称号到期(124);
            }
            else if (this.本期特权 == 5)
            {
                this.玩家称号到期(131);
            }
            this.上期特权 = this.本期特权;
            this.上期记录 = this.本期记录;
            this.上期日期 = this.本期日期;
            this.本期特权 = 0;
            this.本期记录 = 0U;
            this.本期日期 = default(DateTime);
            this.特权时间 = DateTime.MaxValue;
        }


        public void 玩家激活特权(byte 特权类型)
        {
            if (特权类型 == 3)
            {
                this.玩家获得称号(61);
            }
            else if (特权类型 == 4)
            {
                this.玩家获得称号(124);
            }
            else
            {
                if (特权类型 != 5)
                {
                    return;
                }
                this.玩家获得称号(131);
            }
            this.本期特权 = 特权类型;
            this.本期记录 = uint.MaxValue;
            this.本期日期 = MainProcess.CurrentTime;
            this.特权时间 = this.本期日期.AddDays(30.0);
        }


        public void 玩家称号到期(byte Id)
        {
            if (this.称号列表.Remove(Id))
            {
                if (this.当前称号 == Id)
                {
                    this.当前称号 = 0;
                    this.CombatBonus.Remove(Id);
                    this.更新玩家战力();
                    this.Stat加成.Remove(Id);
                    this.更新对象Stat();
                    base.发送封包(new 同步装配称号
                    {
                        对象编号 = this.MapId
                    });
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 玩家失去称号
                {
                    Id = Id
                });
            }
        }


        public void 玩家获得称号(byte Id)
        {
            GameTitle 游戏称号;
            if (GameTitle.DataSheet.TryGetValue(Id, out 游戏称号))
            {
                this.称号列表[Id] = MainProcess.CurrentTime.AddMinutes((double)游戏称号.EffectiveTime);
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 玩家获得称号
                {
                    Id = Id,
                    剩余时间 = (int)(this.称号列表[Id] - MainProcess.CurrentTime).TotalMinutes
                });
            }
        }


        public void 玩家获得仇恨(MapObject 对象)
        {
            foreach (PetObject PetObject in this.宠物列表.ToList<PetObject>())
            {
                if (PetObject.邻居列表.Contains(对象) && !对象.检查状态(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                {
                    PetObject.HateObject.添加仇恨(对象, default(DateTime), 0);
                }
            }
        }


        public bool 查找背包物品(int Id, out ItemData 物品)
        {
            for (byte b = 0; b < this.背包大小; b += 1)
            {
                if (this.角色背包.TryGetValue(b, out 物品) && 物品.Id == Id)
                {
                    return true;
                }
            }
            物品 = null;
            return false;
        }


        public bool 查找背包物品(int 所需总数, int Id, out List<ItemData> 物品列表)
        {
            物品列表 = new List<ItemData>();
            for (byte b = 0; b < this.背包大小; b += 1)
            {
                ItemData ItemData;
                if (this.角色背包.TryGetValue(b, out ItemData) && ItemData.Id == Id)
                {
                    物品列表.Add(ItemData);
                    if ((所需总数 -= ItemData.当前持久.V) <= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        public bool 查找背包物品(int 所需总数, HashSet<int> Id, out List<ItemData> 物品列表)
        {
            物品列表 = new List<ItemData>();
            for (byte b = 0; b < this.背包大小; b += 1)
            {
                ItemData ItemData;
                if (this.角色背包.TryGetValue(b, out ItemData) && Id.Contains(ItemData.Id))
                {
                    物品列表.Add(ItemData);
                    if ((所需总数 -= ItemData.当前持久.V) <= 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        public void ConsumeBackpackItem(int 消耗总数, ItemData 当前物品)
        {
            if ((当前物品.当前持久.V -= 消耗总数) <= 0)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new 删除玩家物品
                    {
                        背包类型 = 当前物品.物品容器.V,
                        物品位置 = 当前物品.物品位置.V
                    });
                }
                this.角色背包.Remove(当前物品.物品位置.V);
                当前物品.Delete();
                return;
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new 玩家物品变动
            {
                物品描述 = 当前物品.字节描述()
            });
        }


        public void 消耗背包物品(int 消耗总数, List<ItemData> 物品列表)
        {
            IOrderedEnumerable<ItemData> ItemDatas = from O in 物品列表
                                                     orderby O.物品位置
                                                     select O;
            foreach (ItemData ItemData in 物品列表)
            {
                int num = Math.Min(消耗总数, ItemData.当前持久.V);
                this.ConsumeBackpackItem(num, ItemData);
                if ((消耗总数 -= num) <= 0)
                {
                    break;
                }
            }
        }


        public void 玩家角色下线()
        {
            PlayerDeals PlayerDeals = this.当前交易;
            if (PlayerDeals != null)
            {
                PlayerDeals.结束交易();
            }
            TeamData 所属队伍 = this.所属队伍;
            if (所属队伍 != null)
            {
                所属队伍.发送封包(new 同步队员状态
                {
                    对象编号 = this.MapId,
                    状态编号 = 1
                });
            }
            GuildData 所属行会 = this.所属行会;
            if (所属行会 != null)
            {
                所属行会.发送封包(new SyncMemberInfoPacket
                {
                    对象编号 = this.MapId,
                    对象信息 = ComputingClass.TimeShift(MainProcess.CurrentTime)
                });
            }
            foreach (CharacterData CharacterData in this.粉丝列表)
            {
                客户网络 网络连接 = CharacterData.ActiveConnection;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new 好友上线下线
                    {
                        对象编号 = this.MapId,
                        对象名字 = this.对象名字,
                        对象职业 = (byte)this.角色职业,
                        对象性别 = (byte)this.角色性别,
                        上线下线 = 3
                    });
                }
            }
            foreach (CharacterData CharacterData2 in this.仇恨列表)
            {
                客户网络 网络连接2 = CharacterData2.ActiveConnection;
                if (网络连接2 != null)
                {
                    网络连接2.发送封包(new 好友上线下线
                    {
                        对象编号 = this.MapId,
                        对象名字 = this.对象名字,
                        对象职业 = (byte)this.角色职业,
                        对象性别 = (byte)this.角色性别,
                        上线下线 = 3
                    });
                }
            }
            foreach (PetObject PetObject in this.宠物列表.ToList<PetObject>())
            {
                PetObject.宠物沉睡处理();
            }
            foreach (BuffData BuffData in this.Buff列表.Values.ToList<BuffData>())
            {
                if (BuffData.下线消失)
                {
                    base.删除Buff时处理(BuffData.Id.V);
                }
            }
            this.CharacterData.角色下线();
            base.删除对象();
            this.当前地图.玩家列表.Remove(this);
        }


        public void 玩家进入场景()
        {
            客户网络 网络连接 = this.网络连接;
            if (网络连接 != null)
            {
                网络连接.发送封包(new ObjectCharacterStopPacket
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度
                });
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 != null)
            {
                网络连接2.发送封包(new 玩家进入场景
                {
                    MapId = this.MapId,
                    当前坐标 = this.当前坐标,
                    当前高度 = this.当前高度,
                    路线编号 = this.当前地图.路线编号,
                    RouteStatus = this.当前地图.地图状态
                });
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 != null)
            {
                网络连接3.发送封包(new ObjectComesIntoViewPacket
                {
                    出现方式 = 1,
                    对象编号 = this.MapId,
                    现身坐标 = this.当前坐标,
                    现身高度 = this.当前高度,
                    现身方向 = (ushort)this.当前方向,
                    现身姿态 = ((byte)(this.对象死亡 ? 13 : 1)),
                    体力比例 = (byte)(this.当前体力 * 100 / this[GameObjectStats.MaxPhysicalStrength])
                });
            }
            客户网络 网络连接4 = this.网络连接;
            if (网络连接4 != null)
            {
                网络连接4.发送封包(new 同步对象体力
                {
                    对象编号 = this.MapId,
                    当前体力 = this.当前体力,
                    体力上限 = this[GameObjectStats.MaxPhysicalStrength]
                });
            }
            客户网络 网络连接5 = this.网络连接;
            if (网络连接5 != null)
            {
                网络连接5.发送封包(new 同步对象魔力
                {
                    当前魔力 = this.当前魔力
                });
            }
            客户网络 网络连接6 = this.网络连接;
            if (网络连接6 != null)
            {
                网络连接6.发送封包(new 同步元宝数量
                {
                    元宝数量 = this.元宝数量
                });
            }
            客户网络 网络连接7 = this.网络连接;
            if (网络连接7 != null)
            {
                网络连接7.发送封包(new SyncCooldownListPacket
                {
                    字节描述 = this.全部冷却描述()
                });
            }
            客户网络 网络连接8 = this.网络连接;
            if (网络连接8 != null)
            {
                网络连接8.发送封包(new 同步状态列表
                {
                    字节数据 = this.全部Buff描述()
                });
            }
            客户网络 网络连接9 = this.网络连接;
            if (网络连接9 != null)
            {
                网络连接9.发送封包(new SwitchBattleStancePacket
                {
                    角色编号 = this.MapId
                });
            }
            base.绑定网格();
            base.更新邻居时处理();
            GameSkills 技能模板;
            if (GameSkills.DataSheet.TryGetValue("通用-玩家取出武器", out 技能模板))
            {
                new 技能实例(this, 技能模板, null, base.动作编号, this.当前地图, this.当前坐标, null, this.当前坐标, null, null, false);
            }
            if (this.宠物列表.Count != this.PetData.Count)
            {
                foreach (PetData PetData in this.PetData.ToList<PetData>())
                {
                    if (!(MainProcess.CurrentTime >= PetData.叛变时间.V) && Monsters.DataSheet.ContainsKey(PetData.宠物名字.V))
                    {
                        PetObject PetObject = new PetObject(this, PetData);
                        this.宠物列表.Add(PetObject);
                        客户网络 网络连接10 = this.网络连接;
                        if (网络连接10 != null)
                        {
                            网络连接10.发送封包(new SyncPetLevelPacket
                            {
                                宠物编号 = PetObject.MapId,
                                宠物等级 = PetObject.宠物等级
                            });
                        }
                        客户网络 网络连接11 = this.网络连接;
                        if (网络连接11 != null)
                        {
                            网络连接11.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 9473,
                                第一参数 = (int)this.PetMode
                            });
                        }
                    }
                    else
                    {
                        PetData.Delete();
                        this.PetData.Remove(PetData);
                    }
                }
            }
        }


        public void 玩家退出副本()
        {
            if (this.对象死亡)
            {
                this.玩家请求复活();
                return;
            }
            this.玩家切换地图(MapGatewayProcess.分配地图(this.重生地图), AreaType.复活区域, default(Point));
        }


        public void 玩家请求复活()
        {
            if (this.对象死亡)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new 玩家角色复活
                    {
                        对象编号 = this.MapId,
                        复活方式 = 3
                    });
                }
                this.当前体力 = (int)((float)this[GameObjectStats.MaxPhysicalStrength] * 0.3f);
                this.当前魔力 = (int)((float)this[GameObjectStats.MaxMagic2] * 0.3f);
                this.对象死亡 = false;
                this.阻塞网格 = true;
                if (this.当前地图 == MapGatewayProcess.沙城地图 && MapGatewayProcess.沙城节点 >= 2)
                {
                    if (this.所属行会 != null && this.所属行会 == SystemData.数据.占领行会.V)
                    {
                        this.玩家切换地图(this.当前地图, AreaType.未知区域, MapGatewayProcess.守方传送区域.RandomCoords);
                        return;
                    }
                    if (this.所属行会 != null && this.所属行会 == MapGatewayProcess.八卦坛激活行会)
                    {
                        this.玩家切换地图(this.当前地图, AreaType.未知区域, MapGatewayProcess.内城复活区域.RandomCoords);
                        return;
                    }
                    this.玩家切换地图(this.当前地图, AreaType.未知区域, MapGatewayProcess.外城复活区域.RandomCoords);
                    return;
                }
                else
                {
                    this.玩家切换地图(this.复活地图, this.红名玩家 ? AreaType.红名区域 : AreaType.复活区域, default(Point));
                }
            }
        }


        public void 玩家进入法阵(int TeleportGateNumber)
        {
            if (this.绑定地图)
            {
                if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
                {
                    TeleportGates 传送法阵;
                    GameMap 游戏地图;
                    if (!this.当前地图.法阵列表.TryGetValue((byte)TeleportGateNumber, out 传送法阵))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 775
                        });
                        return;
                    }
                    else if (base.网格距离(传送法阵.FromCoords) >= 8)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 4609
                        });
                        return;
                    }
                    else if (!GameMap.DataSheet.TryGetValue(传送法阵.ToMapId, out 游戏地图))
                    {
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 775
                        });
                        return;
                    }
                    else if (this.当前等级 < 游戏地图.MinLevel)
                    {
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 4624
                        });
                        return;
                    }
                    else
                    {
                        this.玩家切换地图((this.当前地图.MapId == (int)游戏地图.MapId) ? this.当前地图 : MapGatewayProcess.分配地图((int)游戏地图.MapId), AreaType.未知区域, 传送法阵.ToCoords);
                    }
                }
                else
                {
                    客户网络 网络连接5 = this.网络连接;
                    if (网络连接5 == null)
                    {
                        return;
                    }
                    网络连接5.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 769
                    });
                    return;
                }
            }
        }


        public void 玩家角色走动(Point 终点坐标)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.当前坐标 == 终点坐标)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new ObjectCharacterStopPacket
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度
                });
                return;
            }
            else if (!this.能否走动())
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new ObjectCharacterStopPacket
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度
                });
                return;
            }
            else
            {
                GameDirection GameDirection = ComputingClass.计算方向(this.当前坐标, 终点坐标);
                Point point = ComputingClass.前方坐标(this.当前坐标, GameDirection, 1);
                if (!this.当前地图.能否通行(point))
                {
                    if (this.当前方向 != (GameDirection = ComputingClass.计算方向(this.当前坐标, point)))
                    {
                        this.CharacterData.当前朝向.V = GameDirection;
                        base.发送封包(new ObjectRotationDirectionPacket
                        {
                            对象编号 = this.MapId,
                            对象朝向 = (ushort)GameDirection,
                            转向耗时 = 100
                        });
                    }
                    base.发送封包(new ObjectCharacterStopPacket
                    {
                        对象编号 = this.MapId,
                        对象坐标 = this.当前坐标,
                        对象高度 = this.当前高度
                    });
                    return;
                }
                this.行走时间 = MainProcess.CurrentTime.AddMilliseconds((double)this.行走耗时);
                this.忙碌时间 = MainProcess.CurrentTime.AddMilliseconds((double)this.行走耗时);
                if (this.当前方向 != (GameDirection = ComputingClass.计算方向(this.当前坐标, point)))
                {
                    this.CharacterData.当前朝向.V = GameDirection;
                    base.发送封包(new ObjectRotationDirectionPacket
                    {
                        对象编号 = this.MapId,
                        对象朝向 = (ushort)GameDirection,
                        转向耗时 = 100
                    });
                }
                base.发送封包(new ObjectCharacterWalkPacket
                {
                    对象编号 = this.MapId,
                    移动坐标 = point,
                    移动速度 = base.行走速度
                });
                base.ItSelf移动时处理(point);
                return;
            }
        }


        public void 玩家角色跑动(Point 终点坐标)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.当前坐标 == 终点坐标)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new ObjectCharacterStopPacket
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度
                });
                return;
            }
            else if (this.能否跑动())
            {
                GameDirection GameDirection = ComputingClass.计算方向(this.当前坐标, 终点坐标);
                Point point = ComputingClass.前方坐标(this.当前坐标, GameDirection, 1);
                Point point2 = ComputingClass.前方坐标(this.当前坐标, GameDirection, 2);
                if (!this.当前地图.能否通行(point))
                {
                    if (this.当前方向 != (GameDirection = ComputingClass.计算方向(this.当前坐标, point)))
                    {
                        this.CharacterData.当前朝向.V = GameDirection;
                        base.发送封包(new ObjectRotationDirectionPacket
                        {
                            对象编号 = this.MapId,
                            对象朝向 = (ushort)GameDirection,
                            转向耗时 = 100
                        });
                    }
                    base.发送封包(new ObjectCharacterStopPacket
                    {
                        对象编号 = this.MapId,
                        对象坐标 = this.当前坐标,
                        对象高度 = this.当前高度
                    });
                    return;
                }
                if (!this.当前地图.能否通行(point2))
                {
                    this.玩家角色走动(终点坐标);
                    return;
                }
                this.奔跑时间 = MainProcess.CurrentTime.AddMilliseconds((double)this.奔跑耗时);
                this.忙碌时间 = MainProcess.CurrentTime.AddMilliseconds((double)this.奔跑耗时);
                if (this.当前方向 != (GameDirection = ComputingClass.计算方向(this.当前坐标, point2)))
                {
                    this.CharacterData.当前朝向.V = GameDirection;
                    base.发送封包(new ObjectRotationDirectionPacket
                    {
                        对象编号 = this.MapId,
                        对象朝向 = (ushort)GameDirection,
                        转向耗时 = 100
                    });
                }
                base.发送封包(new ObjectCharacterRunPacket
                {
                    对象编号 = this.MapId,
                    移动坐标 = point2,
                    移动耗时 = base.奔跑速度
                });
                base.ItSelf移动时处理(point2);
                return;
            }
            else
            {
                if (this.能否走动())
                {
                    this.玩家角色走动(终点坐标);
                    return;
                }
                base.发送封包(new ObjectCharacterStopPacket
                {
                    对象编号 = this.MapId,
                    对象坐标 = this.当前坐标,
                    对象高度 = this.当前高度
                });
                return;
            }
        }


        public void 玩家角色转动(GameDirection 转动方向)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (!this.能否转动())
            {
                return;
            }
            this.当前方向 = 转动方向;
        }


        public void 玩家切换姿态()
        {
        }


        public void 玩家开关技能(ushort SkillId)
        {
            if (this.对象死亡)
            {
                return;
            }
            SkillData SkillData;
            if (!this.MainSkills表.TryGetValue(SkillId, out SkillData) && !this.PassiveSkill.TryGetValue(SkillId, out SkillData))
            {
                if (this != null)
                {
                    this.网络连接.尝试断开连接(new Exception("释放未学会的技能, 尝试断开连接."));
                    return;
                }
            }
            else
            {
                foreach (string key in SkillData.铭文模板.SwitchSkills.ToList<string>())
                {
                    GameSkills 游戏技能;
                    if (GameSkills.DataSheet.TryGetValue(key, out 游戏技能))
                    {
                        SkillData SkillData2;
                        if (this.MainSkills表.TryGetValue(游戏技能.BindingLevelId, out SkillData2))
                        {
                            int[] NeedConsumeMagic = 游戏技能.NeedConsumeMagic;
                            int? num = (NeedConsumeMagic != null) ? new int?(NeedConsumeMagic.Length) : null;
                            int v = (int)SkillData2.技能等级.V;
                            if (num.GetValueOrDefault() > v & num != null)
                            {
                                if (this.当前魔力 < 游戏技能.NeedConsumeMagic[(int)SkillData2.技能等级.V])
                                {
                                    continue;
                                }
                                this.当前魔力 -= 游戏技能.NeedConsumeMagic[(int)SkillData2.技能等级.V];
                            }
                        }
                        new 技能实例(this, 游戏技能, SkillData, 0, this.当前地图, this.当前坐标, this, this.当前坐标, null, null, false);
                        break;
                    }
                }
            }
        }


        public void 玩家释放技能(ushort SkillId, byte 动作编号, int 目标编号, Point 技能锚点)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            SkillData SkillData;
            if (!this.MainSkills表.TryGetValue(SkillId, out SkillData) && !this.PassiveSkill.TryGetValue(SkillId, out SkillData))
            {
                this.网络连接.尝试断开连接(new Exception(string.Format("错误操作: 玩家释放技能. 错误: 没有学会技能. SkillId:{0}", SkillId)));
                return;
            }
            DateTime dateTime;
            if (!this.冷却记录.TryGetValue((int)SkillId | 16777216, out dateTime) || !(MainProcess.CurrentTime < dateTime))
            {
                if (this.角色职业 == GameObjectRace.刺客)
                {
                    foreach (BuffData BuffData in this.Buff列表.Values.ToList<BuffData>())
                    {
                        if ((BuffData.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign && (BuffData.Buff模板.PlayerState & GameObjectState.StealthStatus) != GameObjectState.Normal)
                        {
                            base.移除Buff时处理(BuffData.Id.V);
                        }
                    }
                }
                MapObject MapObject;
                MapGatewayProcess.Objects.TryGetValue(目标编号, out MapObject);
                foreach (string key in SkillData.铭文模板.MainSkills.ToList<string>())
                {
                    int num = 0;
                    int num2 = 0;
                    List<ItemData> list = null;
                    GameSkills 游戏技能;
                    if (GameSkills.DataSheet.TryGetValue(key, out 游戏技能) && 游戏技能.OwnSkillId == SkillId)
                    {
                        DateTime dateTime2;
                        if (游戏技能.GroupId == 0 || !this.冷却记录.TryGetValue((int)(游戏技能.GroupId | 0), out dateTime2) || !(MainProcess.CurrentTime < dateTime2))
                        {
                            if (游戏技能.CheckOccupationalWeapons)
                            {
                                EquipmentData EquipmentData;
                                if (this.角色装备.TryGetValue(0, out EquipmentData))
                                {
                                    if (EquipmentData.NeedRace == this.角色职业)
                                    {
                                        goto IL_24E;
                                    }
                                }
                                break;
                            }
                        IL_24E:
                            if (!游戏技能.CheckSkillMarks || this.Buff列表.ContainsKey(游戏技能.SkillTagId))
                            {
                                if (游戏技能.CheckPassiveTags && this[GameObjectStats.SkillSign] != 1)
                                {
                                    break;
                                }
                                if (游戏技能.CheckSkillCount && SkillData.剩余次数.V <= 0)
                                {
                                    break;
                                }
                                if (!游戏技能.CheckBusyGreen || !(MainProcess.CurrentTime < this.忙碌时间))
                                {
                                    if (游戏技能.CheckStiff && MainProcess.CurrentTime < this.硬直时间)
                                    {
                                        客户网络 网络连接 = this.网络连接;
                                        if (网络连接 != null)
                                        {
                                            网络连接.发送封包(new AddedSkillCooldownPacket
                                            {
                                                冷却编号 = ((int)SkillId | 16777216),
                                                Cooldown = (int)(this.硬直时间 - MainProcess.CurrentTime).TotalMilliseconds
                                            });
                                        }
                                        客户网络 网络连接2 = this.网络连接;
                                        if (网络连接2 != null)
                                        {
                                            网络连接2.发送封包(new 技能释放完成
                                            {
                                                SkillId = SkillId,
                                                动作编号 = 动作编号
                                            });
                                        }
                                    }
                                    else
                                    {
                                        if (游戏技能.CalculateLuckyProbability || 游戏技能.CalculateTriggerProbability < 1f)
                                        {
                                            if (游戏技能.CalculateLuckyProbability)
                                            {
                                                if (!ComputingClass.计算概率(ComputingClass.计算幸运(this[GameObjectStats.幸运等级])))
                                                {
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                float num3 = 0f;
                                                if (游戏技能.StatBoostProbability != GameObjectStats.未知Stat)
                                                {
                                                    num3 = Math.Max(0f, (float)this[游戏技能.StatBoostProbability] * 游戏技能.StatBoostFactor);
                                                }
                                                if (!ComputingClass.计算概率(游戏技能.CalculateTriggerProbability + num3))
                                                {
                                                    continue;
                                                }
                                            }
                                        }
                                        SkillData SkillData2;
                                        BuffData BuffData2;
                                        BuffData BuffData3;
                                        if ((游戏技能.ValidateLearnedSkills == 0
                                            || (
                                            this.MainSkills表.TryGetValue(游戏技能.ValidateLearnedSkills, out SkillData2)
                                            && (
                                                游戏技能.VerficationSkillInscription == 0
                                                || 游戏技能.VerficationSkillInscription == SkillData2.Id
                                               )
                                            )
                                         ) && (游戏技能.VerifyPlayerBuff == 0 || (this.Buff列表.TryGetValue(游戏技能.VerifyPlayerBuff, out BuffData2) && (int)BuffData2.当前层数.V >= 游戏技能.PlayerBuffLayer)) && (游戏技能.VerifyTargetBuff == 0 || (MapObject != null && MapObject.Buff列表.TryGetValue(游戏技能.VerifyTargetBuff, out BuffData3) && (int)BuffData3.当前层数.V >= 游戏技能.TargetBuffLayers)) && (游戏技能.VerifyTargetType == SpecifyTargetType.None || (MapObject != null && MapObject.特定类型(this, 游戏技能.VerifyTargetType))))
                                        {
                                            SkillData SkillData3;
                                            if (this.MainSkills表.TryGetValue(游戏技能.BindingLevelId, out SkillData3))
                                            {
                                                int[] NeedConsumeMagic = 游戏技能.NeedConsumeMagic;
                                                int? num4 = (NeedConsumeMagic != null) ? new int?(NeedConsumeMagic.Length) : null;
                                                int v = (int)SkillData3.技能等级.V;
                                                if ((num4.GetValueOrDefault() > v & num4 != null) && this.当前魔力 < (num = 游戏技能.NeedConsumeMagic[(int)SkillData3.技能等级.V]))
                                                {
                                                    continue;
                                                }
                                            }
                                            HashSet<int> NeedConsumeItems = 游戏技能.NeedConsumeItems;
                                            if (NeedConsumeItems != null && NeedConsumeItems.Count != 0)
                                            {
                                                EquipmentData EquipmentData2;
                                                if (!this.角色装备.TryGetValue(15, out EquipmentData2) || EquipmentData2.当前持久.V < 游戏技能.GearDeductionPoints)
                                                {
                                                    if (!this.查找背包物品(游戏技能.NeedConsumeItemsQuantity, 游戏技能.NeedConsumeItems, out list))
                                                    {
                                                        continue;
                                                    }
                                                    num2 = 游戏技能.NeedConsumeItemsQuantity;
                                                }
                                                else
                                                {
                                                    list = new List<ItemData>
                                                    {
                                                        EquipmentData2
                                                    };
                                                    num2 = 游戏技能.GearDeductionPoints;
                                                }
                                            }
                                            if (num >= 0)
                                            {
                                                this.当前魔力 -= num;
                                            }
                                            if (list != null && list.Count == 1 && list[0].物品类型 == ItemType.战具)
                                            {
                                                this.战具损失持久(num2);
                                            }
                                            else if (list != null)
                                            {
                                                this.消耗背包物品(num2, list);
                                            }
                                            if (游戏技能.CheckPassiveTags && this[GameObjectStats.SkillSign] == 1)
                                            {
                                                this[GameObjectStats.SkillSign] = 0;
                                            }
                                            new 技能实例(this, 游戏技能, SkillData, 动作编号, this.当前地图, this.当前坐标, MapObject, 技能锚点, null, null, false);
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    客户网络 网络连接3 = this.网络连接;
                                    if (网络连接3 != null)
                                    {
                                        网络连接3.发送封包(new AddedSkillCooldownPacket
                                        {
                                            冷却编号 = ((int)SkillId | 16777216),
                                            Cooldown = (int)(this.忙碌时间 - MainProcess.CurrentTime).TotalMilliseconds
                                        });
                                    }
                                    客户网络 网络连接4 = this.网络连接;
                                    if (网络连接4 == null)
                                    {
                                        break;
                                    }
                                    网络连接4.发送封包(new 技能释放完成
                                    {
                                        SkillId = SkillId,
                                        动作编号 = 动作编号
                                    });
                                    break;
                                }
                            }
                        }
                        else
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 != null)
                            {
                                网络连接5.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = ((int)SkillId | 16777216),
                                    Cooldown = (int)(dateTime2 - MainProcess.CurrentTime).TotalMilliseconds
                                });
                            }
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                break;
                            }
                            网络连接6.发送封包(new 技能释放完成
                            {
                                SkillId = SkillId,
                                动作编号 = 动作编号
                            });
                            break;
                        }
                    }
                }
                return;
            }
            客户网络 网络连接7 = this.网络连接;
            if (网络连接7 != null)
            {
                网络连接7.发送封包(new AddedSkillCooldownPacket
                {
                    冷却编号 = ((int)SkillId | 16777216),
                    Cooldown = (int)(dateTime - MainProcess.CurrentTime).TotalMilliseconds
                });
            }
            客户网络 网络连接8 = this.网络连接;
            if (网络连接8 != null)
            {
                网络连接8.发送封包(new 技能释放完成
                {
                    SkillId = SkillId,
                    动作编号 = 动作编号
                });
            }
            客户网络 网络连接9 = this.网络连接;
            if (网络连接9 == null)
            {
                return;
            }
            网络连接9.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 1281,
                第一参数 = (int)SkillId,
                第二参数 = (int)动作编号
            });
        }


        public void 更改AttackMode(AttackMode 模式)
        {
            this.AttackMode = 模式;
        }


        public void 更改PetMode(PetMode 模式)
        {
            if (this.宠物数量 == 0)
            {
                return;
            }
            if (this.PetMode == PetMode.休息 && (模式 == PetMode.自动 || 模式 == PetMode.Attack))
            {
                foreach (PetObject PetObject in this.宠物列表.ToList<PetObject>())
                {
                    PetObject.HateObject.仇恨列表.Clear();
                }
                this.PetMode = PetMode.Attack;
                return;
            }
            if (this.PetMode == PetMode.Attack && (模式 == PetMode.自动 || 模式 == PetMode.休息))
            {
                this.PetMode = PetMode.休息;
            }
        }


        public void EquipSkill(byte 技能栏位, ushort SkillId)
        {
            if (技能栏位 <= 7 || 技能栏位 >= 32)
            {
                return;
            }
            SkillData SkillData;
            if (!this.MainSkills表.TryGetValue(SkillId, out SkillData))
            {
                SkillData SkillData2;
                if (this.快捷栏位.TryGetValue(技能栏位, out SkillData2))
                {
                    this.快捷栏位.Remove(技能栏位);
                    SkillData2.快捷栏位.V = 100;
                }
                return;
            }
            if (SkillData.自动装配)
            {
                return;
            }
            if (SkillData.快捷栏位.V == 技能栏位)
            {
                return;
            }
            this.快捷栏位.Remove(SkillData.快捷栏位.V);
            SkillData.快捷栏位.V = 100;
            SkillData SkillData3;
            if (this.快捷栏位.TryGetValue(技能栏位, out SkillData3) && SkillData3 != null)
            {
                SkillData3.快捷栏位.V = 100;
            }
            this.快捷栏位[技能栏位] = SkillData;
            SkillData.快捷栏位.V = 技能栏位;
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new CharacterDragSkillsPacket
            {
                技能栏位 = 技能栏位,
                Id = SkillData.Id,
                SkillId = SkillData.SkillId.V,
                技能等级 = SkillData.技能等级.V
            });
        }


        public void 玩家选中对象(int 对象编号)
        {
            MapObject MapObject;
            if (MapGatewayProcess.Objects.TryGetValue(对象编号, out MapObject))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new 玩家选中目标
                    {
                        角色编号 = this.MapId,
                        目标编号 = MapObject.MapId
                    });
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new SelectTargetDetailsPacket
                {
                    对象编号 = MapObject.MapId,
                    当前体力 = MapObject.当前体力,
                    当前魔力 = MapObject.当前魔力,
                    MaxPhysicalStrength = MapObject[GameObjectStats.MaxPhysicalStrength],
                    MaxMagic2 = MapObject[GameObjectStats.MaxMagic2],
                    Buff描述 = MapObject.对象Buff详述()
                });
            }
        }


        public void 开始Npcc对话(int 对象编号)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (!MapGatewayProcess.守卫对象表.TryGetValue(对象编号, out this.对话守卫))
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 开始Npcc对话. 错误: 没有找到对象."));
                return;
            }
            if (this.当前地图 != this.对话守卫.当前地图)
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 开始Npcc对话. 错误: 跨越地图对话."));
                return;
            }
            if (base.网格距离(this.对话守卫) > 12)
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 开始Npcc对话. 错误: 超长距离对话."));
                return;
            }
            if (NpcDialogs.DataSheet.ContainsKey((int)this.对话守卫.模板编号 * 100000))
            {
                this.打开商店 = this.对话守卫.StoreId;
                this.打开界面 = this.对话守卫.InterfaceCode;
                this.对话超时 = MainProcess.CurrentTime.AddSeconds(30.0);
                this.对话页面 = (int)this.对话守卫.模板编号 * 100000;
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 同步交互结果
                {
                    对象编号 = this.对话守卫.MapId,
                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                });
            }
        }


        public void 继续Npcc对话(int 选项编号)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.对话守卫 == null)
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 继续Npcc对话.  错误: 没有选中守卫."));
                return;
            }
            if (this.当前地图 != this.对话守卫.当前地图)
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 开始Npcc对话. 错误: 跨越地图对话."));
                return;
            }
            if (base.网格距离(this.对话守卫) > 12)
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 开始Npcc对话. 错误: 超长距离对话."));
                return;
            }
            if (!(MainProcess.CurrentTime > this.对话超时))
            {
                this.对话超时 = MainProcess.CurrentTime.AddSeconds(30.0);
                int num = this.对话页面;
                if (num <= 619403000)
                {
                    if (num <= 612604000)
                    {
                        if (num <= 611400000)
                        {
                            if (num <= 479600000)
                            {
                                if (num != 479500000)
                                {
                                    if (num != 479600000)
                                    {
                                        return;
                                    }
                                    if (选项编号 == 1)
                                    {
                                        this.玩家切换地图(MapGatewayProcess.分配地图(this.重生地图), AreaType.复活区域, default(Point));
                                        return;
                                    }
                                }
                                else if (选项编号 == 1)
                                {
                                    if (MainProcess.CurrentTime.Hour != (int)Config.武斗场时间一 && MainProcess.CurrentTime.Hour != (int)Config.武斗场时间二)
                                    {
                                        this.对话页面 = 479501000;
                                        客户网络 网络连接 = this.网络连接;
                                        if (网络连接 == null)
                                        {
                                            return;
                                        }
                                        网络连接.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", Config.武斗场时间一, Config.武斗场时间二))
                                        });
                                        return;
                                    }
                                    else if (MainProcess.CurrentTime.Hour == this.CharacterData.武斗日期.V.Hour)
                                    {
                                        this.对话页面 = 479502000;
                                        客户网络 网络连接2 = this.网络连接;
                                        if (网络连接2 == null)
                                        {
                                            return;
                                        }
                                        网络连接2.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        if (this.当前等级 < 25)
                                        {
                                            this.对话页面 = 711900001;
                                            客户网络 网络连接3 = this.网络连接;
                                            if (网络连接3 != null)
                                            {
                                                网络连接3.发送封包(new 同步交互结果
                                                {
                                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", 25)),
                                                    对象编号 = this.对话守卫.MapId
                                                });
                                            }
                                        }
                                        if (this.NumberGoldCoins >= 50000)
                                        {
                                            this.NumberGoldCoins -= 50000;
                                            this.CharacterData.武斗日期.V = MainProcess.CurrentTime;
                                            this.玩家切换地图((this.当前地图.MapId == 183) ? this.当前地图 : MapGatewayProcess.分配地图(183), AreaType.传送区域, default(Point));
                                            return;
                                        }
                                        this.对话页面 = 479503000;
                                        客户网络 网络连接4 = this.网络连接;
                                        if (网络连接4 == null)
                                        {
                                            return;
                                        }
                                        网络连接4.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", 50000))
                                        });
                                        return;
                                    }
                                }
                            }
                            else if (num != 611300000)
                            {
                                if (num != 611400000)
                                {
                                    return;
                                }
                                int num2 = 30;
                                int num3 = 100000;
                                int num4 = 223;
                                if (选项编号 == 1)
                                {
                                    if ((int)this.当前等级 < num2)
                                    {
                                        this.对话页面 = 711900001;
                                        客户网络 网络连接5 = this.网络连接;
                                        if (网络连接5 == null)
                                        {
                                            return;
                                        }
                                        网络连接5.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num2)),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        if (this.NumberGoldCoins >= num3)
                                        {
                                            this.NumberGoldCoins -= num3;
                                            this.玩家切换地图((this.当前地图.MapId == num4) ? this.当前地图 : MapGatewayProcess.分配地图(num4), AreaType.传送区域, default(Point));
                                            return;
                                        }
                                        this.对话页面 = 711900002;
                                        客户网络 网络连接6 = this.网络连接;
                                        if (网络连接6 == null)
                                        {
                                            return;
                                        }
                                        网络连接6.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num3)),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                }
                            }
                            else
                            {
                                int num5 = 0;
                                int num6 = 0;
                                int num7 = 147;
                                if (选项编号 == 1)
                                {
                                    if ((int)this.当前等级 < num5)
                                    {
                                        this.对话页面 = 711900001;
                                        客户网络 网络连接7 = this.网络连接;
                                        if (网络连接7 == null)
                                        {
                                            return;
                                        }
                                        网络连接7.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num5)),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        if (this.NumberGoldCoins >= num6)
                                        {
                                            this.NumberGoldCoins -= num6;
                                            this.玩家切换地图((this.当前地图.MapId == num7) ? this.当前地图 : MapGatewayProcess.分配地图(num7), AreaType.复活区域, default(Point));
                                            return;
                                        }
                                        this.对话页面 = 711900002;
                                        客户网络 网络连接8 = this.网络连接;
                                        if (网络连接8 == null)
                                        {
                                            return;
                                        }
                                        网络连接8.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num6)),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                }
                            }
                        }
                        else if (num <= 612600000)
                        {
                            if (num != 612300000)
                            {
                                if (num != 612600000)
                                {
                                    return;
                                }
                                if (选项编号 == 1)
                                {
                                    EquipmentData EquipmentData;
                                    if (!this.角色装备.TryGetValue(0, out EquipmentData))
                                    {
                                        this.对话页面 = 612603000;
                                        客户网络 网络连接9 = this.网络连接;
                                        if (网络连接9 == null)
                                        {
                                            return;
                                        }
                                        网络连接9.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        this.对话页面 = 612604000;
                                        this.重铸部位 = 0;
                                        客户网络 网络连接10 = this.网络连接;
                                        if (网络连接10 == null)
                                        {
                                            return;
                                        }
                                        网络连接10.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                        });
                                        return;
                                    }
                                }
                                else if (选项编号 == 2)
                                {
                                    this.对话页面 = 612601000;
                                    客户网络 网络连接11 = this.网络连接;
                                    if (网络连接11 == null)
                                    {
                                        return;
                                    }
                                    网络连接11.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else if (选项编号 == 3)
                                {
                                    this.对话页面 = 612602000;
                                    客户网络 网络连接12 = this.网络连接;
                                    if (网络连接12 == null)
                                    {
                                        return;
                                    }
                                    网络连接12.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                            }
                            else if (MapGatewayProcess.沙城节点 >= 2 && this.所属行会 != null && this.所属行会 == MapGatewayProcess.八卦坛激活行会)
                            {
                                this.玩家切换地图(MapGatewayProcess.沙城地图, AreaType.未知区域, MapGatewayProcess.皇宫随机区域.RandomCoords);
                                return;
                            }
                        }
                        else if (num != 612601000)
                        {
                            if (num != 612602000)
                            {
                                if (num != 612604000)
                                {
                                    return;
                                }
                                if (选项编号 == 1)
                                {
                                    EquipmentData EquipmentData2;
                                    if (!this.角色装备.TryGetValue(this.重铸部位, out EquipmentData2))
                                    {
                                        this.对话页面 = 612603000;
                                        客户网络 网络连接13 = this.网络连接;
                                        if (网络连接13 == null)
                                        {
                                            return;
                                        }
                                        网络连接13.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        int num8 = 500000;
                                        int num9 = 1;
                                        int 重铸所需灵气 = EquipmentData2.重铸所需灵气;
                                        List<ItemData> 物品列表;
                                        if (this.NumberGoldCoins < 500000)
                                        {
                                            this.对话页面 = 612605000;
                                            客户网络 网络连接14 = this.网络连接;
                                            if (网络连接14 == null)
                                            {
                                                return;
                                            }
                                            网络连接14.发送封包(new 同步交互结果
                                            {
                                                对象编号 = this.对话守卫.MapId,
                                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:[{0}] 个 [{1}]><#P1:{2}>", num9, GameItems.DataSheet[重铸所需灵气].Name, num8 / 10000))
                                            });
                                            return;
                                        }
                                        else if (!this.查找背包物品(num9, 重铸所需灵气, out 物品列表))
                                        {
                                            this.对话页面 = 612605000;
                                            客户网络 网络连接15 = this.网络连接;
                                            if (网络连接15 == null)
                                            {
                                                return;
                                            }
                                            网络连接15.发送封包(new 同步交互结果
                                            {
                                                对象编号 = this.对话守卫.MapId,
                                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:[{0}] 个 [{1}]><#P1:{2}>", num9, GameItems.DataSheet[重铸所需灵气].Name, num8 / 10000))
                                            });
                                            return;
                                        }
                                        else
                                        {
                                            this.NumberGoldCoins -= num8;
                                            this.消耗背包物品(num9, 物品列表);
                                            EquipmentData2.随机Stat.SetValue(EquipmentStats.GenerateStats(EquipmentData2.物品类型, true));
                                            客户网络 网络连接16 = this.网络连接;
                                            if (网络连接16 != null)
                                            {
                                                网络连接16.发送封包(new 玩家物品变动
                                                {
                                                    物品描述 = EquipmentData2.字节描述()
                                                });
                                            }
                                            this.Stat加成[EquipmentData2] = EquipmentData2.装备Stat;
                                            this.更新对象Stat();
                                            this.对话页面 = 612606000;
                                            客户网络 网络连接17 = this.网络连接;
                                            if (网络连接17 == null)
                                            {
                                                return;
                                            }
                                            网络连接17.发送封包(new 同步交互结果
                                            {
                                                对象编号 = this.对话守卫.MapId,
                                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, "<#P1:" + EquipmentData2.StatDescription + ">")
                                            });
                                            return;
                                        }
                                    }
                                }
                            }
                            else if (选项编号 == 1)
                            {
                                EquipmentData EquipmentData3;
                                if (!this.角色装备.TryGetValue(9, out EquipmentData3))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接18 = this.网络连接;
                                    if (网络连接18 == null)
                                    {
                                        return;
                                    }
                                    网络连接18.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 9;
                                    客户网络 网络连接19 = this.网络连接;
                                    if (网络连接19 == null)
                                    {
                                        return;
                                    }
                                    网络连接19.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                            else if (选项编号 == 2)
                            {
                                EquipmentData EquipmentData4;
                                if (!this.角色装备.TryGetValue(10, out EquipmentData4))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接20 = this.网络连接;
                                    if (网络连接20 == null)
                                    {
                                        return;
                                    }
                                    网络连接20.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 10;
                                    客户网络 网络连接21 = this.网络连接;
                                    if (网络连接21 == null)
                                    {
                                        return;
                                    }
                                    网络连接21.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                            else if (选项编号 == 3)
                            {
                                EquipmentData EquipmentData5;
                                if (!this.角色装备.TryGetValue(11, out EquipmentData5))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接22 = this.网络连接;
                                    if (网络连接22 == null)
                                    {
                                        return;
                                    }
                                    网络连接22.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 11;
                                    客户网络 网络连接23 = this.网络连接;
                                    if (网络连接23 == null)
                                    {
                                        return;
                                    }
                                    网络连接23.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                            else if (选项编号 == 4)
                            {
                                EquipmentData EquipmentData6;
                                if (!this.角色装备.TryGetValue(12, out EquipmentData6))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接24 = this.网络连接;
                                    if (网络连接24 == null)
                                    {
                                        return;
                                    }
                                    网络连接24.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 12;
                                    客户网络 网络连接25 = this.网络连接;
                                    if (网络连接25 == null)
                                    {
                                        return;
                                    }
                                    网络连接25.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                            else if (选项编号 == 5)
                            {
                                EquipmentData EquipmentData7;
                                if (!this.角色装备.TryGetValue(8, out EquipmentData7))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接26 = this.网络连接;
                                    if (网络连接26 == null)
                                    {
                                        return;
                                    }
                                    网络连接26.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 8;
                                    客户网络 网络连接27 = this.网络连接;
                                    if (网络连接27 == null)
                                    {
                                        return;
                                    }
                                    网络连接27.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                            else if (选项编号 == 6)
                            {
                                EquipmentData EquipmentData8;
                                if (!this.角色装备.TryGetValue(14, out EquipmentData8))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接28 = this.网络连接;
                                    if (网络连接28 == null)
                                    {
                                        return;
                                    }
                                    网络连接28.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 14;
                                    客户网络 网络连接29 = this.网络连接;
                                    if (网络连接29 == null)
                                    {
                                        return;
                                    }
                                    网络连接29.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                            else if (选项编号 == 7)
                            {
                                EquipmentData EquipmentData9;
                                if (!this.角色装备.TryGetValue(13, out EquipmentData9))
                                {
                                    this.对话页面 = 612603000;
                                    客户网络 网络连接30 = this.网络连接;
                                    if (网络连接30 == null)
                                    {
                                        return;
                                    }
                                    网络连接30.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.对话页面 = 612604000;
                                    this.重铸部位 = 13;
                                    客户网络 网络连接31 = this.网络连接;
                                    if (网络连接31 == null)
                                    {
                                        return;
                                    }
                                    网络连接31.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                    });
                                    return;
                                }
                            }
                        }
                        else if (选项编号 == 1)
                        {
                            EquipmentData EquipmentData10;
                            if (!this.角色装备.TryGetValue(1, out EquipmentData10))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接32 = this.网络连接;
                                if (网络连接32 == null)
                                {
                                    return;
                                }
                                网络连接32.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 1;
                                客户网络 网络连接33 = this.网络连接;
                                if (网络连接33 == null)
                                {
                                    return;
                                }
                                网络连接33.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 2)
                        {
                            EquipmentData EquipmentData11;
                            if (!this.角色装备.TryGetValue(3, out EquipmentData11))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接34 = this.网络连接;
                                if (网络连接34 == null)
                                {
                                    return;
                                }
                                网络连接34.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 3;
                                客户网络 网络连接35 = this.网络连接;
                                if (网络连接35 == null)
                                {
                                    return;
                                }
                                网络连接35.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 3)
                        {
                            EquipmentData EquipmentData12;
                            if (!this.角色装备.TryGetValue(6, out EquipmentData12))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接36 = this.网络连接;
                                if (网络连接36 == null)
                                {
                                    return;
                                }
                                网络连接36.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 6;
                                客户网络 网络连接37 = this.网络连接;
                                if (网络连接37 == null)
                                {
                                    return;
                                }
                                网络连接37.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 4)
                        {
                            EquipmentData EquipmentData13;
                            if (!this.角色装备.TryGetValue(7, out EquipmentData13))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接38 = this.网络连接;
                                if (网络连接38 == null)
                                {
                                    return;
                                }
                                网络连接38.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 7;
                                客户网络 网络连接39 = this.网络连接;
                                if (网络连接39 == null)
                                {
                                    return;
                                }
                                网络连接39.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 5)
                        {
                            EquipmentData EquipmentData14;
                            if (!this.角色装备.TryGetValue(4, out EquipmentData14))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接40 = this.网络连接;
                                if (网络连接40 == null)
                                {
                                    return;
                                }
                                网络连接40.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 4;
                                客户网络 网络连接41 = this.网络连接;
                                if (网络连接41 == null)
                                {
                                    return;
                                }
                                网络连接41.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 6)
                        {
                            EquipmentData EquipmentData15;
                            if (!this.角色装备.TryGetValue(5, out EquipmentData15))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接42 = this.网络连接;
                                if (网络连接42 == null)
                                {
                                    return;
                                }
                                网络连接42.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 5;
                                客户网络 网络连接43 = this.网络连接;
                                if (网络连接43 == null)
                                {
                                    return;
                                }
                                网络连接43.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 7)
                        {
                            EquipmentData EquipmentData16;
                            if (!this.角色装备.TryGetValue(2, out EquipmentData16))
                            {
                                this.对话页面 = 612603000;
                                客户网络 网络连接44 = this.网络连接;
                                if (网络连接44 == null)
                                {
                                    return;
                                }
                                网络连接44.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                this.对话页面 = 612604000;
                                this.重铸部位 = 2;
                                客户网络 网络连接45 = this.网络连接;
                                if (网络连接45 == null)
                                {
                                    return;
                                }
                                网络连接45.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                    }
                    else if (num <= 619202500)
                    {
                        if (num <= 619200000)
                        {
                            if (num != 612606000)
                            {
                                if (num != 619200000)
                                {
                                    return;
                                }
                                if (选项编号 == 1)
                                {
                                    this.对话页面 = 619201000;
                                }
                                else
                                {
                                    if (选项编号 != 2)
                                    {
                                        return;
                                    }
                                    this.对话页面 = 619202000;
                                }
                                客户网络 网络连接46 = this.网络连接;
                                if (网络连接46 == null)
                                {
                                    return;
                                }
                                网络连接46.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 1)
                            {
                                this.对话页面 = 612604000;
                                客户网络 网络连接47 = this.网络连接;
                                if (网络连接47 == null)
                                {
                                    return;
                                }
                                网络连接47.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}>", (EquipmentWearingParts)this.重铸部位))
                                });
                                return;
                            }
                        }
                        else if (num != 619201000)
                        {
                            if (num != 619202000)
                            {
                                if (num != 619202500)
                                {
                                    return;
                                }
                                if (选项编号 == 1)
                                {
                                    EquipmentData EquipmentData17 = null;
                                    if (this.雕色部位 == 1)
                                    {
                                        this.角色装备.TryGetValue(3, out EquipmentData17);
                                    }
                                    else if (this.雕色部位 == 2)
                                    {
                                        this.角色装备.TryGetValue(1, out EquipmentData17);
                                    }
                                    else if (this.雕色部位 == 3)
                                    {
                                        this.角色装备.TryGetValue(7, out EquipmentData17);
                                    }
                                    else if (this.雕色部位 == 4)
                                    {
                                        this.角色装备.TryGetValue(5, out EquipmentData17);
                                    }
                                    else if (this.雕色部位 == 5)
                                    {
                                        this.角色装备.TryGetValue(6, out EquipmentData17);
                                    }
                                    else if (this.雕色部位 == 6)
                                    {
                                        this.角色装备.TryGetValue(4, out EquipmentData17);
                                    }
                                    else if (this.雕色部位 == 7)
                                    {
                                        this.角色装备.TryGetValue(2, out EquipmentData17);
                                    }
                                    else
                                    {
                                        if (this.雕色部位 != 8)
                                        {
                                            return;
                                        }
                                        this.角色装备.TryGetValue(14, out EquipmentData17);
                                    }
                                    if (EquipmentData17 == null)
                                    {
                                        this.对话页面 = 619202100;
                                        客户网络 网络连接48 = this.网络连接;
                                        if (网络连接48 == null)
                                        {
                                            return;
                                        }
                                        网络连接48.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else if (EquipmentData17.孔洞颜色.Count == 0)
                                    {
                                        this.对话页面 = 619202400;
                                        客户网络 网络连接49 = this.网络连接;
                                        if (网络连接49 == null)
                                        {
                                            return;
                                        }
                                        网络连接49.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else if (EquipmentData17.镶嵌灵石.Count != 0)
                                    {
                                        this.对话页面 = 619202300;
                                        客户网络 网络连接50 = this.网络连接;
                                        if (网络连接50 == null)
                                        {
                                            return;
                                        }
                                        网络连接50.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        int num10 = 5;
                                        int num11 = 100000;
                                        List<ItemData> 物品列表2;
                                        if (this.NumberGoldCoins < 100000)
                                        {
                                            this.对话页面 = 619202200;
                                            客户网络 网络连接51 = this.网络连接;
                                            if (网络连接51 == null)
                                            {
                                                return;
                                            }
                                            网络连接51.发送封包(new 同步交互结果
                                            {
                                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0><#P1:{1}>", num10, num11 / 10000)),
                                                对象编号 = this.对话守卫.MapId
                                            });
                                            return;
                                        }
                                        else if (!this.查找背包物品(num10, 91116, out 物品列表2))
                                        {
                                            this.对话页面 = 619202200;
                                            客户网络 网络连接52 = this.网络连接;
                                            if (网络连接52 == null)
                                            {
                                                return;
                                            }
                                            网络连接52.发送封包(new 同步交互结果
                                            {
                                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0><#P1:{1}>", num10, num11 / 10000)),
                                                对象编号 = this.对话守卫.MapId
                                            });
                                            return;
                                        }
                                        else
                                        {
                                            this.NumberGoldCoins -= num11;
                                            this.消耗背包物品(num10, 物品列表2);
                                            EquipmentData17.孔洞颜色[MainProcess.RandomNumber.Next(EquipmentData17.孔洞颜色.Count)] = (EquipHoleColor)MainProcess.RandomNumber.Next(1, 8);
                                            客户网络 网络连接53 = this.网络连接;
                                            if (网络连接53 != null)
                                            {
                                                网络连接53.发送封包(new 玩家物品变动
                                                {
                                                    物品描述 = EquipmentData17.字节描述()
                                                });
                                            }
                                            if (EquipmentData17.孔洞颜色.Count == 1)
                                            {
                                                this.对话页面 = 619202500;
                                                客户网络 网络连接54 = this.网络连接;
                                                if (网络连接54 == null)
                                                {
                                                    return;
                                                }
                                                网络连接54.发送封包(new 同步交互结果
                                                {
                                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", EquipmentData17.孔洞颜色[0])),
                                                    对象编号 = this.对话守卫.MapId
                                                });
                                                return;
                                            }
                                            else if (EquipmentData17.孔洞颜色.Count == 2)
                                            {
                                                this.对话页面 = 619202600;
                                                客户网络 网络连接55 = this.网络连接;
                                                if (网络连接55 == null)
                                                {
                                                    return;
                                                }
                                                网络连接55.发送封包(new 同步交互结果
                                                {
                                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", EquipmentData17.孔洞颜色[0], EquipmentData17.孔洞颜色[1])),
                                                    对象编号 = this.对话守卫.MapId
                                                });
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                EquipmentData EquipmentData18 = null;
                                if (选项编号 == 1)
                                {
                                    this.角色装备.TryGetValue(3, out EquipmentData18);
                                }
                                else if (选项编号 == 2)
                                {
                                    this.角色装备.TryGetValue(1, out EquipmentData18);
                                }
                                else if (选项编号 == 3)
                                {
                                    this.角色装备.TryGetValue(7, out EquipmentData18);
                                }
                                else if (选项编号 == 4)
                                {
                                    this.角色装备.TryGetValue(5, out EquipmentData18);
                                }
                                else if (选项编号 == 5)
                                {
                                    this.角色装备.TryGetValue(6, out EquipmentData18);
                                }
                                else if (选项编号 == 6)
                                {
                                    this.角色装备.TryGetValue(4, out EquipmentData18);
                                }
                                else if (选项编号 == 7)
                                {
                                    this.角色装备.TryGetValue(2, out EquipmentData18);
                                }
                                else
                                {
                                    if (选项编号 != 8)
                                    {
                                        return;
                                    }
                                    this.角色装备.TryGetValue(14, out EquipmentData18);
                                }
                                if (EquipmentData18 == null)
                                {
                                    this.对话页面 = 619202100;
                                    客户网络 网络连接56 = this.网络连接;
                                    if (网络连接56 == null)
                                    {
                                        return;
                                    }
                                    网络连接56.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else if (EquipmentData18.孔洞颜色.Count == 0)
                                {
                                    this.对话页面 = 619202400;
                                    客户网络 网络连接57 = this.网络连接;
                                    if (网络连接57 == null)
                                    {
                                        return;
                                    }
                                    网络连接57.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else if (EquipmentData18.镶嵌灵石.Count != 0)
                                {
                                    this.对话页面 = 619202300;
                                    客户网络 网络连接58 = this.网络连接;
                                    if (网络连接58 == null)
                                    {
                                        return;
                                    }
                                    网络连接58.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    this.雕色部位 = (byte)选项编号;
                                    if (EquipmentData18.孔洞颜色.Count == 1)
                                    {
                                        this.对话页面 = 619202500;
                                        客户网络 网络连接59 = this.网络连接;
                                        if (网络连接59 == null)
                                        {
                                            return;
                                        }
                                        网络连接59.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", EquipmentData18.孔洞颜色[0])),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                    else if (EquipmentData18.孔洞颜色.Count == 2)
                                    {
                                        this.对话页面 = 619202600;
                                        客户网络 网络连接60 = this.网络连接;
                                        if (网络连接60 == null)
                                        {
                                            return;
                                        }
                                        网络连接60.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", EquipmentData18.孔洞颜色[0], EquipmentData18.孔洞颜色[1])),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                }
                            }
                        }
                        else
                        {
                            EquipmentData EquipmentData19 = null;
                            if (选项编号 == 1)
                            {
                                this.角色装备.TryGetValue(3, out EquipmentData19);
                            }
                            else if (选项编号 == 2)
                            {
                                this.角色装备.TryGetValue(1, out EquipmentData19);
                            }
                            else if (选项编号 == 3)
                            {
                                this.角色装备.TryGetValue(7, out EquipmentData19);
                            }
                            else if (选项编号 == 4)
                            {
                                this.角色装备.TryGetValue(5, out EquipmentData19);
                            }
                            else if (选项编号 == 5)
                            {
                                this.角色装备.TryGetValue(6, out EquipmentData19);
                            }
                            else if (选项编号 == 6)
                            {
                                this.角色装备.TryGetValue(4, out EquipmentData19);
                            }
                            else if (选项编号 == 7)
                            {
                                this.角色装备.TryGetValue(2, out EquipmentData19);
                            }
                            else
                            {
                                if (选项编号 != 8)
                                {
                                    return;
                                }
                                this.角色装备.TryGetValue(14, out EquipmentData19);
                            }
                            if (EquipmentData19 == null)
                            {
                                this.对话页面 = 619201100;
                                客户网络 网络连接61 = this.网络连接;
                                if (网络连接61 == null)
                                {
                                    return;
                                }
                                网络连接61.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (EquipmentData19.孔洞颜色.Count >= 2)
                            {
                                this.对话页面 = 619201300;
                                客户网络 网络连接62 = this.网络连接;
                                if (网络连接62 == null)
                                {
                                    return;
                                }
                                网络连接62.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                int num12 = (EquipmentData19.孔洞颜色.Count == 0) ? 5 : 50;
                                List<ItemData> 物品列表3;
                                if (!this.查找背包物品(num12, 91115, out 物品列表3))
                                {
                                    this.对话页面 = 619201200;
                                    客户网络 网络连接63 = this.网络连接;
                                    if (网络连接63 == null)
                                    {
                                        return;
                                    }
                                    网络连接63.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num12)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    this.消耗背包物品(num12, 物品列表3);
                                    EquipmentData19.孔洞颜色.Add(EquipHoleColor.黄色);
                                    客户网络 网络连接64 = this.网络连接;
                                    if (网络连接64 == null)
                                    {
                                        return;
                                    }
                                    网络连接64.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = EquipmentData19.字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                    }
                    else if (num <= 619400000)
                    {
                        if (num != 619202600)
                        {
                            if (num != 619400000)
                            {
                                return;
                            }
                            if (选项编号 == 1)
                            {
                                this.对话页面 = 619401000;
                                客户网络 网络连接65 = this.网络连接;
                                if (网络连接65 == null)
                                {
                                    return;
                                }
                                网络连接65.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 2)
                            {
                                this.对话页面 = 619402000;
                                客户网络 网络连接66 = this.网络连接;
                                if (网络连接66 == null)
                                {
                                    return;
                                }
                                网络连接66.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 3)
                            {
                                this.对话页面 = 619403000;
                                客户网络 网络连接67 = this.网络连接;
                                if (网络连接67 == null)
                                {
                                    return;
                                }
                                网络连接67.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 4)
                            {
                                this.对话页面 = 619404000;
                                客户网络 网络连接68 = this.网络连接;
                                if (网络连接68 == null)
                                {
                                    return;
                                }
                                网络连接68.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 5)
                            {
                                this.对话页面 = 619405000;
                                客户网络 网络连接69 = this.网络连接;
                                if (网络连接69 == null)
                                {
                                    return;
                                }
                                网络连接69.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 6)
                            {
                                this.对话页面 = 619406000;
                                客户网络 网络连接70 = this.网络连接;
                                if (网络连接70 == null)
                                {
                                    return;
                                }
                                网络连接70.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 7)
                            {
                                this.对话页面 = 619407000;
                                客户网络 网络连接71 = this.网络连接;
                                if (网络连接71 == null)
                                {
                                    return;
                                }
                                网络连接71.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (选项编号 == 8)
                            {
                                this.对话页面 = 619408000;
                                客户网络 网络连接72 = this.网络连接;
                                if (网络连接72 == null)
                                {
                                    return;
                                }
                                网络连接72.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                        }
                        else if (选项编号 == 1)
                        {
                            EquipmentData EquipmentData20 = null;
                            if (this.雕色部位 == 1)
                            {
                                this.角色装备.TryGetValue(3, out EquipmentData20);
                            }
                            else if (this.雕色部位 == 2)
                            {
                                this.角色装备.TryGetValue(1, out EquipmentData20);
                            }
                            else if (this.雕色部位 == 3)
                            {
                                this.角色装备.TryGetValue(7, out EquipmentData20);
                            }
                            else if (this.雕色部位 == 4)
                            {
                                this.角色装备.TryGetValue(5, out EquipmentData20);
                            }
                            else if (this.雕色部位 == 5)
                            {
                                this.角色装备.TryGetValue(6, out EquipmentData20);
                            }
                            else if (this.雕色部位 == 6)
                            {
                                this.角色装备.TryGetValue(4, out EquipmentData20);
                            }
                            else if (this.雕色部位 == 7)
                            {
                                this.角色装备.TryGetValue(2, out EquipmentData20);
                            }
                            else
                            {
                                if (this.雕色部位 != 8)
                                {
                                    return;
                                }
                                this.角色装备.TryGetValue(14, out EquipmentData20);
                            }
                            if (EquipmentData20 == null)
                            {
                                this.对话页面 = 619202100;
                                客户网络 网络连接73 = this.网络连接;
                                if (网络连接73 == null)
                                {
                                    return;
                                }
                                网络连接73.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (EquipmentData20.孔洞颜色.Count == 0)
                            {
                                this.对话页面 = 619202400;
                                客户网络 网络连接74 = this.网络连接;
                                if (网络连接74 == null)
                                {
                                    return;
                                }
                                网络连接74.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else if (EquipmentData20.镶嵌灵石.Count != 0)
                            {
                                this.对话页面 = 619202300;
                                客户网络 网络连接75 = this.网络连接;
                                if (网络连接75 == null)
                                {
                                    return;
                                }
                                网络连接75.发送封包(new 同步交互结果
                                {
                                    对象编号 = this.对话守卫.MapId,
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                });
                                return;
                            }
                            else
                            {
                                int num13 = 5;
                                int num14 = 100000;
                                List<ItemData> 物品列表4;
                                if (this.NumberGoldCoins < 100000)
                                {
                                    this.对话页面 = 619202200;
                                    客户网络 网络连接76 = this.网络连接;
                                    if (网络连接76 == null)
                                    {
                                        return;
                                    }
                                    网络连接76.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", num13, num14 / 10000)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (!this.查找背包物品(num13, 91116, out 物品列表4))
                                {
                                    this.对话页面 = 619202200;
                                    客户网络 网络连接77 = this.网络连接;
                                    if (网络连接77 == null)
                                    {
                                        return;
                                    }
                                    网络连接77.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", num13, num14 / 10000)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    this.NumberGoldCoins -= num14;
                                    this.消耗背包物品(num13, 物品列表4);
                                    EquipmentData20.孔洞颜色[MainProcess.RandomNumber.Next(EquipmentData20.孔洞颜色.Count)] = (EquipHoleColor)MainProcess.RandomNumber.Next(1, 8);
                                    客户网络 网络连接78 = this.网络连接;
                                    if (网络连接78 != null)
                                    {
                                        网络连接78.发送封包(new 玩家物品变动
                                        {
                                            物品描述 = EquipmentData20.字节描述()
                                        });
                                    }
                                    if (EquipmentData20.孔洞颜色.Count == 1)
                                    {
                                        this.对话页面 = 619202500;
                                        客户网络 网络连接79 = this.网络连接;
                                        if (网络连接79 == null)
                                        {
                                            return;
                                        }
                                        网络连接79.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", EquipmentData20.孔洞颜色[0])),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                    else if (EquipmentData20.孔洞颜色.Count == 2)
                                    {
                                        this.对话页面 = 619202600;
                                        客户网络 网络连接80 = this.网络连接;
                                        if (网络连接80 == null)
                                        {
                                            return;
                                        }
                                        网络连接80.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", EquipmentData20.孔洞颜色[0], EquipmentData20.孔洞颜色[1])),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    else if (num != 619401000)
                    {
                        if (num != 619402000)
                        {
                            if (num != 619403000)
                            {
                                return;
                            }
                            int num15 = 10;
                            int num16;
                            if (选项编号 == 1)
                            {
                                num16 = 10110;
                            }
                            else
                            {
                                if (选项编号 != 2)
                                {
                                    return;
                                }
                                num16 = 10111;
                            }
                            if (this.背包剩余 <= 0)
                            {
                                this.对话页面 = 619400200;
                                客户网络 网络连接81 = this.网络连接;
                                if (网络连接81 == null)
                                {
                                    return;
                                }
                                网络连接81.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                            else
                            {
                                List<ItemData> 物品列表5;
                                if (this.查找背包物品(num15, num16, out 物品列表5))
                                {
                                    byte b = 0;
                                    while (b < this.背包大小)
                                    {
                                        if (this.角色背包.ContainsKey(b))
                                        {
                                            b += 1;
                                        }
                                        else
                                        {
                                            this.消耗背包物品(num15, 物品列表5);
                                            this.角色背包[b] = new ItemData(GameItems.DataSheet[num16 + 1], this.CharacterData, 1, b, 1);
                                            客户网络 网络连接82 = this.网络连接;
                                            if (网络连接82 != null)
                                            {
                                                网络连接82.发送封包(new 玩家物品变动
                                                {
                                                    物品描述 = this.角色背包[b].字节描述()
                                                });
                                            }
                                            客户网络 网络连接83 = this.网络连接;
                                            if (网络连接83 == null)
                                            {
                                                return;
                                            }
                                            网络连接83.发送封包(new 成功合成灵石
                                            {
                                                灵石状态 = 1
                                            });
                                            return;
                                        }
                                    }
                                }
                                this.对话页面 = 619400100;
                                客户网络 网络连接84 = this.网络连接;
                                if (网络连接84 == null)
                                {
                                    return;
                                }
                                网络连接84.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num15)),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                        }
                        else
                        {
                            int num17 = 10;
                            int num18;
                            if (选项编号 == 1)
                            {
                                num18 = 10320;
                            }
                            else
                            {
                                if (选项编号 != 2)
                                {
                                    return;
                                }
                                num18 = 10321;
                            }
                            if (this.背包剩余 <= 0)
                            {
                                this.对话页面 = 619400200;
                                客户网络 网络连接85 = this.网络连接;
                                if (网络连接85 == null)
                                {
                                    return;
                                }
                                网络连接85.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                            else
                            {
                                List<ItemData> 物品列表6;
                                if (this.查找背包物品(num17, num18, out 物品列表6))
                                {
                                    byte b2 = 0;
                                    while (b2 < this.背包大小)
                                    {
                                        if (this.角色背包.ContainsKey(b2))
                                        {
                                            b2 += 1;
                                        }
                                        else
                                        {
                                            this.消耗背包物品(num17, 物品列表6);
                                            this.角色背包[b2] = new ItemData(GameItems.DataSheet[num18 + 1], this.CharacterData, 1, b2, 1);
                                            客户网络 网络连接86 = this.网络连接;
                                            if (网络连接86 != null)
                                            {
                                                网络连接86.发送封包(new 玩家物品变动
                                                {
                                                    物品描述 = this.角色背包[b2].字节描述()
                                                });
                                            }
                                            客户网络 网络连接87 = this.网络连接;
                                            if (网络连接87 == null)
                                            {
                                                return;
                                            }
                                            网络连接87.发送封包(new 成功合成灵石
                                            {
                                                灵石状态 = 1
                                            });
                                            return;
                                        }
                                    }
                                }
                                this.对话页面 = 619400100;
                                客户网络 网络连接88 = this.网络连接;
                                if (网络连接88 == null)
                                {
                                    return;
                                }
                                网络连接88.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num17)),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                        }
                    }
                    else
                    {
                        int num19 = 10;
                        int num20;
                        if (选项编号 == 1)
                        {
                            num20 = 10420;
                        }
                        else
                        {
                            if (选项编号 != 2)
                            {
                                return;
                            }
                            num20 = 10421;
                        }
                        if (this.背包剩余 <= 0)
                        {
                            this.对话页面 = 619400200;
                            客户网络 网络连接89 = this.网络连接;
                            if (网络连接89 == null)
                            {
                                return;
                            }
                            网络连接89.发送封包(new 同步交互结果
                            {
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                对象编号 = this.对话守卫.MapId
                            });
                            return;
                        }
                        else
                        {
                            List<ItemData> 物品列表7;
                            if (this.查找背包物品(num19, num20, out 物品列表7))
                            {
                                byte b3 = 0;
                                while (b3 < this.背包大小)
                                {
                                    if (this.角色背包.ContainsKey(b3))
                                    {
                                        b3 += 1;
                                    }
                                    else
                                    {
                                        this.消耗背包物品(num19, 物品列表7);
                                        this.角色背包[b3] = new ItemData(GameItems.DataSheet[num20 + 1], this.CharacterData, 1, b3, 1);
                                        客户网络 网络连接90 = this.网络连接;
                                        if (网络连接90 != null)
                                        {
                                            网络连接90.发送封包(new 玩家物品变动
                                            {
                                                物品描述 = this.角色背包[b3].字节描述()
                                            });
                                        }
                                        客户网络 网络连接91 = this.网络连接;
                                        if (网络连接91 == null)
                                        {
                                            return;
                                        }
                                        网络连接91.发送封包(new 成功合成灵石
                                        {
                                            灵石状态 = 1
                                        });
                                        return;
                                    }
                                }
                            }
                            this.对话页面 = 619400100;
                            客户网络 网络连接92 = this.网络连接;
                            if (网络连接92 == null)
                            {
                                return;
                            }
                            网络连接92.发送封包(new 同步交互结果
                            {
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num19)),
                                对象编号 = this.对话守卫.MapId
                            });
                            return;
                        }
                    }
                }
                else if (num <= 635800000)
                {
                    if (num <= 619408000)
                    {
                        if (num <= 619405000)
                        {
                            if (num != 619404000)
                            {
                                if (num != 619405000)
                                {
                                    return;
                                }
                                int num21 = 10;
                                int num22;
                                if (选项编号 == 1)
                                {
                                    num22 = 10720;
                                }
                                else
                                {
                                    if (选项编号 != 2)
                                    {
                                        return;
                                    }
                                    num22 = 10721;
                                }
                                if (this.背包剩余 <= 0)
                                {
                                    this.对话页面 = 619400200;
                                    客户网络 网络连接93 = this.网络连接;
                                    if (网络连接93 == null)
                                    {
                                        return;
                                    }
                                    网络连接93.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    List<ItemData> 物品列表8;
                                    if (this.查找背包物品(num21, num22, out 物品列表8))
                                    {
                                        byte b4 = 0;
                                        while (b4 < this.背包大小)
                                        {
                                            if (this.角色背包.ContainsKey(b4))
                                            {
                                                b4 += 1;
                                            }
                                            else
                                            {
                                                this.消耗背包物品(num21, 物品列表8);
                                                this.角色背包[b4] = new ItemData(GameItems.DataSheet[num22 + 1], this.CharacterData, 1, b4, 1);
                                                客户网络 网络连接94 = this.网络连接;
                                                if (网络连接94 != null)
                                                {
                                                    网络连接94.发送封包(new 玩家物品变动
                                                    {
                                                        物品描述 = this.角色背包[b4].字节描述()
                                                    });
                                                }
                                                客户网络 网络连接95 = this.网络连接;
                                                if (网络连接95 == null)
                                                {
                                                    return;
                                                }
                                                网络连接95.发送封包(new 成功合成灵石
                                                {
                                                    灵石状态 = 1
                                                });
                                                return;
                                            }
                                        }
                                    }
                                    this.对话页面 = 619400100;
                                    客户网络 网络连接96 = this.网络连接;
                                    if (网络连接96 == null)
                                    {
                                        return;
                                    }
                                    网络连接96.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num21)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                            }
                            else
                            {
                                int num23 = 10;
                                int num24;
                                if (选项编号 == 1)
                                {
                                    num24 = 10620;
                                }
                                else
                                {
                                    if (选项编号 != 2)
                                    {
                                        return;
                                    }
                                    num24 = 10621;
                                }
                                if (this.背包剩余 <= 0)
                                {
                                    this.对话页面 = 619400200;
                                    客户网络 网络连接97 = this.网络连接;
                                    if (网络连接97 == null)
                                    {
                                        return;
                                    }
                                    网络连接97.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    List<ItemData> 物品列表9;
                                    if (this.查找背包物品(num23, num24, out 物品列表9))
                                    {
                                        byte b5 = 0;
                                        while (b5 < this.背包大小)
                                        {
                                            if (this.角色背包.ContainsKey(b5))
                                            {
                                                b5 += 1;
                                            }
                                            else
                                            {
                                                this.消耗背包物品(num23, 物品列表9);
                                                this.角色背包[b5] = new ItemData(GameItems.DataSheet[num24 + 1], this.CharacterData, 1, b5, 1);
                                                客户网络 网络连接98 = this.网络连接;
                                                if (网络连接98 != null)
                                                {
                                                    网络连接98.发送封包(new 玩家物品变动
                                                    {
                                                        物品描述 = this.角色背包[b5].字节描述()
                                                    });
                                                }
                                                客户网络 网络连接99 = this.网络连接;
                                                if (网络连接99 == null)
                                                {
                                                    return;
                                                }
                                                网络连接99.发送封包(new 成功合成灵石
                                                {
                                                    灵石状态 = 1
                                                });
                                                return;
                                            }
                                        }
                                    }
                                    this.对话页面 = 619400100;
                                    客户网络 网络连接100 = this.网络连接;
                                    if (网络连接100 == null)
                                    {
                                        return;
                                    }
                                    网络连接100.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num23)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                            }
                        }
                        else if (num != 619406000)
                        {
                            if (num != 619407000)
                            {
                                if (num != 619408000)
                                {
                                    return;
                                }
                                int num25 = 10;
                                int num26;
                                if (选项编号 == 1)
                                {
                                    num26 = 10520;
                                }
                                else
                                {
                                    if (选项编号 != 2)
                                    {
                                        return;
                                    }
                                    num26 = 10521;
                                }
                                if (this.背包剩余 <= 0)
                                {
                                    this.对话页面 = 619400200;
                                    客户网络 网络连接101 = this.网络连接;
                                    if (网络连接101 == null)
                                    {
                                        return;
                                    }
                                    网络连接101.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    List<ItemData> 物品列表10;
                                    if (this.查找背包物品(num25, num26, out 物品列表10))
                                    {
                                        byte b6 = 0;
                                        while (b6 < this.背包大小)
                                        {
                                            if (this.角色背包.ContainsKey(b6))
                                            {
                                                b6 += 1;
                                            }
                                            else
                                            {
                                                this.消耗背包物品(num25, 物品列表10);
                                                this.角色背包[b6] = new ItemData(GameItems.DataSheet[num26 + 1], this.CharacterData, 1, b6, 1);
                                                客户网络 网络连接102 = this.网络连接;
                                                if (网络连接102 != null)
                                                {
                                                    网络连接102.发送封包(new 玩家物品变动
                                                    {
                                                        物品描述 = this.角色背包[b6].字节描述()
                                                    });
                                                }
                                                客户网络 网络连接103 = this.网络连接;
                                                if (网络连接103 == null)
                                                {
                                                    return;
                                                }
                                                网络连接103.发送封包(new 成功合成灵石
                                                {
                                                    灵石状态 = 1
                                                });
                                                return;
                                            }
                                        }
                                    }
                                    this.对话页面 = 619400100;
                                    客户网络 网络连接104 = this.网络连接;
                                    if (网络连接104 == null)
                                    {
                                        return;
                                    }
                                    网络连接104.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num25)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                            }
                            else
                            {
                                int num27 = 10;
                                int num28;
                                if (选项编号 == 1)
                                {
                                    num28 = 10220;
                                }
                                else
                                {
                                    if (选项编号 != 2)
                                    {
                                        return;
                                    }
                                    num28 = 10221;
                                }
                                if (this.背包剩余 <= 0)
                                {
                                    this.对话页面 = 619400200;
                                    客户网络 网络连接105 = this.网络连接;
                                    if (网络连接105 == null)
                                    {
                                        return;
                                    }
                                    网络连接105.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    List<ItemData> 物品列表11;
                                    if (this.查找背包物品(num27, num28, out 物品列表11))
                                    {
                                        byte b7 = 0;
                                        while (b7 < this.背包大小)
                                        {
                                            if (this.角色背包.ContainsKey(b7))
                                            {
                                                b7 += 1;
                                            }
                                            else
                                            {
                                                this.消耗背包物品(num27, 物品列表11);
                                                this.角色背包[b7] = new ItemData(GameItems.DataSheet[num28 + 1], this.CharacterData, 1, b7, 1);
                                                客户网络 网络连接106 = this.网络连接;
                                                if (网络连接106 != null)
                                                {
                                                    网络连接106.发送封包(new 玩家物品变动
                                                    {
                                                        物品描述 = this.角色背包[b7].字节描述()
                                                    });
                                                }
                                                客户网络 网络连接107 = this.网络连接;
                                                if (网络连接107 == null)
                                                {
                                                    return;
                                                }
                                                网络连接107.发送封包(new 成功合成灵石
                                                {
                                                    灵石状态 = 1
                                                });
                                                return;
                                            }
                                        }
                                    }
                                    this.对话页面 = 619400100;
                                    客户网络 网络连接108 = this.网络连接;
                                    if (网络连接108 == null)
                                    {
                                        return;
                                    }
                                    网络连接108.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num27)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                            }
                        }
                        else
                        {
                            int num29 = 10;
                            int num30;
                            if (选项编号 == 1)
                            {
                                num30 = 10120;
                            }
                            else
                            {
                                if (选项编号 != 2)
                                {
                                    return;
                                }
                                num30 = 10121;
                            }
                            if (this.背包剩余 <= 0)
                            {
                                this.对话页面 = 619400200;
                                客户网络 网络连接109 = this.网络连接;
                                if (网络连接109 == null)
                                {
                                    return;
                                }
                                网络连接109.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                            else
                            {
                                List<ItemData> 物品列表12;
                                if (this.查找背包物品(num29, num30, out 物品列表12))
                                {
                                    byte b8 = 0;
                                    while (b8 < this.背包大小)
                                    {
                                        if (this.角色背包.ContainsKey(b8))
                                        {
                                            b8 += 1;
                                        }
                                        else
                                        {
                                            this.消耗背包物品(num29, 物品列表12);
                                            this.角色背包[b8] = new ItemData(GameItems.DataSheet[num30 + 1], this.CharacterData, 1, b8, 1);
                                            客户网络 网络连接110 = this.网络连接;
                                            if (网络连接110 != null)
                                            {
                                                网络连接110.发送封包(new 玩家物品变动
                                                {
                                                    物品描述 = this.角色背包[b8].字节描述()
                                                });
                                            }
                                            客户网络 网络连接111 = this.网络连接;
                                            if (网络连接111 == null)
                                            {
                                                return;
                                            }
                                            网络连接111.发送封包(new 成功合成灵石
                                            {
                                                灵石状态 = 1
                                            });
                                            return;
                                        }
                                    }
                                }
                                this.对话页面 = 619400100;
                                客户网络 网络连接112 = this.网络连接;
                                if (网络连接112 == null)
                                {
                                    return;
                                }
                                网络连接112.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num29)),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (num <= 625200000)
                        {
                            if (num != 624200000)
                            {
                                if (num != 625200000)
                                {
                                    return;
                                }
                                if (选项编号 != 1)
                                {
                                    return;
                                }
                                List<ItemData> 物品列表13;
                                if (this.查找背包物品(1, 91127, out 物品列表13))
                                {
                                    this.消耗背包物品(1, 物品列表13);
                                    if (this.CharacterData.屠魔兑换.V.Date != MainProcess.CurrentTime.Date)
                                    {
                                        this.CharacterData.屠魔兑换.V = MainProcess.CurrentTime;
                                        this.CharacterData.屠魔次数.V = 0;
                                    }
                                    this.玩家增加经验(null, (int)Math.Max(100000.0, 1000000.0 * Math.Pow(0.699999988079071, (double)this.CharacterData.屠魔次数.V)));
                                    DataMonitor<int> 屠魔次数 = this.CharacterData.屠魔次数;
                                    int v = 屠魔次数.V;
                                    屠魔次数.V = v + 1;
                                    return;
                                }
                                this.对话页面 = 625201000;
                                客户网络 网络连接113 = this.网络连接;
                                if (网络连接113 == null)
                                {
                                    return;
                                }
                                网络连接113.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                            else
                            {
                                if (选项编号 != 1)
                                {
                                    return;
                                }
                                int DeductCoinsCommand = 100000;
                                int NeedLevel = 25;
                                if (this.所属队伍 == null)
                                {
                                    this.对话页面 = 624201000;
                                    客户网络 网络连接114 = this.网络连接;
                                    if (网络连接114 == null)
                                    {
                                        return;
                                    }
                                    网络连接114.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.CharacterData != this.所属队伍.队长数据)
                                {
                                    this.对话页面 = 624202000;
                                    客户网络 网络连接115 = this.网络连接;
                                    if (网络连接115 == null)
                                    {
                                        return;
                                    }
                                    网络连接115.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.所属队伍.队伍成员.Count < 4)
                                {
                                    this.对话页面 = 624207000;
                                    客户网络 网络连接116 = this.网络连接;
                                    if (网络连接116 == null)
                                    {
                                        return;
                                    }
                                    网络连接116.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.所属队伍.队伍成员.FirstOrDefault(delegate (CharacterData O)
                                {
                                    MapObject item;
                                    return O.ActiveConnection == null || !MapGatewayProcess.Objects.TryGetValue(O.角色编号, out item) || !this.对话守卫.邻居列表.Contains(item);
                                }) != null)
                                {
                                    this.对话页面 = 624203000;
                                    客户网络 网络连接117 = this.网络连接;
                                    if (网络连接117 == null)
                                    {
                                        return;
                                    }
                                    网络连接117.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.所属队伍.队伍成员.FirstOrDefault((CharacterData O) => O.NumberGoldCoins < DeductCoinsCommand) != null)
                                {
                                    this.对话页面 = 624204000;
                                    客户网络 网络连接118 = this.网络连接;
                                    if (网络连接118 == null)
                                    {
                                        return;
                                    }
                                    网络连接118.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", DeductCoinsCommand / 10000)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.所属队伍.队伍成员.FirstOrDefault((CharacterData O) => O.屠魔大厅.V.Date == MainProcess.CurrentTime.Date) != null)
                                {
                                    this.对话页面 = 624205000;
                                    客户网络 网络连接119 = this.网络连接;
                                    if (网络连接119 == null)
                                    {
                                        return;
                                    }
                                    网络连接119.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.所属队伍.队伍成员.FirstOrDefault((CharacterData O) => (int)O.当前等级.V < NeedLevel) != null)
                                {
                                    this.对话页面 = 624206000;
                                    客户网络 网络连接120 = this.网络连接;
                                    if (网络连接120 == null)
                                    {
                                        return;
                                    }
                                    网络连接120.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", NeedLevel)),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else if (this.所属队伍.队伍成员.FirstOrDefault((CharacterData O) => MapGatewayProcess.ActiveObjects[O.角色编号].对象死亡) != null)
                                {
                                    this.对话页面 = 624208000;
                                    客户网络 网络连接121 = this.网络连接;
                                    if (网络连接121 == null)
                                    {
                                        return;
                                    }
                                    网络连接121.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    MapInstance MapInstance;
                                    if (!MapGatewayProcess.MapInstance表.TryGetValue(1281, out MapInstance))
                                    {
                                        return;
                                    }
                                    MapInstance MapInstance2 = new MapInstance(GameMap.DataSheet[80], 1);
                                    MapInstance2.地形数据 = MapInstance.地形数据;
                                    MapInstance2.地图区域 = MapInstance.地图区域;
                                    MapInstance2.怪物区域 = MapInstance.怪物区域;
                                    MapInstance2.守卫区域 = MapInstance.守卫区域;
                                    MapInstance2.传送区域 = MapInstance.传送区域;
                                    MapInstance2.节点计时 = MainProcess.CurrentTime.AddSeconds(20.0);
                                    MapInstance2.怪物波数 = (from O in MapInstance.怪物区域
                                                         orderby O.FromCoords.X
                                                         select O).ToList<MonsterSpawns>();
                                    MapInstance2.MapObject = new HashSet<MapObject>[MapInstance.地图大小.X, MapInstance.地图大小.Y];
                                    MapInstance MapInstance3 = MapInstance2;
                                    MapGatewayProcess.副本实例表.Add(MapInstance3);
                                    MapInstance3.副本守卫 = new GuardInstance(Guards.DataSheet[6724], MapInstance3, GameDirection.左下, new Point(1005, 273));
                                    using (IEnumerator<CharacterData> enumerator = this.所属队伍.队伍成员.GetEnumerator())
                                    {
                                        while (enumerator.MoveNext())
                                        {
                                            CharacterData CharacterData = enumerator.Current;
                                            PlayerObject PlayerObject = MapGatewayProcess.ActiveObjects[CharacterData.角色编号] as PlayerObject;
                                            PlayerDeals PlayerDeals = PlayerObject.当前交易;
                                            if (PlayerDeals != null)
                                            {
                                                PlayerDeals.结束交易();
                                            }
                                            PlayerObject.NumberGoldCoins -= DeductCoinsCommand;
                                            PlayerObject.玩家切换地图(MapInstance3, AreaType.传送区域, default(Point));
                                        }
                                        return;
                                    }
                                }
                            }
                        }
                        if (num != 627400000)
                        {
                            if (num != 635000000)
                            {
                                if (num != 635800000)
                                {
                                    return;
                                }
                                if (选项编号 == 1)
                                {
                                    this.玩家切换地图((this.当前地图.MapId == 147) ? this.当前地图 : MapGatewayProcess.分配地图(147), AreaType.复活区域, default(Point));
                                    return;
                                }
                            }
                            else
                            {
                                int num31 = 40;
                                int num32 = 100000;
                                int num33 = 87;
                                if (选项编号 == 1)
                                {
                                    if ((int)this.当前等级 < num31)
                                    {
                                        this.对话页面 = 711900001;
                                        客户网络 网络连接122 = this.网络连接;
                                        if (网络连接122 == null)
                                        {
                                            return;
                                        }
                                        网络连接122.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num31)),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        if (this.NumberGoldCoins >= num32)
                                        {
                                            this.NumberGoldCoins -= num32;
                                            this.玩家切换地图((this.当前地图.MapId == num33) ? this.当前地图 : MapGatewayProcess.分配地图(num33), AreaType.传送区域, default(Point));
                                            return;
                                        }
                                        this.对话页面 = 711900002;
                                        客户网络 网络连接123 = this.网络连接;
                                        if (网络连接123 == null)
                                        {
                                            return;
                                        }
                                        网络连接123.发送封包(new 同步交互结果
                                        {
                                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num32)),
                                            对象编号 = this.对话守卫.MapId
                                        });
                                        return;
                                    }
                                }
                            }
                        }
                        else if (选项编号 == 1)
                        {
                            if (this.所属行会 != null && SystemData.数据.占领行会.V == this.所属行会)
                            {
                                if (this.CharacterData.攻沙日期.V != SystemData.数据.占领时间.V)
                                {
                                    this.对话页面 = 627403000;
                                    客户网络 网络连接124 = this.网络连接;
                                    if (网络连接124 == null)
                                    {
                                        return;
                                    }
                                    网络连接124.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                                else
                                {
                                    if (!(this.CharacterData.领奖日期.V.Date == MainProcess.CurrentTime.Date))
                                    {
                                        byte b9 = 0;
                                        while (b9 < this.背包大小)
                                        {
                                            if (this.角色背包.ContainsKey(b9))
                                            {
                                                b9 += 1;
                                            }
                                            else
                                            {
                                                byte b10 = b9;
                                                GameItems 模板;
                                                if (b10 == 255)
                                                {
                                                    客户网络 网络连接125 = this.网络连接;
                                                    if (网络连接125 == null)
                                                    {
                                                        return;
                                                    }
                                                    网络连接125.发送封包(new GameErrorMessagePacket
                                                    {
                                                        错误代码 = 6459
                                                    });
                                                    return;
                                                }
                                                else if (!GameItems.DataSheetByName.TryGetValue("沙城每日宝箱", out 模板))
                                                {
                                                    客户网络 网络连接126 = this.网络连接;
                                                    if (网络连接126 == null)
                                                    {
                                                        return;
                                                    }
                                                    网络连接126.发送封包(new GameErrorMessagePacket
                                                    {
                                                        错误代码 = 1802
                                                    });
                                                    return;
                                                }
                                                else
                                                {
                                                    this.CharacterData.领奖日期.V = MainProcess.CurrentTime;
                                                    this.角色背包[b10] = new ItemData(模板, this.CharacterData, 1, b10, 1);
                                                    客户网络 网络连接127 = this.网络连接;
                                                    if (网络连接127 == null)
                                                    {
                                                        return;
                                                    }
                                                    网络连接127.发送封包(new 玩家物品变动
                                                    {
                                                        物品描述 = this.角色背包[b10].字节描述()
                                                    });
                                                    return;
                                                }
                                            }
                                        }
                                    }
                                    this.对话页面 = 627402000;
                                    客户网络 网络连接128 = this.网络连接;
                                    if (网络连接128 == null)
                                    {
                                        return;
                                    }
                                    网络连接128.发送封包(new 同步交互结果
                                    {
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                        对象编号 = this.对话守卫.MapId
                                    });
                                    return;
                                }
                            }
                            else
                            {
                                this.对话页面 = 627401000;
                                客户网络 网络连接129 = this.网络连接;
                                if (网络连接129 == null)
                                {
                                    return;
                                }
                                网络连接129.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                        }
                    }
                }
                else if (num <= 674000000)
                {
                    if (num <= 636100000)
                    {
                        if (num != 635900000)
                        {
                            if (num != 636100000)
                            {
                                return;
                            }
                            if (选项编号 == 1)
                            {
                                this.玩家切换地图((this.当前地图.MapId == 87) ? this.当前地图 : MapGatewayProcess.分配地图(87), AreaType.传送区域, default(Point));
                                return;
                            }
                        }
                        else if (选项编号 == 1)
                        {
                            this.玩家切换地图((this.当前地图.MapId == 88) ? this.当前地图 : MapGatewayProcess.分配地图(88), AreaType.传送区域, default(Point));
                            return;
                        }
                    }
                    else if (num != 670500000)
                    {
                        if (num != 670508000)
                        {
                            if (num != 674000000)
                            {
                                return;
                            }
                            if (选项编号 == 1)
                            {
                                this.对话页面 = 674001000;
                                客户网络 网络连接130 = this.网络连接;
                                if (网络连接130 == null)
                                {
                                    return;
                                }
                                网络连接130.发送封包(new 同步交互结果
                                {
                                    交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面),
                                    对象编号 = this.对话守卫.MapId
                                });
                                return;
                            }
                            else if (选项编号 == 2)
                            {
                                客户网络 网络连接131 = this.网络连接;
                                if (网络连接131 == null)
                                {
                                    return;
                                }
                                网络连接131.发送封包(new 查看攻城名单
                                {
                                    字节描述 = SystemData.数据.沙城申请描述()
                                });
                                return;
                            }
                        }
                        else
                        {
                            if (this.CharacterData.升级装备.V == null)
                            {
                                this.网络连接.尝试断开连接(new Exception("错误操作: 继续Npcc对话.  错误: 尝试取回武器."));
                                return;
                            }
                            if (选项编号 == 1)
                            {
                                if (this.背包剩余 <= 0)
                                {
                                    this.对话页面 = 670505000;
                                    客户网络 网络连接132 = this.网络连接;
                                    if (网络连接132 == null)
                                    {
                                        return;
                                    }
                                    网络连接132.发送封包(new 同步交互结果
                                    {
                                        对象编号 = this.对话守卫.MapId,
                                        交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                    });
                                    return;
                                }
                                else
                                {
                                    int num34 = (int)((this.CharacterData.升级装备.V.升级次数.V + 1) * 100) * 10000;
                                    if (this.NumberGoldCoins < num34)
                                    {
                                        this.对话页面 = 670510000;
                                        客户网络 网络连接133 = this.网络连接;
                                        if (网络连接133 == null)
                                        {
                                            return;
                                        }
                                        网络连接133.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                    else
                                    {
                                        int num35 = (int)((this.CharacterData.升级装备.V.升级次数.V + 1) * 10);
                                        List<ItemData> 物品列表14;
                                        if (this.查找背包物品(num35, 110012, out 物品列表14))
                                        {
                                            byte b11 = 0;
                                            while (b11 < this.背包大小)
                                            {
                                                if (this.角色背包.ContainsKey(b11))
                                                {
                                                    b11 += 1;
                                                }
                                                else
                                                {
                                                    byte b12 = b11;
                                                    if (b12 == 255)
                                                    {
                                                        this.对话页面 = 670505000;
                                                        客户网络 网络连接134 = this.网络连接;
                                                        if (网络连接134 == null)
                                                        {
                                                            return;
                                                        }
                                                        网络连接134.发送封包(new 同步交互结果
                                                        {
                                                            对象编号 = this.对话守卫.MapId,
                                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                                        });
                                                        return;
                                                    }
                                                    else
                                                    {
                                                        this.NumberGoldCoins -= num34;
                                                        this.消耗背包物品(num35, 物品列表14);
                                                        this.角色背包[b12] = this.CharacterData.升级装备.V;
                                                        this.CharacterData.升级装备.V = null;
                                                        this.角色背包[b12].物品容器.V = 1;
                                                        this.角色背包[b12].物品位置.V = b12;
                                                        客户网络 网络连接135 = this.网络连接;
                                                        if (网络连接135 != null)
                                                        {
                                                            网络连接135.发送封包(new 玩家物品变动
                                                            {
                                                                物品描述 = this.角色背包[b12].字节描述()
                                                            });
                                                        }
                                                        客户网络 网络连接136 = this.网络连接;
                                                        if (网络连接136 != null)
                                                        {
                                                            网络连接136.发送封包(new 武器升级结果
                                                            {
                                                                升级结果 = 2
                                                            });
                                                        }
                                                        客户网络 网络连接137 = this.网络连接;
                                                        if (网络连接137 == null)
                                                        {
                                                            return;
                                                        }
                                                        网络连接137.发送封包(new 武器升级结果
                                                        {
                                                            升级结果 = 2
                                                        });
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                        this.对话页面 = 670509000;
                                        客户网络 网络连接138 = this.网络连接;
                                        if (网络连接138 == null)
                                        {
                                            return;
                                        }
                                        网络连接138.发送封包(new 同步交互结果
                                        {
                                            对象编号 = this.对话守卫.MapId,
                                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                                        });
                                        return;
                                    }
                                }
                            }
                            else if (选项编号 != 2 && 选项编号 == 3)
                            {
                                this.放弃升级武器();
                                return;
                            }
                        }
                    }
                    else if (选项编号 == 1)
                    {
                        if (this.CharacterData.升级装备.V != null)
                        {
                            this.对话页面 = 670501000;
                            客户网络 网络连接139 = this.网络连接;
                            if (网络连接139 == null)
                            {
                                return;
                            }
                            网络连接139.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else
                        {
                            this.打开界面 = "UpgradeCurEquippedWepn";
                            this.对话页面 = 670502000;
                            客户网络 网络连接140 = this.网络连接;
                            if (网络连接140 == null)
                            {
                                return;
                            }
                            网络连接140.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                    }
                    else if (选项编号 == 2)
                    {
                        if (this.CharacterData.升级装备.V == null)
                        {
                            this.对话页面 = 670503000;
                            客户网络 网络连接141 = this.网络连接;
                            if (网络连接141 == null)
                            {
                                return;
                            }
                            网络连接141.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else if (MainProcess.CurrentTime < this.CharacterData.取回时间.V)
                        {
                            this.对话页面 = 670504000;
                            客户网络 网络连接142 = this.网络连接;
                            if (网络连接142 == null)
                            {
                                return;
                            }
                            网络连接142.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", (int)(this.CharacterData.取回时间.V - MainProcess.CurrentTime).TotalMinutes + 1))
                            });
                            return;
                        }
                        else if (this.背包剩余 <= 0)
                        {
                            this.对话页面 = 670505000;
                            客户网络 网络连接143 = this.网络连接;
                            if (网络连接143 == null)
                            {
                                return;
                            }
                            网络连接143.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else if (this.玩家取回装备(0))
                        {
                            this.对话页面 = 670507000;
                            客户网络 网络连接144 = this.网络连接;
                            if (网络连接144 == null)
                            {
                                return;
                            }
                            网络连接144.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else
                        {
                            this.对话页面 = 670508000;
                            客户网络 网络连接145 = this.网络连接;
                            if (网络连接145 == null)
                            {
                                return;
                            }
                            网络连接145.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", (int)(this.CharacterData.升级装备.V.升级次数.V * 10 + 10), (int)(this.CharacterData.升级装备.V.升级次数.V * 100 + 100)))
                            });
                            return;
                        }
                    }
                    else if (选项编号 == 3)
                    {
                        if (this.CharacterData.升级装备.V == null)
                        {
                            this.对话页面 = 670503000;
                            客户网络 网络连接146 = this.网络连接;
                            if (网络连接146 == null)
                            {
                                return;
                            }
                            网络连接146.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else if (this.NumberGoldCoins < 100000)
                        {
                            this.对话页面 = 670506000;
                            客户网络 网络连接147 = this.网络连接;
                            if (网络连接147 == null)
                            {
                                return;
                            }
                            网络连接147.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else if (this.背包剩余 <= 0)
                        {
                            this.对话页面 = 670505000;
                            客户网络 网络连接148 = this.网络连接;
                            if (网络连接148 == null)
                            {
                                return;
                            }
                            网络连接148.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else if (this.玩家取回装备(100000))
                        {
                            this.对话页面 = 670507000;
                            客户网络 网络连接149 = this.网络连接;
                            if (网络连接149 == null)
                            {
                                return;
                            }
                            网络连接149.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                            });
                            return;
                        }
                        else
                        {
                            this.CharacterData.取回时间.V = MainProcess.CurrentTime;
                            this.对话页面 = 670508000;
                            客户网络 网络连接150 = this.网络连接;
                            if (网络连接150 == null)
                            {
                                return;
                            }
                            网络连接150.发送封包(new 同步交互结果
                            {
                                对象编号 = this.对话守卫.MapId,
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:{1}>", (int)(this.CharacterData.升级装备.V.升级次数.V * 10 + 10), (int)(this.CharacterData.升级装备.V.升级次数.V * 100 + 100)))
                            });
                            return;
                        }
                    }
                }
                else if (num <= 711900000)
                {
                    if (num != 674001000)
                    {
                        if (num != 711900000)
                        {
                            return;
                        }
                        if (选项编号 == 1)
                        {
                            this.对话页面 = 711901000;
                        }
                        else if (选项编号 == 2)
                        {
                            this.对话页面 = 711902000;
                        }
                        else
                        {
                            if (选项编号 != 3)
                            {
                                return;
                            }
                            this.对话页面 = 711903000;
                        }
                        客户网络 网络连接151 = this.网络连接;
                        if (网络连接151 == null)
                        {
                            return;
                        }
                        网络连接151.发送封包(new 同步交互结果
                        {
                            对象编号 = this.对话守卫.MapId,
                            交互文本 = NpcDialogs.GetBufferFromDialogId(this.对话页面)
                        });
                        return;
                    }
                    else if (选项编号 == 1)
                    {
                        if (this.所属行会 == null)
                        {
                            客户网络 网络连接152 = this.网络连接;
                            if (网络连接152 == null)
                            {
                                return;
                            }
                            网络连接152.发送封包(new 社交错误提示
                            {
                                错误编号 = 6668
                            });
                            return;
                        }
                        else if (this.CharacterData != this.所属行会.行会会长.V)
                        {
                            客户网络 网络连接153 = this.网络连接;
                            if (网络连接153 == null)
                            {
                                return;
                            }
                            网络连接153.发送封包(new 社交错误提示
                            {
                                错误编号 = 8961
                            });
                            return;
                        }
                        else if (this.所属行会 == SystemData.数据.占领行会.V)
                        {
                            客户网络 网络连接154 = this.网络连接;
                            if (网络连接154 == null)
                            {
                                return;
                            }
                            网络连接154.发送封包(new 社交错误提示
                            {
                                错误编号 = 8965
                            });
                            return;
                        }
                        else if (SystemData.数据.申请行会.Values.FirstOrDefault((GuildData O) => O == this.所属行会) != null)
                        {
                            客户网络 网络连接155 = this.网络连接;
                            if (网络连接155 == null)
                            {
                                return;
                            }
                            网络连接155.发送封包(new 社交错误提示
                            {
                                错误编号 = 8964
                            });
                            return;
                        }
                        else if (this.NumberGoldCoins < 1000000)
                        {
                            客户网络 网络连接156 = this.网络连接;
                            if (网络连接156 == null)
                            {
                                return;
                            }
                            网络连接156.发送封包(new 社交错误提示
                            {
                                错误编号 = 8962
                            });
                            return;
                        }
                        else
                        {
                            ItemData 当前物品;
                            if (this.查找背包物品(90196, out 当前物品))
                            {
                                this.NumberGoldCoins -= 1000000;
                                this.ConsumeBackpackItem(1, 当前物品);
                                SystemData.数据.申请行会.Add(MainProcess.CurrentTime.Date.AddDays(1.0).AddHours(20.0), this.所属行会);
                                NetworkServiceGateway.发送公告(string.Format("The guild [{0}] has signed up for the next day's Shabak Battle", this.所属行会), true);
                                return;
                            }
                            客户网络 网络连接157 = this.网络连接;
                            if (网络连接157 == null)
                            {
                                return;
                            }
                            网络连接157.发送封包(new 社交错误提示
                            {
                                错误编号 = 8963
                            });
                            return;
                        }
                    }
                }
                else if (num != 711901000)
                {
                    if (num != 711902000)
                    {
                        if (num != 711903000)
                        {
                            return;
                        }
                        int num36;
                        int num37;
                        int num38;
                        if (选项编号 == 1)
                        {
                            num36 = 15;
                            num37 = 2500;
                            num38 = 144;
                        }
                        else if (选项编号 == 2)
                        {
                            num36 = 20;
                            num37 = 3500;
                            num38 = 148;
                        }
                        else if (选项编号 == 3)
                        {
                            num36 = 25;
                            num37 = 3500;
                            num38 = 178;
                        }
                        else if (选项编号 == 4)
                        {
                            num36 = 25;
                            num37 = 4500;
                            num38 = 146;
                        }
                        else if (选项编号 == 5)
                        {
                            num36 = 30;
                            num37 = 5500;
                            num38 = 175;
                        }
                        else
                        {
                            if (选项编号 != 6)
                            {
                                return;
                            }
                            num36 = 45;
                            num37 = 7500;
                            num38 = 59;
                        }
                        if ((int)this.当前等级 < num36)
                        {
                            this.对话页面 = 711900001;
                            客户网络 网络连接158 = this.网络连接;
                            if (网络连接158 == null)
                            {
                                return;
                            }
                            网络连接158.发送封包(new 同步交互结果
                            {
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num36)),
                                对象编号 = this.对话守卫.MapId
                            });
                            return;
                        }
                        else
                        {
                            if (this.NumberGoldCoins >= num37)
                            {
                                this.NumberGoldCoins -= num37;
                                this.玩家切换地图((this.当前地图.MapId == num38) ? this.当前地图 : MapGatewayProcess.分配地图(num38), AreaType.传送区域, default(Point));
                                return;
                            }
                            this.对话页面 = 711900002;
                            客户网络 网络连接159 = this.网络连接;
                            if (网络连接159 == null)
                            {
                                return;
                            }
                            网络连接159.发送封包(new 同步交互结果
                            {
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num37)),
                                对象编号 = this.对话守卫.MapId
                            });
                            return;
                        }
                    }
                    else
                    {
                        int num39;
                        int num40;
                        int num41;
                        if (选项编号 == 1)
                        {
                            num39 = 1;
                            num40 = 2500;
                            num41 = 145;
                        }
                        else if (选项编号 == 2)
                        {
                            num39 = 40;
                            num40 = 6500;
                            num41 = 187;
                        }
                        else
                        {
                            if (选项编号 != 3)
                            {
                                return;
                            }
                            num39 = 40;
                            num40 = 9500;
                            num41 = 191;
                        }
                        if ((int)this.当前等级 < num39)
                        {
                            this.对话页面 = 711900001;
                            客户网络 网络连接160 = this.网络连接;
                            if (网络连接160 == null)
                            {
                                return;
                            }
                            网络连接160.发送封包(new 同步交互结果
                            {
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num39)),
                                对象编号 = this.对话守卫.MapId
                            });
                            return;
                        }
                        else
                        {
                            if (this.NumberGoldCoins >= num40)
                            {
                                this.NumberGoldCoins -= num40;
                                this.玩家切换地图((this.当前地图.MapId == num41) ? this.当前地图 : MapGatewayProcess.分配地图(num41), AreaType.传送区域, default(Point));
                                return;
                            }
                            this.对话页面 = 711900002;
                            客户网络 网络连接161 = this.网络连接;
                            if (网络连接161 == null)
                            {
                                return;
                            }
                            网络连接161.发送封包(new 同步交互结果
                            {
                                交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num40)),
                                对象编号 = this.对话守卫.MapId
                            });
                            return;
                        }
                    }
                }
                else
                {
                    int num42;
                    int num43;
                    int num44;
                    if (选项编号 == 1)
                    {
                        num42 = 1;
                        num43 = 1000;
                        num44 = 142;
                    }
                    else if (选项编号 == 2)
                    {
                        num42 = 8;
                        num43 = 1500;
                        num44 = 143;
                    }
                    else if (选项编号 == 3)
                    {
                        num42 = 14;
                        num43 = 2000;
                        num44 = 147;
                    }
                    else if (选项编号 == 4)
                    {
                        num42 = 30;
                        num43 = 3000;
                        num44 = 152;
                    }
                    else if (选项编号 == 5)
                    {
                        num42 = 40;
                        num43 = 5000;
                        num44 = 102;
                    }
                    else if (选项编号 == 6)
                    {
                        num42 = 45;
                        num43 = 8000;
                        num44 = 50;
                    }
                    else
                    {
                        if (选项编号 != 7)
                        {
                            return;
                        }
                        num42 = 40;
                        num43 = 10000;
                        num44 = 231;
                    }
                    if ((int)this.当前等级 < num42)
                    {
                        this.对话页面 = 711900001;
                        客户网络 网络连接162 = this.网络连接;
                        if (网络连接162 == null)
                        {
                            return;
                        }
                        网络连接162.发送封包(new 同步交互结果
                        {
                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num42)),
                            对象编号 = this.对话守卫.MapId
                        });
                        return;
                    }
                    else if (this.NumberGoldCoins < num43)
                    {
                        this.对话页面 = 711900002;
                        客户网络 网络连接163 = this.网络连接;
                        if (网络连接163 == null)
                        {
                            return;
                        }
                        网络连接163.发送封包(new 同步交互结果
                        {
                            交互文本 = NpcDialogs.CombineDialog(this.对话页面, string.Format("<#P0:{0}><#P1:0>", num43)),
                            对象编号 = this.对话守卫.MapId
                        });
                        return;
                    }
                    else
                    {
                        this.NumberGoldCoins -= num43;
                        if (num44 != 152)
                        {
                            this.玩家切换地图((this.当前地图.MapId == num44) ? this.当前地图 : MapGatewayProcess.分配地图(num44), AreaType.复活区域, default(Point));
                            return;
                        }
                        if (this.所属行会 != null && this.所属行会 == SystemData.数据.占领行会.V)
                        {
                            this.玩家切换地图((this.当前地图.MapId == num44) ? this.当前地图 : MapGatewayProcess.分配地图(num44), AreaType.传送区域, default(Point));
                            return;
                        }
                        this.玩家切换地图((this.当前地图.MapId == num44) ? this.当前地图 : MapGatewayProcess.分配地图(num44), AreaType.复活区域, default(Point));
                        return;
                    }
                }
                return;
            }
            客户网络 网络连接164 = this.网络连接;
            if (网络连接164 == null)
            {
                return;
            }
            网络连接164.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 3333
            });
        }


        public void 玩家更改设置(byte[] 设置)
        {
            using (MemoryStream memoryStream = new MemoryStream(设置))
            {
                using (BinaryReader binaryReader = new BinaryReader(memoryStream))
                {
                    int num = 设置.Length / 5;
                    for (int i = 0; i < num; i++)
                    {
                        byte 索引 = binaryReader.ReadByte();
                        uint value = binaryReader.ReadUInt32();
                        this.CharacterData.玩家设置[(int)索引] = value;
                    }
                }
            }
        }


        public void 查询地图路线()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((ushort)this.当前地图.LimitInstances);
                    binaryWriter.Write(this.当前地图.MapId);
                    for (int i = 1; i <= (int)this.当前地图.LimitInstances; i++)
                    {
                        binaryWriter.Write(16777216 + i);
                        binaryWriter.Write(MapGatewayProcess.MapInstance表[this.当前地图.MapId * 16 + i].地图状态);
                    }
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new 查询线路信息
                        {
                            字节数据 = memoryStream.ToArray()
                        });
                    }
                }
            }
        }


        public void ToggleMapRoutePacket()
        {
        }


        public void 玩家同步位置()
        {
        }


        public void 玩家扩展背包(byte 背包类型, byte 扩展大小)
        {
            if (扩展大小 == 0)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Player expanded backpack.  Error: Wrong expansion parameter."));
                return;
            }
            if (背包类型 == 1 && this.背包大小 + 扩展大小 > 64)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Player expanded backpack.  Error: Backpack exceeds limit."));
                return;
            }
            if (背包类型 == 2 && this.仓库大小 + 扩展大小 > 72)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Player expanded backpack.  Error: Warehouse exceeded limit."));
                return;
            }
            if (背包类型 != 1)
            {
                if (背包类型 == 2)
                {
                    int num = ComputingClass.扩展仓库((int)(this.仓库大小 - 16), 0, 1, 0);
                    int num2 = ComputingClass.扩展仓库((int)(this.仓库大小 + 扩展大小 - 16), 0, 1, 0) - num;
                    if (this.NumberGoldCoins < num2)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1821
                        });
                        return;
                    }
                    else
                    {
                        this.NumberGoldCoins -= num2;
                        this.仓库大小 += 扩展大小;
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 背包容量改变
                        {
                            背包类型 = 2,
                            背包容量 = this.仓库大小
                        });
                    }
                }
                return;
            }
            int num3 = ComputingClass.扩展背包((int)(this.背包大小 - 32), 0, 1, 0);
            int num4 = ComputingClass.扩展背包((int)(this.背包大小 + 扩展大小 - 32), 0, 1, 0) - num3;
            if (this.NumberGoldCoins < num4)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else
            {
                this.NumberGoldCoins -= num4;
                this.背包大小 += 扩展大小;
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new 背包容量改变
                {
                    背包类型 = 1,
                    背包容量 = this.背包大小
                });
                return;
            }
        }


        public void 商店特修单件(byte 背包类型, byte 装备位置)
        {
            this.网络连接.尝试断开连接(new Exception("MISTAKE: Special repair of a single piece of equipment.  Error: Function blocked."));
        }


        public void 商店修理单件(byte 背包类型, byte 装备位置)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.对话守卫 == null)
            {
                this.网络连接.尝试断开连接(new Exception("Bug: Shop repair single piece.  Error: Npc not selected."));
                return;
            }
            if (this.打开商店 == 0)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Shop repairing single piece.  Error: Shop not opened."));
                return;
            }
            if (this.当前地图 != this.对话守卫.当前地图 || base.网格距离(this.对话守卫) > 12)
            {
                this.网络连接.尝试断开连接(new Exception("Bug: Shop repair single piece.  Bug: Character is too far away."));
                return;
            }
            if (背包类型 != 1)
            {
                if (背包类型 == 0)
                {
                    EquipmentData EquipmentData;
                    if (!this.角色装备.TryGetValue(装备位置, out EquipmentData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1802
                        });
                        return;
                    }
                    else if (!EquipmentData.CanRepair)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1814
                        });
                        return;
                    }
                    else if (this.NumberGoldCoins < EquipmentData.修理费用)
                    {
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1821
                        });
                        return;
                    }
                    else
                    {
                        this.NumberGoldCoins -= EquipmentData.修理费用;
                        EquipmentData.最大持久.V = Math.Max(1000, EquipmentData.最大持久.V - (int)((float)(EquipmentData.最大持久.V - EquipmentData.当前持久.V) * 0.035f));
                        if (EquipmentData.当前持久.V <= 0)
                        {
                            this.Stat加成[EquipmentData] = EquipmentData.装备Stat;
                            this.更新对象Stat();
                        }
                        EquipmentData.当前持久.V = EquipmentData.最大持久.V;
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 != null)
                        {
                            网络连接4.发送封包(new 玩家物品变动
                            {
                                物品描述 = EquipmentData.字节描述()
                            });
                        }
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 == null)
                        {
                            return;
                        }
                        网络连接5.发送封包(new RepairItemResponsePacket());
                    }
                }
                return;
            }
            ItemData ItemData;
            if (!this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                客户网络 网络连接6 = this.网络连接;
                if (网络连接6 == null)
                {
                    return;
                }
                网络连接6.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                EquipmentData EquipmentData2 = ItemData as EquipmentData;
                if (EquipmentData2 == null)
                {
                    客户网络 网络连接7 = this.网络连接;
                    if (网络连接7 == null)
                    {
                        return;
                    }
                    网络连接7.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1814
                    });
                    return;
                }
                else if (!EquipmentData2.CanRepair)
                {
                    客户网络 网络连接8 = this.网络连接;
                    if (网络连接8 == null)
                    {
                        return;
                    }
                    网络连接8.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1814
                    });
                    return;
                }
                else if (this.NumberGoldCoins < EquipmentData2.修理费用)
                {
                    客户网络 网络连接9 = this.网络连接;
                    if (网络连接9 == null)
                    {
                        return;
                    }
                    网络连接9.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1821
                    });
                    return;
                }
                else
                {
                    this.NumberGoldCoins -= EquipmentData2.修理费用;
                    EquipmentData2.最大持久.V = Math.Max(1000, EquipmentData2.最大持久.V - 334);
                    EquipmentData2.当前持久.V = EquipmentData2.最大持久.V;
                    客户网络 网络连接10 = this.网络连接;
                    if (网络连接10 == null)
                    {
                        return;
                    }
                    网络连接10.发送封包(new 玩家物品变动
                    {
                        物品描述 = EquipmentData2.字节描述()
                    });
                    return;
                }
            }
        }


        public void 商店修理全部()
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.对话守卫 == null)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Shop repair single piece.  Error: Npc not selected."));
                return;
            }
            if (this.打开商店 == 0)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Shop repairing single piece.  Error: Shop not opened."));
                return;
            }
            if (this.当前地图 != this.对话守卫.当前地图 || base.网格距离(this.对话守卫) > 12)
            {
                this.网络连接.尝试断开连接(new Exception("Bug: Shop repair single piece.  Bug: Character is too far away."));
                return;
            }
            if (this.NumberGoldCoins < this.角色装备.Values.Sum(delegate (EquipmentData O)
            {
                if (!O.CanRepair)
                {
                    return 0;
                }
                return O.修理费用;
            }))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else
            {
                foreach (EquipmentData EquipmentData in this.角色装备.Values)
                {
                    if (EquipmentData.CanRepair)
                    {
                        this.NumberGoldCoins -= EquipmentData.修理费用;
                        EquipmentData.最大持久.V = Math.Max(1000, EquipmentData.最大持久.V - (int)((float)(EquipmentData.最大持久.V - EquipmentData.当前持久.V) * 0.035f));
                        if (EquipmentData.当前持久.V <= 0)
                        {
                            this.Stat加成[EquipmentData] = EquipmentData.装备Stat;
                            this.更新对象Stat();
                        }
                        EquipmentData.当前持久.V = EquipmentData.最大持久.V;
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new 玩家物品变动
                            {
                                物品描述 = EquipmentData.字节描述()
                            });
                        }
                    }
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new RepairItemResponsePacket());
                return;
            }
        }


        public void 随身修理单件(byte 背包类型, byte 装备位置, int Id)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (Id != 0)
            {
                this.网络连接.尝试断开连接(new Exception("MISTAKE: Shop repair of a single item.  Error: Prohibited item."));
                return;
            }
            if (背包类型 != 1)
            {
                if (背包类型 == 0)
                {
                    EquipmentData EquipmentData;
                    if (!this.角色装备.TryGetValue(装备位置, out EquipmentData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1802
                        });
                        return;
                    }
                    else if (!EquipmentData.CanRepair)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1814
                        });
                        return;
                    }
                    else if (this.NumberGoldCoins < EquipmentData.特修费用)
                    {
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1821
                        });
                        return;
                    }
                    else
                    {
                        this.NumberGoldCoins -= EquipmentData.特修费用;
                        EquipmentData.当前持久.V = EquipmentData.最大持久.V;
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new 玩家物品变动
                        {
                            物品描述 = EquipmentData.字节描述()
                        });
                    }
                }
                return;
            }
            ItemData ItemData;
            if (!this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                客户网络 网络连接5 = this.网络连接;
                if (网络连接5 == null)
                {
                    return;
                }
                网络连接5.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                EquipmentData EquipmentData2 = ItemData as EquipmentData;
                if (EquipmentData2 == null)
                {
                    客户网络 网络连接6 = this.网络连接;
                    if (网络连接6 == null)
                    {
                        return;
                    }
                    网络连接6.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1814
                    });
                    return;
                }
                else if (!EquipmentData2.CanRepair)
                {
                    客户网络 网络连接7 = this.网络连接;
                    if (网络连接7 == null)
                    {
                        return;
                    }
                    网络连接7.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1814
                    });
                    return;
                }
                else if (this.NumberGoldCoins < EquipmentData2.特修费用)
                {
                    客户网络 网络连接8 = this.网络连接;
                    if (网络连接8 == null)
                    {
                        return;
                    }
                    网络连接8.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1821
                    });
                    return;
                }
                else
                {
                    this.NumberGoldCoins -= EquipmentData2.特修费用;
                    if (EquipmentData2.当前持久.V <= 0)
                    {
                        this.Stat加成[EquipmentData2] = EquipmentData2.装备Stat;
                        this.更新对象Stat();
                    }
                    EquipmentData2.当前持久.V = EquipmentData2.最大持久.V;
                    客户网络 网络连接9 = this.网络连接;
                    if (网络连接9 != null)
                    {
                        网络连接9.发送封包(new 玩家物品变动
                        {
                            物品描述 = EquipmentData2.字节描述()
                        });
                    }
                    客户网络 网络连接10 = this.网络连接;
                    if (网络连接10 == null)
                    {
                        return;
                    }
                    网络连接10.发送封包(new RepairItemResponsePacket());
                    return;
                }
            }
        }


        public void 随身修理全部()
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.NumberGoldCoins < this.角色装备.Values.Sum(delegate (EquipmentData O)
            {
                if (!O.CanRepair)
                {
                    return 0;
                }
                return O.特修费用;
            }))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else
            {
                foreach (EquipmentData EquipmentData in this.角色装备.Values)
                {
                    if (EquipmentData.CanRepair)
                    {
                        this.NumberGoldCoins -= EquipmentData.特修费用;
                        if (EquipmentData.当前持久.V <= 0)
                        {
                            this.Stat加成[EquipmentData] = EquipmentData.装备Stat;
                            this.更新对象Stat();
                        }
                        EquipmentData.当前持久.V = EquipmentData.最大持久.V;
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new 玩家物品变动
                            {
                                物品描述 = EquipmentData.字节描述()
                            });
                        }
                    }
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new RepairItemResponsePacket());
                return;
            }
        }


        public void RequestStoreDataPacket(int 数据版本)
        {
            if (数据版本 != 0)
            {
                if (数据版本 == GameStore.StoreCount)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new SyncStoreDataPacket
                    {
                        版本编号 = GameStore.StoreCount,
                        商品数量 = 0,
                        文件内容 = new byte[0]
                    });
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new SyncStoreDataPacket
            {
                版本编号 = GameStore.StoreCount,
                商品数量 = GameStore.StoreItemsCounts,
                文件内容 = GameStore.StoreBuffer
            });
        }


        public void 查询珍宝商店(int 数据版本)
        {
            if (数据版本 != 0)
            {
                if (数据版本 == Treasures.Effect)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 同步珍宝数据
                    {
                        版本编号 = Treasures.Effect,
                        商品数量 = 0,
                        商店数据 = new byte[0]
                    });
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new 同步珍宝数据
            {
                版本编号 = Treasures.Effect,
                商品数量 = Treasures.Count,
                商店数据 = Treasures.Buffer
            });
        }


        public void 查询出售信息()
        {
        }


        public void 购买珍宝商品(int Id, int 购入数量)
        {
            Treasures 珍宝商品;
            GameItems GameItems;
            if (Treasures.DataSheet.TryGetValue(Id, out 珍宝商品) && GameItems.DataSheet.TryGetValue(Id, out GameItems))
            {
                int num;
                if (购入数量 != 1)
                {
                    if (GameItems.PersistType == PersistentItemType.堆叠)
                    {
                        num = Math.Min(购入数量, GameItems.MaxDurability);
                        goto IL_42;
                    }
                }
                num = 1;
            IL_42:
                int num2 = num;
                int num3 = 珍宝商品.CurrentPrice * num2;
                int num4 = -1;
                byte b = 0;
                while (b < this.背包大小)
                {
                    ItemData ItemData;
                    if (this.角色背包.TryGetValue(b, out ItemData) && (GameItems.PersistType != PersistentItemType.堆叠 || GameItems.Id != ItemData.Id || ItemData.当前持久.V + 购入数量 > GameItems.MaxDurability))
                    {
                        b += 1;
                    }
                    else
                    {
                        num4 = (int)b;
                    IL_A7:
                        if (num4 == -1)
                        {
                            客户网络 网络连接 = this.网络连接;
                            if (网络连接 == null)
                            {
                                return;
                            }
                            网络连接.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1793
                            });
                            return;
                        }
                        else
                        {
                            if (this.元宝数量 >= num3)
                            {
                                this.元宝数量 -= num3;
                                if (Id <= 1501000 || Id >= 1501005)
                                {
                                    this.CharacterData.消耗元宝.V += (long)num3;
                                }
                                ItemData ItemData2;
                                if (this.角色背包.TryGetValue((byte)num4, out ItemData2))
                                {
                                    ItemData2.当前持久.V += num2;
                                    客户网络 网络连接2 = this.网络连接;
                                    if (网络连接2 != null)
                                    {
                                        网络连接2.发送封包(new 玩家物品变动
                                        {
                                            物品描述 = ItemData2.字节描述()
                                        });
                                    }
                                }
                                else
                                {
                                    EquipmentItem EquipmentItem = GameItems as EquipmentItem;
                                    if (EquipmentItem != null)
                                    {
                                        this.角色背包[(byte)num4] = new EquipmentData(EquipmentItem, this.CharacterData, 1, (byte)num4, false);
                                    }
                                    else
                                    {
                                        int 持久 = 0;
                                        switch (GameItems.PersistType)
                                        {
                                            case PersistentItemType.消耗:
                                            case PersistentItemType.纯度:
                                                持久 = GameItems.MaxDurability;
                                                break;
                                            case PersistentItemType.堆叠:
                                                持久 = num2;
                                                break;
                                            case PersistentItemType.容器:
                                                持久 = 0;
                                                break;
                                        }
                                        this.角色背包[(byte)num4] = new ItemData(GameItems, this.CharacterData, 1, (byte)num4, 持久);
                                    }
                                    客户网络 网络连接3 = this.网络连接;
                                    if (网络连接3 != null)
                                    {
                                        网络连接3.发送封包(new 玩家物品变动
                                        {
                                            物品描述 = this.角色背包[(byte)num4].字节描述()
                                        });
                                    }
                                }
                                MainProcess.AddSystemLog(string.Format("Character: [{0}] [Level {1}] Purchased [{2}] * {3}, consumed $[{4}]", new object[]
                                {
                                    this.对象名字,
                                    this.当前等级,
                                    GameItems.Name,
                                    num2,
                                    num3
                                }));
                                return;
                            }
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 8451
                            });
                            return;
                        }
                    }
                }
                //goto IL_A7;
            }
        }


        public void 购买每周特惠(int 礼包编号)
        {
            if (礼包编号 == 1)
            {
                GameItems 模板;
                if (this.元宝数量 < 600)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 2561
                    });
                    return;
                }
                else if (ComputingClass.日期同周(this.CharacterData.补给日期.V, MainProcess.CurrentTime))
                {
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 8466
                    });
                    return;
                }
                else if (this.背包剩余 <= 0)
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1793
                    });
                    return;
                }
                else if (GameItems.DataSheetByName.TryGetValue("战具礼盒", out 模板))
                {
                    for (byte b = 0; b < this.背包大小; b += 1)
                    {
                        if (!this.角色背包.ContainsKey(b))
                        {
                            this.元宝数量 -= 600;
                            this.NumberGoldCoins += 165000;
                            this.双倍经验 += 500000;
                            this.CharacterData.消耗元宝.V += 600L;
                            this.角色背包[b] = new ItemData(模板, this.CharacterData, 1, b, 1);
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 != null)
                            {
                                网络连接4.发送封包(new 玩家物品变动
                                {
                                    物品描述 = this.角色背包[b].字节描述()
                                });
                            }
                            this.CharacterData.补给日期.V = MainProcess.CurrentTime;
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 != null)
                            {
                                网络连接5.发送封包(new SyncSupplementaryVariablesPacket
                                {
                                    变量类型 = 1,
                                    对象编号 = this.MapId,
                                    变量索引 = 112,
                                    变量内容 = ComputingClass.TimeShift(MainProcess.CurrentTime)
                                });
                            }
                            MainProcess.AddSystemLog(string.Format("Level [{0}][{1}] purchased [Weekly Refill Pack], consumed [600] GameCoins", this.对象名字, this.当前等级));
                            return;
                        }
                    }
                    return;
                }
            }
            else if (礼包编号 == 2)
            {
                GameItems 模板2;
                GameItems 模板3;
                if (this.元宝数量 < 3000)
                {
                    客户网络 网络连接6 = this.网络连接;
                    if (网络连接6 == null)
                    {
                        return;
                    }
                    网络连接6.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 2561
                    });
                    return;
                }
                else if (ComputingClass.日期同周(this.CharacterData.战备日期.V, MainProcess.CurrentTime))
                {
                    客户网络 网络连接7 = this.网络连接;
                    if (网络连接7 == null)
                    {
                        return;
                    }
                    网络连接7.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 8466
                    });
                    return;
                }
                else if (GameItems.DataSheetByName.TryGetValue("强化战具礼盒", out 模板2) && GameItems.DataSheetByName.TryGetValue("命运之证", out 模板3))
                {
                    if (!(this.CharacterData.战备日期.V == default(DateTime)))
                    {
                        byte b2 = byte.MaxValue;
                        byte b3 = 0;
                        while (b3 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b3))
                            {
                                b3 += 1;
                            }
                            else
                            {
                                b2 = b3;
                            IL_4B5:
                                if (b2 != 255)
                                {
                                    this.元宝数量 -= 3000;
                                    this.NumberGoldCoins += 875000;
                                    this.双倍经验 += 2750000;
                                    this.CharacterData.消耗元宝.V += 3000L;
                                    this.角色背包[b2] = new ItemData(模板2, this.CharacterData, 1, b2, 1);
                                    客户网络 网络连接8 = this.网络连接;
                                    if (网络连接8 != null)
                                    {
                                        网络连接8.发送封包(new 玩家物品变动
                                        {
                                            物品描述 = this.角色背包[b2].字节描述()
                                        });
                                    }
                                    this.CharacterData.战备日期.V = MainProcess.CurrentTime;
                                    客户网络 网络连接9 = this.网络连接;
                                    if (网络连接9 != null)
                                    {
                                        网络连接9.发送封包(new SyncSupplementaryVariablesPacket
                                        {
                                            变量类型 = 1,
                                            对象编号 = this.MapId,
                                            变量索引 = 975,
                                            变量内容 = ComputingClass.TimeShift(MainProcess.CurrentTime)
                                        });
                                    }
                                    MainProcess.AddSystemLog(string.Format("[{0}][Level {1}] Purchased [Weekly Battle Pack], consumed [3000] GameCoins", this.对象名字, this.当前等级));
                                    return;
                                }
                                客户网络 网络连接10 = this.网络连接;
                                if (网络连接10 == null)
                                {
                                    return;
                                }
                                网络连接10.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                return;
                            }
                        }
                        //goto IL_4B5;
                    }
                    byte b4 = byte.MaxValue;
                    byte b5 = byte.MaxValue;
                    for (byte b6 = 0; b6 < this.背包大小; b6 += 1)
                    {
                        if (!this.角色背包.ContainsKey(b6))
                        {
                            if (b4 == 255)
                            {
                                b4 = b6;
                            }
                            else
                            {
                                b5 = b6;
                            }
                            if (b5 != 255)
                            {
                                break;
                            }
                        }
                    }
                    if (b5 != 255)
                    {
                        this.元宝数量 -= 3000;
                        this.NumberGoldCoins += 875000;
                        this.双倍经验 += 2750000;
                        this.CharacterData.消耗元宝.V += 3000L;
                        this.角色背包[b4] = new ItemData(模板2, this.CharacterData, 1, b4, 1);
                        客户网络 网络连接11 = this.网络连接;
                        if (网络连接11 != null)
                        {
                            网络连接11.发送封包(new 玩家物品变动
                            {
                                物品描述 = this.角色背包[b4].字节描述()
                            });
                        }
                        this.角色背包[b5] = new ItemData(模板3, this.CharacterData, 1, b5, 1);
                        客户网络 网络连接12 = this.网络连接;
                        if (网络连接12 != null)
                        {
                            网络连接12.发送封包(new 玩家物品变动
                            {
                                物品描述 = this.角色背包[b5].字节描述()
                            });
                        }
                        this.CharacterData.战备日期.V = MainProcess.CurrentTime;
                        客户网络 网络连接13 = this.网络连接;
                        if (网络连接13 != null)
                        {
                            网络连接13.发送封包(new SyncSupplementaryVariablesPacket
                            {
                                变量类型 = 1,
                                对象编号 = this.MapId,
                                变量索引 = 975,
                                变量内容 = ComputingClass.TimeShift(MainProcess.CurrentTime)
                            });
                        }
                        MainProcess.AddSystemLog(string.Format("Level [{0}][{1}] purchased [Weekly Battle Pack], consumed [3000] Game Coins", this.对象名字, this.当前等级));
                        return;
                    }
                    客户网络 网络连接14 = this.网络连接;
                    if (网络连接14 == null)
                    {
                        return;
                    }
                    网络连接14.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1793
                    });
                    return;
                }
            }
            else
            {
                客户网络 网络连接15 = this.网络连接;
                if (网络连接15 == null)
                {
                    return;
                }
                网络连接15.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 8467
                });
            }
        }


        public void 购买玛法特权(byte 特权类型, byte 购买数量)
        {
            int num;
            if (特权类型 == 3)
            {
                num = 12800;
            }
            else
            {
                if (特权类型 != 4)
                {
                    if (特权类型 != 5)
                    {
                        return;
                    }
                }
                num = 28800;
            }
            if (this.元宝数量 < num)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 8451
                });
                return;
            }
            else
            {
                this.元宝数量 -= num;
                this.CharacterData.消耗元宝.V += (long)num;
                if (this.本期特权 != 0)
                {
                    MonitorDictionary<byte, int> 剩余特权 = this.剩余特权;
                    剩余特权[特权类型] += 30;
                }
                else
                {
                    this.玩家激活特权(特权类型);
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 != null)
                {
                    网络连接2.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65548,
                        第一参数 = (int)特权类型
                    });
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 != null)
                {
                    网络连接3.发送封包(new SyncPrivilegedInfoPacket
                    {
                        字节数组 = this.玛法特权描述()
                    });
                }
                if (特权类型 == 3)
                {
                    MainProcess.AddSystemLog("[" + this.对象名字 + "] Purchased [Marfa Name Jun], consumed [12,800] GameCoins");
                    return;
                }
                if (特权类型 == 4)
                {
                    MainProcess.AddSystemLog("[" + this.对象名字 + "] Purchased [Marauders], consumed [28,800] GameCoins");
                    return;
                }
                if (特权类型 == 5)
                {
                    MainProcess.AddSystemLog("[" + this.对象名字 + "] Purchased [Marfa Warlord], consumed [28,800] GameCoins");
                }
                return;
            }
        }


        public void BookMarfaPrivilegesPacket(byte 特权类型)
        {
            if (this.剩余特权[特权类型] <= 0)
            {
                return;
            }
            if (this.本期特权 == 0)
            {
                this.玩家激活特权(特权类型);
                MonitorDictionary<byte, int> 剩余特权 = this.剩余特权;
                if ((剩余特权[特权类型] -= 30) <= 0)
                {
                    this.预定特权 = 0;
                }
            }
            else
            {
                this.预定特权 = 特权类型;
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 != null)
            {
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 65550,
                    第一参数 = (int)this.预定特权
                });
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new SyncPrivilegedInfoPacket
            {
                字节数组 = this.玛法特权描述()
            });
        }


        public void 领取特权礼包(byte 特权类型, byte 礼包位置)
        {
            if (礼包位置 >= 28)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Receive privilege pack Error: Wrong pack location"));
                return;
            }
            if (特权类型 == 1)
            {
                if (this.本期特权 != 3 && this.本期特权 != 4)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65556
                    });
                    return;
                }
                else if ((MainProcess.CurrentTime.Date.AddDays(1.0) - this.本期日期.Date).TotalDays < (double)礼包位置)
                {
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65547
                    });
                    return;
                }
                else if (((ulong)this.本期记录 & (ulong)(1L << (int)(礼包位置 & 31))) == 0UL)
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65546
                    });
                    return;
                }
                else
                {
                    int num = (int)(礼包位置 % 7);
                    if (num == 0)
                    {
                        this.本期记录 &= ~(1U << (int)礼包位置);
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 != null)
                        {
                            网络连接4.发送封包(new SyncPrivilegedInfoPacket
                            {
                                字节数组 = this.玛法特权描述()
                            });
                        }
                        this.NumberGoldCoins += ((this.本期特权 == 3) ? 50000 : 100000);
                        return;
                    }
                    if (num == 1)
                    {
                        byte b = byte.MaxValue;
                        byte b2 = 0;
                        while (b2 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b2))
                            {
                                b2 += 1;
                            }
                            else
                            {
                                b = b2;
                            IL_181:
                                if (b == 255)
                                {
                                    客户网络 网络连接5 = this.网络连接;
                                    if (网络连接5 == null)
                                    {
                                        return;
                                    }
                                    网络连接5.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板;
                                    if (!GameItems.DataSheetByName.TryGetValue((this.本期特权 == 3) ? "名俊铭文石礼包" : "豪杰铭文石礼包", out 模板))
                                    {
                                        return;
                                    }
                                    this.本期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接6 = this.网络连接;
                                    if (网络连接6 != null)
                                    {
                                        网络连接6.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b] = new ItemData(模板, this.CharacterData, 1, b, 1);
                                    客户网络 网络连接7 = this.网络连接;
                                    if (网络连接7 == null)
                                    {
                                        return;
                                    }
                                    网络连接7.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_181;
                    }
                    if (num == 2)
                    {
                        byte b3 = byte.MaxValue;
                        byte b4 = 0;
                        while (b4 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b4))
                            {
                                b4 += 1;
                            }
                            else
                            {
                                b3 = b4;
                            IL_28C:
                                if (b3 == 255)
                                {
                                    客户网络 网络连接8 = this.网络连接;
                                    if (网络连接8 == null)
                                    {
                                        return;
                                    }
                                    网络连接8.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板2;
                                    if (!GameItems.DataSheetByName.TryGetValue("随机传送石", out 模板2))
                                    {
                                        return;
                                    }
                                    this.本期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接9 = this.网络连接;
                                    if (网络连接9 != null)
                                    {
                                        网络连接9.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b3] = new ItemData(模板2, this.CharacterData, 1, b3, 50);
                                    客户网络 网络连接10 = this.网络连接;
                                    if (网络连接10 == null)
                                    {
                                        return;
                                    }
                                    网络连接10.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b3].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_28C;
                    }
                    if (num == 3)
                    {
                        byte b5 = byte.MaxValue;
                        byte b6 = 0;
                        while (b6 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b6))
                            {
                                b6 += 1;
                            }
                            else
                            {
                                b5 = b6;
                            IL_389:
                                if (b5 == 255)
                                {
                                    客户网络 网络连接11 = this.网络连接;
                                    if (网络连接11 == null)
                                    {
                                        return;
                                    }
                                    网络连接11.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板3;
                                    if (!GameItems.DataSheetByName.TryGetValue((this.本期特权 == 3) ? "名俊灵石宝盒" : "豪杰灵石宝盒", out 模板3))
                                    {
                                        return;
                                    }
                                    this.本期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接12 = this.网络连接;
                                    if (网络连接12 != null)
                                    {
                                        网络连接12.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b5] = new ItemData(模板3, this.CharacterData, 1, b5, 1);
                                    客户网络 网络连接13 = this.网络连接;
                                    if (网络连接13 == null)
                                    {
                                        return;
                                    }
                                    网络连接13.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b5].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_389;
                    }
                    if (num == 4)
                    {
                        byte b7 = byte.MaxValue;
                        byte b8 = 0;
                        while (b8 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b8))
                            {
                                b8 += 1;
                            }
                            else
                            {
                                b7 = b8;
                            IL_493:
                                if (b7 == 255)
                                {
                                    客户网络 网络连接14 = this.网络连接;
                                    if (网络连接14 == null)
                                    {
                                        return;
                                    }
                                    网络连接14.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板4;
                                    if (!GameItems.DataSheetByName.TryGetValue("雕色石", out 模板4))
                                    {
                                        return;
                                    }
                                    this.本期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接15 = this.网络连接;
                                    if (网络连接15 != null)
                                    {
                                        网络连接15.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b7] = new ItemData(模板4, this.CharacterData, 1, b7, (this.本期特权 == 3) ? 1 : 2);
                                    客户网络 网络连接16 = this.网络连接;
                                    if (网络连接16 == null)
                                    {
                                        return;
                                    }
                                    网络连接16.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b7].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_493;
                    }
                    if (num == 5)
                    {
                        byte b9 = byte.MaxValue;
                        byte b10 = 0;
                        while (b10 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b10))
                            {
                                b10 += 1;
                            }
                            else
                            {
                                b9 = b10;
                            IL_596:
                                if (b9 == 255)
                                {
                                    客户网络 网络连接17 = this.网络连接;
                                    if (网络连接17 == null)
                                    {
                                        return;
                                    }
                                    网络连接17.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板5;
                                    if (!GameItems.DataSheetByName.TryGetValue("修复油", out 模板5))
                                    {
                                        return;
                                    }
                                    this.本期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接18 = this.网络连接;
                                    if (网络连接18 != null)
                                    {
                                        网络连接18.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b9] = new ItemData(模板5, this.CharacterData, 1, b9, (this.本期特权 == 3) ? 1 : 2);
                                    客户网络 网络连接19 = this.网络连接;
                                    if (网络连接19 == null)
                                    {
                                        return;
                                    }
                                    网络连接19.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b9].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_596;
                    }
                    if (num == 6)
                    {
                        byte b11 = byte.MaxValue;
                        byte b12 = 0;
                        while (b12 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b12))
                            {
                                b12 += 1;
                            }
                            else
                            {
                                b11 = b12;
                            IL_69E:
                                if (b11 == 255)
                                {
                                    客户网络 网络连接20 = this.网络连接;
                                    if (网络连接20 == null)
                                    {
                                        return;
                                    }
                                    网络连接20.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板6;
                                    if (!GameItems.DataSheetByName.TryGetValue("祝福油", out 模板6))
                                    {
                                        return;
                                    }
                                    this.本期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接21 = this.网络连接;
                                    if (网络连接21 != null)
                                    {
                                        网络连接21.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b11] = new ItemData(模板6, this.CharacterData, 1, b11, (this.本期特权 == 3) ? 2 : 4);
                                    客户网络 网络连接22 = this.网络连接;
                                    if (网络连接22 == null)
                                    {
                                        return;
                                    }
                                    网络连接22.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b11].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_69E;
                    }
                }
            }
            else if (特权类型 == 2)
            {
                if (this.上期特权 != 3 && this.上期特权 != 4)
                {
                    客户网络 网络连接23 = this.网络连接;
                    if (网络连接23 == null)
                    {
                        return;
                    }
                    网络连接23.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65556
                    });
                    return;
                }
                else if (((ulong)this.上期记录 & (ulong)(1L << (int)(礼包位置 & 31))) == 0UL)
                {
                    客户网络 网络连接24 = this.网络连接;
                    if (网络连接24 == null)
                    {
                        return;
                    }
                    网络连接24.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65546
                    });
                    return;
                }
                else
                {
                    int num2 = (int)(礼包位置 % 7);
                    if (num2 == 0)
                    {
                        this.上期记录 &= ~(1U << (int)礼包位置);
                        客户网络 网络连接25 = this.网络连接;
                        if (网络连接25 != null)
                        {
                            网络连接25.发送封包(new SyncPrivilegedInfoPacket
                            {
                                字节数组 = this.玛法特权描述()
                            });
                        }
                        this.NumberGoldCoins += ((this.上期特权 == 3) ? 50000 : 100000);
                        return;
                    }
                    if (num2 == 1)
                    {
                        byte b13 = byte.MaxValue;
                        byte b14 = 0;
                        while (b14 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b14))
                            {
                                b14 += 1;
                            }
                            else
                            {
                                b13 = b14;
                            IL_86E:
                                if (b13 == 255)
                                {
                                    客户网络 网络连接26 = this.网络连接;
                                    if (网络连接26 == null)
                                    {
                                        return;
                                    }
                                    网络连接26.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板7;
                                    if (!GameItems.DataSheetByName.TryGetValue((this.上期特权 == 3) ? "名俊铭文石礼包" : "豪杰铭文石礼包", out 模板7))
                                    {
                                        return;
                                    }
                                    this.上期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接27 = this.网络连接;
                                    if (网络连接27 != null)
                                    {
                                        网络连接27.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b13] = new ItemData(模板7, this.CharacterData, 1, b13, 1);
                                    客户网络 网络连接28 = this.网络连接;
                                    if (网络连接28 == null)
                                    {
                                        return;
                                    }
                                    网络连接28.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b13].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_86E;
                    }
                    if (num2 == 2)
                    {
                        byte b15 = byte.MaxValue;
                        byte b16 = 0;
                        while (b16 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b16))
                            {
                                b16 += 1;
                            }
                            else
                            {
                                b15 = b16;
                            IL_97A:
                                if (b15 == 255)
                                {
                                    客户网络 网络连接29 = this.网络连接;
                                    if (网络连接29 == null)
                                    {
                                        return;
                                    }
                                    网络连接29.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板8;
                                    if (!GameItems.DataSheetByName.TryGetValue("随机传送石", out 模板8))
                                    {
                                        return;
                                    }
                                    this.上期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接30 = this.网络连接;
                                    if (网络连接30 != null)
                                    {
                                        网络连接30.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b15] = new ItemData(模板8, this.CharacterData, 1, b15, 50);
                                    客户网络 网络连接31 = this.网络连接;
                                    if (网络连接31 == null)
                                    {
                                        return;
                                    }
                                    网络连接31.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b15].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_97A;
                    }
                    if (num2 == 3)
                    {
                        byte b17 = byte.MaxValue;
                        byte b18 = 0;
                        while (b18 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b18))
                            {
                                b18 += 1;
                            }
                            else
                            {
                                b17 = b18;
                            IL_A77:
                                if (b17 == 255)
                                {
                                    客户网络 网络连接32 = this.网络连接;
                                    if (网络连接32 == null)
                                    {
                                        return;
                                    }
                                    网络连接32.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板9;
                                    if (!GameItems.DataSheetByName.TryGetValue((this.上期特权 == 3) ? "名俊灵石宝盒" : "豪杰灵石宝盒", out 模板9))
                                    {
                                        return;
                                    }
                                    this.上期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接33 = this.网络连接;
                                    if (网络连接33 != null)
                                    {
                                        网络连接33.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b17] = new ItemData(模板9, this.CharacterData, 1, b17, 1);
                                    客户网络 网络连接34 = this.网络连接;
                                    if (网络连接34 == null)
                                    {
                                        return;
                                    }
                                    网络连接34.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b17].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_A77;
                    }
                    if (num2 == 4)
                    {
                        byte b19 = byte.MaxValue;
                        byte b20 = 0;
                        while (b20 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b20))
                            {
                                b20 += 1;
                            }
                            else
                            {
                                b19 = b20;
                            IL_B83:
                                if (b19 == 255)
                                {
                                    客户网络 网络连接35 = this.网络连接;
                                    if (网络连接35 == null)
                                    {
                                        return;
                                    }
                                    网络连接35.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板10;
                                    if (!GameItems.DataSheetByName.TryGetValue("雕色石", out 模板10))
                                    {
                                        return;
                                    }
                                    this.上期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接36 = this.网络连接;
                                    if (网络连接36 != null)
                                    {
                                        网络连接36.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b19] = new ItemData(模板10, this.CharacterData, 1, b19, (this.上期特权 == 3) ? 1 : 2);
                                    客户网络 网络连接37 = this.网络连接;
                                    if (网络连接37 == null)
                                    {
                                        return;
                                    }
                                    网络连接37.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b19].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_B83;
                    }
                    if (num2 == 5)
                    {
                        byte b21 = byte.MaxValue;
                        byte b22 = 0;
                        while (b22 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b22))
                            {
                                b22 += 1;
                            }
                            else
                            {
                                b21 = b22;
                            IL_C8B:
                                if (b21 == 255)
                                {
                                    客户网络 网络连接38 = this.网络连接;
                                    if (网络连接38 == null)
                                    {
                                        return;
                                    }
                                    网络连接38.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板11;
                                    if (!GameItems.DataSheetByName.TryGetValue("修复油", out 模板11))
                                    {
                                        return;
                                    }
                                    this.上期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接39 = this.网络连接;
                                    if (网络连接39 != null)
                                    {
                                        网络连接39.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b21] = new ItemData(模板11, this.CharacterData, 1, b21, (this.上期特权 == 3) ? 1 : 2);
                                    客户网络 网络连接40 = this.网络连接;
                                    if (网络连接40 == null)
                                    {
                                        return;
                                    }
                                    网络连接40.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b21].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_C8B;
                    }
                    if (num2 == 6)
                    {
                        byte b23 = byte.MaxValue;
                        byte b24 = 0;
                        while (b24 < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b24))
                            {
                                b24 += 1;
                            }
                            else
                            {
                                b23 = b24;
                            IL_D93:
                                if (b23 == 255)
                                {
                                    客户网络 网络连接41 = this.网络连接;
                                    if (网络连接41 == null)
                                    {
                                        return;
                                    }
                                    网络连接41.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 6459
                                    });
                                    return;
                                }
                                else
                                {
                                    GameItems 模板12;
                                    if (!GameItems.DataSheetByName.TryGetValue("祝福油", out 模板12))
                                    {
                                        return;
                                    }
                                    this.上期记录 &= ~(1U << (int)礼包位置);
                                    客户网络 网络连接42 = this.网络连接;
                                    if (网络连接42 != null)
                                    {
                                        网络连接42.发送封包(new SyncPrivilegedInfoPacket
                                        {
                                            字节数组 = this.玛法特权描述()
                                        });
                                    }
                                    this.角色背包[b23] = new ItemData(模板12, this.CharacterData, 1, b23, (this.上期特权 == 3) ? 2 : 4);
                                    客户网络 网络连接43 = this.网络连接;
                                    if (网络连接43 == null)
                                    {
                                        return;
                                    }
                                    网络连接43.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b23].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                        //goto IL_D93;
                    }
                }
            }
            else
            {
                客户网络 网络连接44 = this.网络连接;
                if (网络连接44 == null)
                {
                    return;
                }
                网络连接44.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 65556
                });
            }
        }


        public void 玩家使用称号(byte Id)
        {
            GameTitle 游戏称号;
            if (!this.称号列表.ContainsKey(Id))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5377
                });
                return;
            }
            else if (!GameTitle.DataSheet.TryGetValue(Id, out 游戏称号))
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5378
                });
                return;
            }
            else
            {
                if (this.当前称号 != Id)
                {
                    if (this.当前称号 != 0)
                    {
                        this.CombatBonus.Remove(this.当前称号);
                        this.Stat加成.Remove(this.当前称号);
                    }
                    this.当前称号 = Id;
                    this.CombatBonus[Id] = 游戏称号.Combat;
                    this.更新玩家战力();
                    this.Stat加成[Id] = 游戏称号.Attributes;
                    this.更新对象Stat();
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 != null)
                    {
                        网络连接3.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1500,
                            第一参数 = (int)Id
                        });
                    }
                    base.发送封包(new 同步装配称号
                    {
                        对象编号 = this.MapId,
                        Id = Id
                    });
                    return;
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new 同步装配称号
                {
                    对象编号 = this.MapId,
                    Id = Id
                });
                return;
            }
        }


        public void 玩家卸下称号()
        {
            if (this.当前称号 == 0)
            {
                return;
            }
            if (this.CombatBonus.Remove(this.当前称号))
            {
                this.更新玩家战力();
            }
            if (this.Stat加成.Remove(this.当前称号))
            {
                this.更新对象Stat();
            }
            this.当前称号 = 0;
            base.发送封包(new 同步装配称号
            {
                对象编号 = this.MapId
            });
        }


        public void 玩家整理背包(byte 背包类型)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                if (背包类型 == 1)
                {
                    List<ItemData> list = this.角色背包.Values.ToList<ItemData>();
                    list.Sort((ItemData a, ItemData b) => b.Id.CompareTo(a.Id));
                    byte b5 = 0;
                    while ((int)b5 < list.Count)
                    {
                        if (list[(int)b5].能否堆叠 && list[(int)b5].当前持久.V < list[(int)b5].最大持久.V)
                        {
                            for (int i = (int)(b5 + 1); i < list.Count; i++)
                            {
                                if (list[(int)b5].Id == list[i].Id)
                                {
                                    int num;
                                    list[(int)b5].当前持久.V += (num = Math.Min(list[(int)b5].最大持久.V - list[(int)b5].当前持久.V, list[i].当前持久.V));
                                    if ((list[i].当前持久.V -= num) <= 0)
                                    {
                                        list[i].Delete();
                                        list.RemoveAt(i);
                                        i--;
                                    }
                                    if (list[(int)b5].当前持久.V >= list[(int)b5].最大持久.V)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        b5 += 1;
                    }
                    this.角色背包.Clear();
                    byte b2 = 0;
                    while ((int)b2 < list.Count)
                    {
                        this.角色背包[b2] = list[(int)b2];
                        this.角色背包[b2].当前位置 = b2;
                        b2 += 1;
                    }
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new SyncBackpackInfoPacket
                        {
                            物品描述 = this.背包物品描述()
                        });
                    }
                }
                if (背包类型 == 2)
                {
                    List<ItemData> list2 = this.角色仓库.Values.ToList<ItemData>();
                    list2.Sort((ItemData a, ItemData b) => b.Id.CompareTo(a.Id));
                    byte b3 = 0;
                    while ((int)b3 < list2.Count)
                    {
                        if (list2[(int)b3].能否堆叠 && list2[(int)b3].当前持久.V < list2[(int)b3].最大持久.V)
                        {
                            for (int j = (int)(b3 + 1); j < list2.Count; j++)
                            {
                                if (list2[(int)b3].Id == list2[j].Id)
                                {
                                    int num2;
                                    list2[(int)b3].当前持久.V += (num2 = Math.Min(list2[(int)b3].最大持久.V - list2[(int)b3].当前持久.V, list2[j].当前持久.V));
                                    if ((list2[j].当前持久.V -= num2) <= 0)
                                    {
                                        list2[j].Delete();
                                        list2.RemoveAt(j);
                                        j--;
                                    }
                                    if (list2[(int)b3].当前持久.V >= list2[(int)b3].最大持久.V)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        b3 += 1;
                    }
                    this.角色仓库.Clear();
                    byte b4 = 0;
                    while ((int)b4 < list2.Count)
                    {
                        this.角色仓库[b4] = list2[(int)b4];
                        this.角色仓库[b4].当前位置 = b4;
                        b4 += 1;
                    }
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new SyncBackpackInfoPacket
                    {
                        物品描述 = this.仓库物品描述()
                    });
                }
                return;
            }
        }


        public void 玩家拾取物品(ItemObject 物品)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (物品.物品绑定 && !物品.物品归属.Contains(this.CharacterData))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2310
                });
                return;
            }
            else if (物品.物品归属.Count != 0 && !物品.物品归属.Contains(this.CharacterData) && MainProcess.CurrentTime < 物品.归属时间)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2307
                });
                return;
            }
            else if (物品.Weight != 0 && 物品.Weight > this.最大负重 - this.背包重量)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1863
                });
                return;
            }
            else if (物品.默认持久 != 0 && this.背包剩余 <= 0)
            {
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1793
                });
                return;
            }
            else
            {
                if (物品.Id == 1)
                {
                    客户网络 网络连接5 = this.网络连接;
                    if (网络连接5 != null)
                    {
                        网络连接5.发送封包(new 玩家拾取金币
                        {
                            NumberGoldCoins = 物品.堆叠数量
                        });
                    }
                    this.NumberGoldCoins += 物品.堆叠数量;
                    物品.物品转移处理();
                    return;
                }
                for (byte b = 0; b < this.背包大小; b += 1)
                {
                    if (!this.角色背包.ContainsKey(b))
                    {
                        if (物品.ItemData != null)
                        {
                            this.角色背包[b] = 物品.ItemData;
                            物品.ItemData.物品位置.V = b;
                            物品.ItemData.物品容器.V = 1;
                        }
                        else
                        {
                            EquipmentItem EquipmentItem = 物品.物品模板 as EquipmentItem;
                            if (EquipmentItem != null)
                            {
                                this.角色背包[b] = new EquipmentData(EquipmentItem, this.CharacterData, 1, b, true);
                            }
                            else if (物品.PersistType == PersistentItemType.容器)
                            {
                                this.角色背包[b] = new ItemData(物品.物品模板, this.CharacterData, 1, b, 0);
                            }
                            else if (物品.PersistType == PersistentItemType.堆叠)
                            {
                                this.角色背包[b] = new ItemData(物品.物品模板, this.CharacterData, 1, b, 物品.堆叠数量);
                            }
                            else
                            {
                                this.角色背包[b] = new ItemData(物品.物品模板, this.CharacterData, 1, b, 物品.默认持久);
                            }
                        }
                        客户网络 网络连接6 = this.网络连接;
                        if (网络连接6 != null)
                        {
                            网络连接6.发送封包(new 玩家拾取物品
                            {
                                物品描述 = this.角色背包[b].字节描述(),
                                角色编号 = this.MapId
                            });
                        }
                        客户网络 网络连接7 = this.网络连接;
                        if (网络连接7 != null)
                        {
                            网络连接7.发送封包(new 玩家物品变动
                            {
                                物品描述 = this.角色背包[b].字节描述()
                            });
                        }
                        物品.物品转移处理();
                        return;
                    }
                }
                return;
            }
        }


        public void 玩家丢弃物品(byte 背包类型, byte 物品位置, ushort 丢弃数量)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3 && this.当前等级 > 7)
            {
                ItemData ItemData;
                if (背包类型 == 1 && this.角色背包.TryGetValue(物品位置, out ItemData))
                {
                    if (ItemData.IsBound)
                    {
                        new ItemObject(ItemData.物品模板, ItemData, this.当前地图, this.当前坐标, new HashSet<CharacterData>
                        {
                            this.CharacterData
                        }, 0, true);
                    }
                    else
                    {
                        new ItemObject(ItemData.物品模板, ItemData, this.当前地图, this.当前坐标, new HashSet<CharacterData>(), 0, false);
                    }
                    this.角色背包.Remove(ItemData.物品位置.V);
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 删除玩家物品
                    {
                        背包类型 = 背包类型,
                        物品位置 = 物品位置
                    });
                }
                return;
            }
        }


        public void 玩家拆分物品(byte 当前背包, byte 物品位置, ushort 拆分数量, byte 目标背包, byte 目标位置)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                ItemData ItemData;
                ItemData ItemData2;
                if (当前背包 == 1 && this.角色背包.TryGetValue(物品位置, out ItemData) && 目标背包 == 1 && 目标位置 < this.背包大小 && ItemData != null && ItemData.PersistType == PersistentItemType.堆叠 && ItemData.当前持久.V > (int)拆分数量 && !this.角色背包.TryGetValue(目标位置, out ItemData2))
                {
                    ItemData.当前持久.V -= (int)拆分数量;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new 玩家物品变动
                        {
                            物品描述 = ItemData.字节描述()
                        });
                    }
                    this.角色背包[目标位置] = new ItemData(ItemData.物品模板, this.CharacterData, 目标背包, 目标位置, (int)拆分数量);
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new 玩家物品变动
                    {
                        物品描述 = this.角色背包[目标位置].字节描述()
                    });
                }
                return;
            }
        }


        public void 玩家分解物品(byte 背包类型, byte 物品位置, byte 分解数量)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                if (背包类型 != 1)
                {
                    this.网络连接.尝试断开连接(new Exception("错误操作: 玩家分解物品.  错误: 背包类型错误."));
                    return;
                }
                ItemData ItemData;
                if (this.角色背包.TryGetValue(物品位置, out ItemData))
                {
                    EquipmentData EquipmentData = ItemData as EquipmentData;
                    if (EquipmentData != null && EquipmentData.CanSold)
                    {
                        if (this.CharacterData.分解日期.V.Date != MainProcess.CurrentTime.Date)
                        {
                            this.CharacterData.分解日期.V = MainProcess.CurrentTime;
                            this.CharacterData.分解经验.V = 0;
                        }
                        int SalePrice = EquipmentData.SalePrice;
                        int num = (int)Math.Max(0f, (float)SalePrice * (1f - (float)this.CharacterData.分解经验.V / 1500000f));
                        this.NumberGoldCoins += Math.Max(1, SalePrice / 2);
                        this.双倍经验 += num;
                        this.CharacterData.分解经验.V += num;
                        this.角色背包.Remove(EquipmentData.当前位置);
                        EquipmentData.Delete();
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 删除玩家物品
                        {
                            背包类型 = 背包类型,
                            物品位置 = 物品位置
                        });
                    }
                    return;
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1877
                });
                return;
            }
        }


        public void 玩家转移物品(byte 当前背包, byte 当前位置, byte 目标背包, byte 目标位置)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (当前背包 == 0 && 当前位置 >= 16)
            {
                return;
            }
            if (当前背包 == 1 && 当前位置 >= this.背包大小)
            {
                return;
            }
            if (当前背包 == 2 && 当前位置 >= this.仓库大小)
            {
                return;
            }
            if (目标背包 == 0 && 目标位置 >= 16)
            {
                return;
            }
            if (目标背包 == 1 && 目标位置 >= this.背包大小)
            {
                return;
            }
            if (目标背包 == 2 && 目标位置 >= this.仓库大小)
            {
                return;
            }
            ItemData ItemData = null;
            if (当前背包 == 0)
            {
                EquipmentData EquipmentData;
                ItemData = (this.角色装备.TryGetValue(当前位置, out EquipmentData) ? EquipmentData : null);
            }
            if (当前背包 == 1)
            {
                ItemData ItemData2;
                ItemData = (this.角色背包.TryGetValue(当前位置, out ItemData2) ? ItemData2 : null);
            }
            if (当前背包 == 2)
            {
                ItemData ItemData3;
                ItemData = (this.角色仓库.TryGetValue(当前位置, out ItemData3) ? ItemData3 : null);
            }
            ItemData ItemData4 = null;
            if (目标背包 == 0)
            {
                EquipmentData EquipmentData2;
                ItemData4 = (this.角色装备.TryGetValue(目标位置, out EquipmentData2) ? EquipmentData2 : null);
            }
            if (目标背包 == 1)
            {
                ItemData ItemData5;
                ItemData4 = (this.角色背包.TryGetValue(目标位置, out ItemData5) ? ItemData5 : null);
            }
            if (目标背包 == 2)
            {
                ItemData ItemData6;
                ItemData4 = (this.角色仓库.TryGetValue(目标位置, out ItemData6) ? ItemData6 : null);
            }
            if (ItemData == null && ItemData4 == null)
            {
                return;
            }
            if (当前背包 == 0 && 目标背包 == 0)
            {
                return;
            }
            if (当前背包 == 0 && 目标背包 == 2)
            {
                return;
            }
            if (当前背包 == 2 && 目标背包 == 0)
            {
                return;
            }
            if (ItemData != null && 当前背包 == 0 && (ItemData as EquipmentData).DisableDismount)
            {
                return;
            }
            if (ItemData4 != null && 目标背包 == 0 && (ItemData4 as EquipmentData).DisableDismount)
            {
                return;
            }
            if (ItemData != null && 目标背包 == 0)
            {
                EquipmentData EquipmentData3 = ItemData as EquipmentData;
                if (EquipmentData3 == null)
                {
                    return;
                }
                if (EquipmentData3.NeedLevel > (int)this.当前等级)
                {
                    return;
                }
                if (EquipmentData3.NeedGender != GameObjectGender.不限 && EquipmentData3.NeedGender != this.角色性别)
                {
                    return;
                }
                if (EquipmentData3.NeedRace != GameObjectRace.通用 && EquipmentData3.NeedRace != this.角色职业)
                {
                    return;
                }
                if (EquipmentData3.NeedAttack > this[GameObjectStats.MaxAttack])
                {
                    return;
                }
                if (EquipmentData3.NeedMagic > this[GameObjectStats.MaxMagic])
                {
                    return;
                }
                if (EquipmentData3.NeedTaoism > this[GameObjectStats.GreatestTaoism])
                {
                    return;
                }
                if (EquipmentData3.NeedAcupuncture > this[GameObjectStats.MaxNeedle])
                {
                    return;
                }
                if (EquipmentData3.NeedArchery > this[GameObjectStats.MaxBow])
                {
                    return;
                }
                if (目标位置 == 0 && EquipmentData3.Weight > this.最大腕力)
                {
                    return;
                }
                if (目标位置 != 0)
                {
                    int? num = EquipmentData3.Weight - ((ItemData4 != null) ? new int?(ItemData4.Weight) : null);
                    int num2 = this.最大穿戴 - this.装备重量;
                    if (num.GetValueOrDefault() > num2 & num != null)
                    {
                        return;
                    }
                }
                if (目标位置 == 0 && EquipmentData3.物品类型 != ItemType.武器)
                {
                    return;
                }
                if (目标位置 == 1 && EquipmentData3.物品类型 != ItemType.衣服)
                {
                    return;
                }
                if (目标位置 == 2 && EquipmentData3.物品类型 != ItemType.披风)
                {
                    return;
                }
                if (目标位置 == 3 && EquipmentData3.物品类型 != ItemType.头盔)
                {
                    return;
                }
                if (目标位置 == 4 && EquipmentData3.物品类型 != ItemType.护肩)
                {
                    return;
                }
                if (目标位置 == 5 && EquipmentData3.物品类型 != ItemType.护腕)
                {
                    return;
                }
                if (目标位置 == 6 && EquipmentData3.物品类型 != ItemType.腰带)
                {
                    return;
                }
                if (目标位置 == 7 && EquipmentData3.物品类型 != ItemType.鞋子)
                {
                    return;
                }
                if (目标位置 == 8 && EquipmentData3.物品类型 != ItemType.项链)
                {
                    return;
                }
                if (目标位置 == 13 && EquipmentData3.物品类型 != ItemType.勋章)
                {
                    return;
                }
                if (目标位置 == 14 && EquipmentData3.物品类型 != ItemType.玉佩)
                {
                    return;
                }
                if (目标位置 == 15 && EquipmentData3.物品类型 != ItemType.战具)
                {
                    return;
                }
                if (目标位置 == 9 && EquipmentData3.物品类型 != ItemType.戒指)
                {
                    return;
                }
                if (目标位置 == 10 && EquipmentData3.物品类型 != ItemType.戒指)
                {
                    return;
                }
                if (目标位置 == 11 && EquipmentData3.物品类型 != ItemType.手镯)
                {
                    return;
                }
                if (目标位置 == 12 && EquipmentData3.物品类型 != ItemType.手镯)
                {
                    return;
                }
            }
            if (ItemData4 != null && 当前背包 == 0)
            {
                EquipmentData EquipmentData4 = ItemData4 as EquipmentData;
                if (EquipmentData4 == null)
                {
                    return;
                }
                if (EquipmentData4.NeedLevel > (int)this.当前等级)
                {
                    return;
                }
                if (EquipmentData4.NeedGender != GameObjectGender.不限 && EquipmentData4.NeedGender != this.角色性别)
                {
                    return;
                }
                if (EquipmentData4.NeedRace != GameObjectRace.通用 && EquipmentData4.NeedRace != this.角色职业)
                {
                    return;
                }
                if (EquipmentData4.NeedAttack > this[GameObjectStats.MaxAttack])
                {
                    return;
                }
                if (EquipmentData4.NeedMagic > this[GameObjectStats.MaxMagic])
                {
                    return;
                }
                if (EquipmentData4.NeedTaoism > this[GameObjectStats.GreatestTaoism])
                {
                    return;
                }
                if (EquipmentData4.NeedAcupuncture > this[GameObjectStats.MaxNeedle])
                {
                    return;
                }
                if (EquipmentData4.NeedArchery > this[GameObjectStats.MaxBow])
                {
                    return;
                }
                if (当前位置 == 0 && EquipmentData4.Weight > this.最大腕力)
                {
                    return;
                }
                if (当前位置 != 0)
                {
                    int? num = EquipmentData4.Weight - ((ItemData != null) ? new int?(ItemData.Weight) : null);
                    int num2 = this.最大穿戴 - this.装备重量;
                    if (num.GetValueOrDefault() > num2 & num != null)
                    {
                        return;
                    }
                }
                if (当前位置 == 0 && EquipmentData4.物品类型 != ItemType.武器)
                {
                    return;
                }
                if (当前位置 == 1 && EquipmentData4.物品类型 != ItemType.衣服)
                {
                    return;
                }
                if (当前位置 == 2 && EquipmentData4.物品类型 != ItemType.披风)
                {
                    return;
                }
                if (当前位置 == 3 && EquipmentData4.物品类型 != ItemType.头盔)
                {
                    return;
                }
                if (当前位置 == 4 && EquipmentData4.物品类型 != ItemType.护肩)
                {
                    return;
                }
                if (当前位置 == 5 && EquipmentData4.物品类型 != ItemType.护腕)
                {
                    return;
                }
                if (当前位置 == 6 && EquipmentData4.物品类型 != ItemType.腰带)
                {
                    return;
                }
                if (当前位置 == 7 && EquipmentData4.物品类型 != ItemType.鞋子)
                {
                    return;
                }
                if (当前位置 == 8 && EquipmentData4.物品类型 != ItemType.项链)
                {
                    return;
                }
                if (当前位置 == 13 && EquipmentData4.物品类型 != ItemType.勋章)
                {
                    return;
                }
                if (当前位置 == 14 && EquipmentData4.物品类型 != ItemType.玉佩)
                {
                    return;
                }
                if (当前位置 == 15 && EquipmentData4.物品类型 != ItemType.战具)
                {
                    return;
                }
                if (当前位置 == 9 && EquipmentData4.物品类型 != ItemType.戒指)
                {
                    return;
                }
                if (当前位置 == 10 && EquipmentData4.物品类型 != ItemType.戒指)
                {
                    return;
                }
                if (当前位置 == 11 && EquipmentData4.物品类型 != ItemType.手镯)
                {
                    return;
                }
                if (当前位置 == 12 && EquipmentData4.物品类型 != ItemType.手镯)
                {
                    return;
                }
            }
            if (ItemData != null && ItemData4 != null && ItemData.能否堆叠 && ItemData4.Id == ItemData.Id && ItemData.堆叠上限 > ItemData.当前持久.V && ItemData4.堆叠上限 > ItemData4.当前持久.V)
            {
                int num3 = Math.Min(ItemData.当前持久.V, ItemData4.堆叠上限 - ItemData4.当前持久.V);
                ItemData4.当前持久.V += num3;
                ItemData.当前持久.V -= num3;
                客户网络 网络连接 = this.网络连接;
                if (网络连接 != null)
                {
                    网络连接.发送封包(new 玩家物品变动
                    {
                        物品描述 = ItemData4.字节描述()
                    });
                }
                if (ItemData.当前持久.V <= 0)
                {
                    ItemData.Delete();
                    if (当前背包 != 1)
                    {
                        if (当前背包 == 2)
                        {
                            this.角色仓库.Remove(当前位置);
                        }
                    }
                    else
                    {
                        this.角色背包.Remove(当前位置);
                    }
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new 删除玩家物品
                    {
                        背包类型 = 当前背包,
                        物品位置 = 当前位置
                    });
                    return;
                }
                else
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new 玩家物品变动
                    {
                        物品描述 = ItemData.字节描述()
                    });
                    return;
                }
            }
            else
            {
                if (ItemData != null)
                {
                    switch (当前背包)
                    {
                        case 0:
                            this.角色装备.Remove(当前位置);
                            break;
                        case 1:
                            this.角色背包.Remove(当前位置);
                            break;
                        case 2:
                            this.角色仓库.Remove(当前位置);
                            break;
                    }
                    ItemData.物品容器.V = 目标背包;
                    ItemData.物品位置.V = 目标位置;
                }
                if (ItemData4 != null)
                {
                    switch (目标背包)
                    {
                        case 0:
                            this.角色装备.Remove(目标位置);
                            break;
                        case 1:
                            this.角色背包.Remove(目标位置);
                            break;
                        case 2:
                            this.角色仓库.Remove(目标位置);
                            break;
                    }
                    ItemData4.物品容器.V = 当前背包;
                    ItemData4.物品位置.V = 当前位置;
                }
                if (ItemData != null)
                {
                    switch (目标背包)
                    {
                        case 0:
                            this.角色装备[目标位置] = (ItemData as EquipmentData);
                            break;
                        case 1:
                            this.角色背包[目标位置] = ItemData;
                            break;
                        case 2:
                            this.角色仓库[目标位置] = ItemData;
                            break;
                    }
                }
                if (ItemData4 != null)
                {
                    switch (当前背包)
                    {
                        case 0:
                            this.角色装备[当前位置] = (ItemData4 as EquipmentData);
                            break;
                        case 1:
                            this.角色背包[当前位置] = ItemData4;
                            break;
                        case 2:
                            this.角色仓库[当前位置] = ItemData4;
                            break;
                    }
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 != null)
                {
                    网络连接4.发送封包(new 玩家转移物品
                    {
                        原有容器 = 当前背包,
                        目标容器 = 目标背包,
                        原有位置 = 当前位置,
                        目标位置 = 目标位置
                    });
                }
                if (目标背包 == 0)
                {
                    this.玩家穿卸装备((EquipmentWearingParts)目标位置, (EquipmentData)ItemData4, (EquipmentData)ItemData);
                    return;
                }
                if (当前背包 == 0)
                {
                    this.玩家穿卸装备((EquipmentWearingParts)当前位置, (EquipmentData)ItemData, (EquipmentData)ItemData4);
                    return;
                }
                return;
            }
        }

        public void UseItem(byte backpackType, byte itemPosition)
        {
            if (!对象死亡 && 摆摊状态 <= 0 && 交易状态 < 3)
            {
                if (backpackType != 1)
                {
                    网络连接.尝试断开连接(new Exception("错误操作: 玩家使用物品.  错误: 背包类型错误."));
                    return;
                }
                if (!角色背包.TryGetValue(itemPosition, out var v))
                {
                    网络连接?.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1802
                    });
                    return;
                }
                if (当前等级 < v.NeedLevel)
                {
                    网络连接.尝试断开连接(new Exception("错误操作: 玩家使用物品.  错误: 等级无法使用."));
                    return;
                }
                if (v.NeedRace != GameObjectRace.通用 && 角色职业 != v.NeedRace)
                {
                    网络连接.尝试断开连接(new Exception("错误操作: 玩家使用物品.  错误: 性别无法使用."));
                    return;
                }
                if (v.NeedRace != GameObjectRace.通用 && 角色职业 != v.NeedRace)
                {
                    网络连接.尝试断开连接(new Exception("错误操作: 玩家使用物品.  错误: 职业无法使用."));
                    return;
                }
                if (冷却记录.TryGetValue(v.Id | 0x2000000, out var v2) && MainProcess.CurrentTime < v2)
                {
                    网络连接?.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1825
                    });
                    return;
                }
                if (v.GroupId > 0 && 冷却记录.TryGetValue(v.GroupId | 0, out var v3) && MainProcess.CurrentTime < v3)
                {
                    网络连接?.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1825
                    });
                    return;
                }

                switch (v.物品类型)
                {
                    case ItemType.技能书籍:
                        if (LearnSkill(v.SkillId))
                        {
                            ConsumeBackpackItem(1, v);
                        }
                        break;
                    case ItemType.可用药剂:
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        药品回血 = MainProcess.CurrentTime.AddSeconds(v.GetProp(ItemProperty.RecoveryTime, 1));
                        回血基数 = v.GetProp(ItemProperty.RecoveryBase, 15);
                        回血次数 = v.GetProp(ItemProperty.RecoverySteps, 6);
                        break;
                    case ItemType.可用杂物:
                        if (背包大小 - 角色背包.Count < 5)
                        {
                            网络连接?.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1793
                            });
                        }
                        else
                        {
                            var drugItem = v.物品模板;

                            if(v.PersistType == PersistentItemType.堆叠)
                            {
                                if (v.UnpackItemId == null || !GameItems.DataSheet.TryGetValue(v.UnpackItemId.Value, out drugItem))
                                    break;
                            }

                            if (v.GroupId > 0 && v.GroupCooling > 0)
                            {
                                冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                                网络连接?.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = (v.GroupId | 0),
                                    Cooldown = v.GroupCooling
                                });
                            }
                            if (v.Cooldown > 0)
                            {
                                冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                                网络连接?.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = (v.Id | 0x2000000),
                                    Cooldown = v.Cooldown
                                });
                            }

                            ConsumeBackpackItem(1, v);

                            byte b21 = 0;
                            byte b22 = 0;
                            while (b21 < 背包大小 && b22 < 6)
                            {
                                if (!角色背包.ContainsKey(b21))
                                {
                                    角色背包[b21] = new ItemData(drugItem, CharacterData, 1, b21, 1);
                                    网络连接?.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = 角色背包[b21].字节描述()
                                    });
                                    b22 = (byte)(b22 + 1);
                                }
                                b21 = (byte)(b21 + 1);
                            }
                        }
                        break;
                }

                switch (v.Name)
                {
                    case "豪杰灵石宝盒":
                        {
                            byte b41 = byte.MaxValue;
                            byte b42 = 0;
                            while (b42 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b42))
                                {
                                    b42 = (byte)(b42 + 1);
                                    continue;
                                }
                                b41 = b42;
                                break;
                            }
                            if (b41 == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            GameItems value24 = null;
                            switch (MainProcess.RandomNumber.Next(8))
                            {
                                case 0:
                                    GameItems.DataSheetByName.TryGetValue("驭朱灵石1级", out value24);
                                    break;
                                case 1:
                                    GameItems.DataSheetByName.TryGetValue("命朱灵石1级", out value24);
                                    break;
                                case 2:
                                    GameItems.DataSheetByName.TryGetValue("守阳灵石1级", out value24);
                                    break;
                                case 3:
                                    GameItems.DataSheetByName.TryGetValue("蔚蓝灵石1级", out value24);
                                    break;
                                case 4:
                                    GameItems.DataSheetByName.TryGetValue("精绿灵石1级", out value24);
                                    break;
                                case 5:
                                    GameItems.DataSheetByName.TryGetValue("纯紫灵石1级", out value24);
                                    break;
                                case 6:
                                    GameItems.DataSheetByName.TryGetValue("深灰灵石1级", out value24);
                                    break;
                                case 7:
                                    GameItems.DataSheetByName.TryGetValue("橙黄灵石1级", out value24);
                                    break;
                            }
                            if (value24 != null)
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b41] = new ItemData(value24, CharacterData, backpackType, b41, 2);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b41].字节描述()
                                });
                            }
                            break;
                        }
                    case "金创药(中量)":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        药品回血 = MainProcess.CurrentTime.AddSeconds(1.0);
                        回血基数 = 10;
                        回血次数 = 5;
                        break;
                    case "战具礼盒":
                        {
                            byte b11 = byte.MaxValue;
                            byte b12 = 0;
                            while (b12 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b12))
                                {
                                    b12 = (byte)(b12 + 1);
                                    continue;
                                }
                                b11 = b12;
                                break;
                            }
                            if (b11 == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            GameItems value6 = null;
                            if (角色职业 == GameObjectRace.战士)
                            {
                                GameItems.DataSheetByName.TryGetValue("气血石", out value6);
                            }
                            else if (角色职业 == GameObjectRace.法师)
                            {
                                GameItems.DataSheetByName.TryGetValue("魔法石", out value6);
                            }
                            else if (角色职业 == GameObjectRace.道士)
                            {
                                GameItems.DataSheetByName.TryGetValue("万灵符", out value6);
                            }
                            else if (角色职业 == GameObjectRace.刺客)
                            {
                                GameItems.DataSheetByName.TryGetValue("吸血令", out value6);
                            }
                            else if (角色职业 == GameObjectRace.弓手)
                            {
                                GameItems.DataSheetByName.TryGetValue("守护箭袋", out value6);
                            }
                            else if (角色职业 == GameObjectRace.龙枪)
                            {
                                GameItems.DataSheetByName.TryGetValue("血精石", out value6);
                            }
                            if (value6 != null && value6 is EquipmentItem 模板)
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b11] = new EquipmentData(模板, CharacterData, backpackType, b11);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b11].字节描述()
                                });
                            }
                            break;
                        }
                    case "万年雪霜":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        当前体力 += (int)Math.Max(75f * (1f + (float)this[GameObjectStats.药品回血] / 10000f), 0f);
                        当前魔力 += (int)Math.Max(100f * (1f + (float)this[GameObjectStats.药品回魔] / 10000f), 0f);
                        break;
                    case "魔龙城回城卷包":
                        if (背包大小 - 角色背包.Count < 5)
                        {
                            网络连接?.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1793
                            });
                        }
                        else
                        {
                            if (!GameItems.DataSheetByName.TryGetValue("魔龙城回城卷", out var value21))
                            {
                                break;
                            }
                            if (v.GroupId > 0 && v.GroupCooling > 0)
                            {
                                冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                                网络连接?.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = (v.GroupId | 0),
                                    Cooldown = v.GroupCooling
                                });
                            }
                            if (v.Cooldown > 0)
                            {
                                冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                                网络连接?.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = (v.Id | 0x2000000),
                                    Cooldown = v.Cooldown
                                });
                            }
                            ConsumeBackpackItem(1, v);
                            byte b35 = 0;
                            byte b36 = 0;
                            while (b35 < 背包大小 && b36 < 6)
                            {
                                if (!角色背包.ContainsKey(b35))
                                {
                                    角色背包[b35] = new ItemData(value21, CharacterData, 1, b35, 1);
                                    网络连接?.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = 角色背包[b35].字节描述()
                                    });
                                    b36 = (byte)(b36 + 1);
                                }
                                b35 = (byte)(b35 + 1);
                            }
                        }
                        break;
                    case "中平枪术":
                        if (LearnSkill(1201))
                        {
                            ConsumeBackpackItem(1, v);
                        }
                        break;
                    case "强效太阳水":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        当前体力 += (int)Math.Max(50f * (1f + (float)this[GameObjectStats.药品回血] / 10000f), 0f);
                        当前魔力 += (int)Math.Max(80f * (1f + (float)this[GameObjectStats.药品回魔] / 10000f), 0f);
                        break;
                    case "元宝袋(小)":
                        ConsumeBackpackItem(1, v);
                        元宝数量 += 100;
                        break;
                    case "盟重回城卷":
                        ConsumeBackpackItem(1, v);
                        玩家切换地图((当前地图.MapId == 147) ? 当前地图 : MapGatewayProcess.分配地图(147), AreaType.复活区域);
                        break;
                    case "祝福油":
                        {
                            if (!角色装备.TryGetValue(0, out var v5))
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1927
                                });
                                break;
                            }
                            if (v5.幸运等级.V >= 7)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1843
                                });
                                break;
                            }
                            ConsumeBackpackItem(1, v);
                            int num2 = 0;
                            num2 = v5.幸运等级.V switch
                            {
                                0 => 80,
                                1 => 10,
                                2 => 8,
                                3 => 6,
                                4 => 5,
                                5 => 4,
                                6 => 3,
                                _ => 80,
                            };
                            int num3 = MainProcess.RandomNumber.Next(100);
                            if (num3 < num2)
                            {
                                v5.幸运等级.V++;
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = v5.字节描述()
                                });
                                网络连接?.发送封包(new 武器幸运变化
                                {
                                    幸运变化 = 1
                                });
                                Stat加成[v5] = v5.装备Stat;
                                更新对象Stat();
                                if (v5.幸运等级.V >= 5)
                                {
                                    NetworkServiceGateway.发送公告($"[{对象名字}] 成功将 [{v5.Name}] 升到幸运 {v5.幸运等级.V} 级.");
                                }
                            }
                            else if (num3 >= 95 && v5.幸运等级.V > -9)
                            {
                                v5.幸运等级.V--;
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = v5.字节描述()
                                });
                                网络连接?.发送封包(new 武器幸运变化
                                {
                                    幸运变化 = -1
                                });
                                Stat加成[v5] = v5.装备Stat;
                                更新对象Stat();
                            }
                            else
                            {
                                网络连接?.发送封包(new 武器幸运变化
                                {
                                    幸运变化 = 0
                                });
                            }
                            break;
                        }
                    case "金创药(小量)":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        药品回血 = MainProcess.CurrentTime.AddSeconds(1.0);
                        回血基数 = 5;
                        回血次数 = 4;
                        break;
                    case "太阳水包":
                        if (背包大小 - 角色背包.Count < 5)
                        {
                            网络连接?.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1793
                            });
                        }
                        else
                        {
                            if (!GameItems.DataSheetByName.TryGetValue("太阳水", out var value22))
                            {
                                break;
                            }
                            if (v.GroupId > 0 && v.GroupCooling > 0)
                            {
                                冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                                网络连接?.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = (v.GroupId | 0),
                                    Cooldown = v.GroupCooling
                                });
                            }
                            if (v.Cooldown > 0)
                            {
                                冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                                网络连接?.发送封包(new AddedSkillCooldownPacket
                                {
                                    冷却编号 = (v.Id | 0x2000000),
                                    Cooldown = v.Cooldown
                                });
                            }
                            ConsumeBackpackItem(1, v);
                            byte b37 = 0;
                            byte b38 = 0;
                            while (b37 < 背包大小 && b38 < 6)
                            {
                                if (!角色背包.ContainsKey(b37))
                                {
                                    角色背包[b37] = new ItemData(value22, CharacterData, 1, b37, 1);
                                    网络连接?.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = 角色背包[b37].字节描述()
                                    });
                                    b38 = (byte)(b38 + 1);
                                }
                                b37 = (byte)(b37 + 1);
                            }
                        }
                        break;
                    case "比奇回城卷":
                        ConsumeBackpackItem(1, v);
                        玩家切换地图((当前地图.MapId == 143) ? 当前地图 : MapGatewayProcess.分配地图(143), AreaType.复活区域);
                        break;
                    case "太阳水":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        当前体力 += (int)Math.Max(30f * (1f + (float)this[GameObjectStats.药品回血] / 10000f), 0f);
                        当前魔力 += (int)Math.Max(40f * (1f + (float)this[GameObjectStats.药品回魔] / 10000f), 0f);
                        break;
                    case "铭文位切换神符":
                        {
                            if (!角色装备.TryGetValue(0, out var v4))
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1927
                                });
                                break;
                            }
                            if (!v4.双铭文栏.V)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1926
                                });
                                break;
                            }
                            if (v4.第一铭文 != null)
                            {
                                玩家装卸铭文(v4.第一铭文.SkillId, 0);
                            }
                            if (v4.第二铭文 != null)
                            {
                                玩家装卸铭文(v4.第二铭文.SkillId, 0);
                            }
                            v4.当前铭栏.V = (byte)((v4.当前铭栏.V == 0) ? 1u : 0u);
                            if (v4.第一铭文 != null)
                            {
                                玩家装卸铭文(v4.第一铭文.SkillId, v4.第一铭文.Id);
                            }
                            if (v4.第二铭文 != null)
                            {
                                玩家装卸铭文(v4.第二铭文.SkillId, v4.第二铭文.Id);
                            }
                            网络连接?.发送封包(new 玩家物品变动
                            {
                                物品描述 = v4.字节描述()
                            });
                            网络连接?.发送封包(new DoubleInscriptionPositionSwitchPacket
                            {
                                当前栏位 = v4.当前铭栏.V,
                                第一铭文 = (v4.第一铭文?.Index ?? 0),
                                第二铭文 = (v4.第二铭文?.Index ?? 0)
                            });
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                            ConsumeBackpackItem(1, v);
                            网络连接?.发送封包(new DoubleInscriptionPositionSwitchPacket
                            {
                                当前栏位 = v4.当前铭栏.V,
                                第一铭文 = (v4.第一铭文?.Index ?? 0),
                                第二铭文 = (v4.第二铭文?.Index ?? 0)
                            });
                            break;
                        }
                    case "强化战具礼盒":
                        {
                            byte b13 = byte.MaxValue;
                            byte b14 = 0;
                            while (b14 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b14))
                                {
                                    b14 = (byte)(b14 + 1);
                                    continue;
                                }
                                b13 = b14;
                                break;
                            }
                            if (b13 == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            GameItems value7 = null;
                            if (角色职业 == GameObjectRace.战士)
                            {
                                GameItems.DataSheetByName.TryGetValue("灵疗石", out value7);
                            }
                            else if (角色职业 == GameObjectRace.法师)
                            {
                                GameItems.DataSheetByName.TryGetValue("幻魔石", out value7);
                            }
                            else if (角色职业 == GameObjectRace.道士)
                            {
                                GameItems.DataSheetByName.TryGetValue("圣灵符", out value7);
                            }
                            else if (角色职业 == GameObjectRace.刺客)
                            {
                                GameItems.DataSheetByName.TryGetValue("狂血令", out value7);
                            }
                            else if (角色职业 == GameObjectRace.弓手)
                            {
                                GameItems.DataSheetByName.TryGetValue("射手箭袋", out value7);
                            }
                            else if (角色职业 == GameObjectRace.龙枪)
                            {
                                GameItems.DataSheetByName.TryGetValue("龙晶石", out value7);
                            }
                            if (value7 != null && value7 is EquipmentItem 模板2)
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b13] = new EquipmentData(模板2, CharacterData, backpackType, b13);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b13].字节描述()
                                });
                            }
                            break;
                        }
                    case "随机传送卷":
                        {
                            Point point2 = 当前地图.随机传送(当前坐标);
                            if (point2 != default(Point))
                            {
                                ConsumeBackpackItem(1, v);
                                玩家切换地图(当前地图, AreaType.未知区域, point2);
                            }
                            else
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 776
                                });
                            }
                            break;
                        }
                    case "疗伤药":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        当前体力 += (int)Math.Max(100f * (1f + (float)this[GameObjectStats.药品回血] / 10000f), 0f);
                        当前魔力 += (int)Math.Max(160f * (1f + (float)this[GameObjectStats.药品回魔] / 10000f), 0f);
                        break;
                    case "魔法药(小量)":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        药品回魔 = MainProcess.CurrentTime.AddSeconds(1.0);
                        回魔基数 = 10;
                        回魔次数 = 3;
                        break;
                    case "强效魔法药":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        药品回魔 = MainProcess.CurrentTime.AddSeconds(1.0);
                        回魔基数 = 25;
                        回魔次数 = 6;
                        break;
                    case "沙城每日宝箱":
                        {
                            byte b29 = byte.MaxValue;
                            byte b30 = 0;
                            while (b30 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b30))
                                {
                                    b30 = (byte)(b30 + 1);
                                    continue;
                                }
                                b29 = b30;
                                break;
                            }
                            if (b29 == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            int num = MainProcess.RandomNumber.Next(100);
                            GameItems value18;
                            if (num < 60)
                            {
                                ConsumeBackpackItem(1, v);
                                双倍经验 += 500000;
                            }
                            else if (num < 80)
                            {
                                ConsumeBackpackItem(1, v);
                                NumberGoldCoins += 100000;
                            }
                            else if (num < 90)
                            {
                                if (GameItems.DataSheetByName.TryGetValue("元宝袋(小)", out var value15))
                                {
                                    ConsumeBackpackItem(1, v);
                                    角色背包[b29] = new ItemData(value15, CharacterData, backpackType, b29, 5);
                                    网络连接?.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = 角色背包[b29].字节描述()
                                    });
                                }
                            }
                            else if (num < 95)
                            {
                                GameItems value16 = null;
                                if (角色职业 == GameObjectRace.战士)
                                {
                                    GameItems.DataSheetByName.TryGetValue("战士铭文石", out value16);
                                }
                                else if (角色职业 == GameObjectRace.法师)
                                {
                                    GameItems.DataSheetByName.TryGetValue("法师铭文石", out value16);
                                }
                                else if (角色职业 == GameObjectRace.道士)
                                {
                                    GameItems.DataSheetByName.TryGetValue("道士铭文石", out value16);
                                }
                                else if (角色职业 == GameObjectRace.刺客)
                                {
                                    GameItems.DataSheetByName.TryGetValue("刺客铭文石", out value16);
                                }
                                else if (角色职业 == GameObjectRace.弓手)
                                {
                                    GameItems.DataSheetByName.TryGetValue("弓手铭文石", out value16);
                                }
                                else if (角色职业 == GameObjectRace.龙枪)
                                {
                                    GameItems.DataSheetByName.TryGetValue("龙枪铭文石", out value16);
                                }
                                if (value16 != null)
                                {
                                    ConsumeBackpackItem(1, v);
                                    角色背包[b29] = new ItemData(value16, CharacterData, backpackType, b29, 3);
                                    网络连接?.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = 角色背包[b29].字节描述()
                                    });
                                }
                            }
                            else if (num < 98)
                            {
                                if (GameItems.DataSheetByName.TryGetValue("祝福油", out var value17))
                                {
                                    ConsumeBackpackItem(1, v);
                                    角色背包[b29] = new ItemData(value17, CharacterData, backpackType, b29, 2);
                                    网络连接?.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = 角色背包[b29].字节描述()
                                    });
                                }
                            }
                            else if (GameItems.DataSheetByName.TryGetValue("沙城奖杯", out value18))
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b29] = new ItemData(value18, CharacterData, backpackType, b29, 1);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b29].字节描述()
                                });
                            }
                            break;
                        }
                    case "盟重回城石":
                        ConsumeBackpackItem(1, v);
                        玩家切换地图((当前地图.MapId == 147) ? 当前地图 : MapGatewayProcess.分配地图(147), AreaType.复活区域);
                        break;
                    case "名俊铭文石礼包":
                        {
                            byte b17 = byte.MaxValue;
                            byte b18 = 0;
                            while (b18 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b18))
                                {
                                    b18 = (byte)(b18 + 1);
                                    continue;
                                }
                                b17 = b18;
                                break;
                            }
                            if (b17 == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            GameItems value9 = null;
                            if (角色职业 == GameObjectRace.战士)
                            {
                                GameItems.DataSheetByName.TryGetValue("战士铭文石", out value9);
                            }
                            else if (角色职业 == GameObjectRace.法师)
                            {
                                GameItems.DataSheetByName.TryGetValue("法师铭文石", out value9);
                            }
                            else if (角色职业 == GameObjectRace.道士)
                            {
                                GameItems.DataSheetByName.TryGetValue("道士铭文石", out value9);
                            }
                            else if (角色职业 == GameObjectRace.刺客)
                            {
                                GameItems.DataSheetByName.TryGetValue("刺客铭文石", out value9);
                            }
                            else if (角色职业 == GameObjectRace.弓手)
                            {
                                GameItems.DataSheetByName.TryGetValue("弓手铭文石", out value9);
                            }
                            else if (角色职业 == GameObjectRace.龙枪)
                            {
                                GameItems.DataSheetByName.TryGetValue("龙枪铭文石", out value9);
                            }
                            if (value9 != null)
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b17] = new ItemData(value9, CharacterData, backpackType, b17, 5);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b17].字节描述()
                                });
                            }
                            break;
                        }
                    case "名俊灵石宝盒":
                        {
                            byte b3 = byte.MaxValue;
                            byte b4 = 0;
                            while (b4 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b4))
                                {
                                    b4 = (byte)(b4 + 1);
                                    continue;
                                }
                                b3 = b4;
                                break;
                            }
                            if (b3 == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            GameItems value2 = null;
                            switch (MainProcess.RandomNumber.Next(8))
                            {
                                case 0:
                                    GameItems.DataSheetByName.TryGetValue("驭朱灵石1级", out value2);
                                    break;
                                case 1:
                                    GameItems.DataSheetByName.TryGetValue("命朱灵石1级", out value2);
                                    break;
                                case 2:
                                    GameItems.DataSheetByName.TryGetValue("守阳灵石1级", out value2);
                                    break;
                                case 3:
                                    GameItems.DataSheetByName.TryGetValue("蔚蓝灵石1级", out value2);
                                    break;
                                case 4:
                                    GameItems.DataSheetByName.TryGetValue("精绿灵石1级", out value2);
                                    break;
                                case 5:
                                    GameItems.DataSheetByName.TryGetValue("纯紫灵石1级", out value2);
                                    break;
                                case 6:
                                    GameItems.DataSheetByName.TryGetValue("深灰灵石1级", out value2);
                                    break;
                                case 7:
                                    GameItems.DataSheetByName.TryGetValue("橙黄灵石1级", out value2);
                                    break;
                            }
                            if (value2 != null)
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b3] = new ItemData(value2, CharacterData, backpackType, b3, 1);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b3].字节描述()
                                });
                            }
                            break;
                        }
                    case "比奇回城石":
                        ConsumeBackpackItem(1, v);
                        玩家切换地图((当前地图.MapId == 143) ? 当前地图 : MapGatewayProcess.分配地图(143), AreaType.复活区域);
                        break;
                    case "魔法药(中量)":
                        if (v.GroupId > 0 && v.GroupCooling > 0)
                        {
                            冷却记录[v.GroupId | 0] = MainProcess.CurrentTime.AddMilliseconds(v.GroupCooling);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.GroupId | 0),
                                Cooldown = v.GroupCooling
                            });
                        }
                        if (v.Cooldown > 0)
                        {
                            冷却记录[v.Id | 0x2000000] = MainProcess.CurrentTime.AddMilliseconds(v.Cooldown);
                            网络连接?.发送封包(new AddedSkillCooldownPacket
                            {
                                冷却编号 = (v.Id | 0x2000000),
                                Cooldown = v.Cooldown
                            });
                        }
                        ConsumeBackpackItem(1, v);
                        药品回魔 = MainProcess.CurrentTime.AddSeconds(1.0);
                        回魔基数 = 16;
                        回魔次数 = 5;
                        break;
                    case "元宝袋(大)":
                        ConsumeBackpackItem(1, v);
                        元宝数量 += 10000;
                        break;
                    case "元宝袋(超)":
                        ConsumeBackpackItem(1, v);
                        元宝数量 += 100000;
                        break;
                    case "随机传送石(大)":
                    case "随机传送石":
                        {
                            Point point = 当前地图.随机传送(当前坐标);
                            if (point != default(Point))
                            {
                                ConsumeBackpackItem(1, v);
                                玩家切换地图(当前地图, AreaType.未知区域, point);
                            }
                            else
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 776
                                });
                            }
                            break;
                        }
                    case "豪杰铭文石礼包":
                        {
                            byte b = byte.MaxValue;
                            byte b2 = 0;
                            while (b2 < 背包大小)
                            {
                                if (角色背包.ContainsKey(b2))
                                {
                                    b2 = (byte)(b2 + 1);
                                    continue;
                                }
                                b = b2;
                                break;
                            }
                            if (b == byte.MaxValue)
                            {
                                网络连接?.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                break;
                            }
                            GameItems value = null;
                            if (角色职业 == GameObjectRace.战士)
                            {
                                GameItems.DataSheetByName.TryGetValue("战士铭文石", out value);
                            }
                            else if (角色职业 == GameObjectRace.法师)
                            {
                                GameItems.DataSheetByName.TryGetValue("法师铭文石", out value);
                            }
                            else if (角色职业 == GameObjectRace.道士)
                            {
                                GameItems.DataSheetByName.TryGetValue("道士铭文石", out value);
                            }
                            else if (角色职业 == GameObjectRace.刺客)
                            {
                                GameItems.DataSheetByName.TryGetValue("刺客铭文石", out value);
                            }
                            else if (角色职业 == GameObjectRace.弓手)
                            {
                                GameItems.DataSheetByName.TryGetValue("弓手铭文石", out value);
                            }
                            else if (角色职业 == GameObjectRace.龙枪)
                            {
                                GameItems.DataSheetByName.TryGetValue("龙枪铭文石", out value);
                            }
                            if (value != null)
                            {
                                ConsumeBackpackItem(1, v);
                                角色背包[b] = new ItemData(value, CharacterData, backpackType, b, 10);
                                网络连接?.发送封包(new 玩家物品变动
                                {
                                    物品描述 = 角色背包[b].字节描述()
                                });
                            }
                            break;
                        }
                    case "元宝袋(中)":
                        ConsumeBackpackItem(1, v);
                        元宝数量 += 1000;
                        break;
                }
            }
            else
            {
                网络连接?.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1877
                });
            }
        }

        public void 玩家喝修复油(byte 背包类型, byte 物品位置)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                EquipmentData EquipmentData = null;
                EquipmentData EquipmentData2;
                if (背包类型 == 0 && this.角色装备.TryGetValue(物品位置, out EquipmentData2))
                {
                    EquipmentData = EquipmentData2;
                }
                ItemData ItemData;
                if (背包类型 == 1 && this.角色背包.TryGetValue(物品位置, out ItemData))
                {
                    EquipmentData EquipmentData3 = ItemData as EquipmentData;
                    if (EquipmentData3 != null)
                    {
                        EquipmentData = EquipmentData3;
                    }
                }
                ItemData 当前物品;
                if (EquipmentData == null)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1802
                    });
                    return;
                }
                else if (!EquipmentData.CanRepair)
                {
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1814
                    });
                    return;
                }
                else if (EquipmentData.最大持久.V >= EquipmentData.默认持久 * 2)
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1953
                    });
                    return;
                }
                else if (!this.查找背包物品(110012, out 当前物品))
                {
                    客户网络 网络连接4 = this.网络连接;
                    if (网络连接4 == null)
                    {
                        return;
                    }
                    网络连接4.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1802
                    });
                    return;
                }
                else
                {
                    this.ConsumeBackpackItem(1, 当前物品);
                    if (ComputingClass.计算概率(1f - (float)EquipmentData.最大持久.V * 0.5f / (float)EquipmentData.默认持久))
                    {
                        EquipmentData.最大持久.V += 1000;
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 != null)
                        {
                            网络连接5.发送封包(new FixMaxPersistentPacket
                            {
                                修复失败 = false
                            });
                        }
                        客户网络 网络连接6 = this.网络连接;
                        if (网络连接6 == null)
                        {
                            return;
                        }
                        网络连接6.发送封包(new 玩家物品变动
                        {
                            物品描述 = EquipmentData.字节描述()
                        });
                        return;
                    }
                    else
                    {
                        客户网络 网络连接7 = this.网络连接;
                        if (网络连接7 == null)
                        {
                            return;
                        }
                        网络连接7.发送封包(new FixMaxPersistentPacket
                        {
                            修复失败 = true
                        });
                        return;
                    }
                }
            }
            else
            {
                客户网络 网络连接8 = this.网络连接;
                if (网络连接8 == null)
                {
                    return;
                }
                网络连接8.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1877
                });
                return;
            }
        }


        public void 玩家合成物品()
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0)
            {
                byte 交易状态 = this.交易状态;
            }
        }


        public void 玩家出售物品(byte 背包类型, byte 物品位置, ushort 出售数量)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                GameStore 游戏商店;
                if (this.对话守卫 != null && this.当前地图 == this.对话守卫.当前地图 && base.网格距离(this.对话守卫) <= 12 && this.打开商店 != 0 && 出售数量 > 0 && GameStore.DataSheet.TryGetValue(this.打开商店, out 游戏商店))
                {
                    ItemData ItemData = null;
                    if (背包类型 == 1)
                    {
                        this.角色背包.TryGetValue(物品位置, out ItemData);
                    }
                    if (ItemData == null || ItemData.IsBound || ItemData.出售类型 == ItemsForSale.禁售)
                    {
                        return;
                    }
                    if (游戏商店.RecyclingType != ItemData.出售类型)
                    {
                        return;
                    }
                    this.角色背包.Remove(物品位置);
                    游戏商店.SellItem(ItemData);
                    this.NumberGoldCoins += ItemData.SalePrice;
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 删除玩家物品
                    {
                        背包类型 = 背包类型,
                        物品位置 = 物品位置
                    });
                }
                return;
            }
        }


        public void 玩家购买物品(int StoreId, int 物品位置, ushort 购入数量)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                GameStore 游戏商店;
                GameItems GameItems;
                if (this.对话守卫 != null && this.当前地图 == this.对话守卫.当前地图 && base.网格距离(this.对话守卫) <= 12 && this.打开商店 != 0 && 购入数量 > 0 && this.打开商店 == StoreId && GameStore.DataSheet.TryGetValue(this.打开商店, out 游戏商店) && 游戏商店.Products.Count > 物品位置 && GameItems.DataSheet.TryGetValue(游戏商店.Products[物品位置].Id, out GameItems))
                {
                    int num;
                    if (购入数量 != 1)
                    {
                        if (GameItems.PersistType == PersistentItemType.堆叠)
                        {
                            num = Math.Min((int)购入数量, GameItems.MaxDurability);
                            goto IL_DD;
                        }
                    }
                    num = 1;
                IL_DD:
                    int num2 = num;
                    GameStoreItem 游戏商品 = 游戏商店.Products[物品位置];
                    int num3 = -1;
                    byte b = 0;
                    while (b < this.背包大小)
                    {
                        ItemData ItemData;
                        if (this.角色背包.TryGetValue(b, out ItemData) && (GameItems.PersistType != PersistentItemType.堆叠 || GameItems.Id != ItemData.Id || ItemData.当前持久.V + (int)购入数量 > GameItems.MaxDurability))
                        {
                            b += 1;
                        }
                        else
                        {
                            num3 = (int)b;
                        IL_149:
                            if (num3 == -1)
                            {
                                客户网络 网络连接 = this.网络连接;
                                if (网络连接 == null)
                                {
                                    return;
                                }
                                网络连接.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1793
                                });
                                return;
                            }
                            else
                            {
                                int num4 = 游戏商品.Price * num2;
                                if (游戏商品.CurrencyType <= 19)
                                {
                                    GameCurrency GameCurrency;
                                    if (!Enum.TryParse<GameCurrency>(游戏商品.CurrencyType.ToString(), out GameCurrency) || !Enum.IsDefined(typeof(GameCurrency), GameCurrency))
                                    {
                                        return;
                                    }
                                    if (GameCurrency == GameCurrency.名师声望 || GameCurrency == GameCurrency.道义点数)
                                    {
                                        num4 *= 1000;
                                    }
                                    if (GameCurrency == GameCurrency.金币)
                                    {
                                        if (this.NumberGoldCoins < num4)
                                        {
                                            客户网络 网络连接2 = this.网络连接;
                                            if (网络连接2 == null)
                                            {
                                                return;
                                            }
                                            网络连接2.发送封包(new GameErrorMessagePacket
                                            {
                                                错误代码 = 13057
                                            });
                                            return;
                                        }
                                        else
                                        {
                                            this.NumberGoldCoins -= num4;
                                        }
                                    }
                                    else if (GameCurrency == GameCurrency.元宝)
                                    {
                                        if (this.元宝数量 < num4)
                                        {
                                            客户网络 网络连接3 = this.网络连接;
                                            if (网络连接3 == null)
                                            {
                                                return;
                                            }
                                            网络连接3.发送封包(new GameErrorMessagePacket
                                            {
                                                错误代码 = 13057
                                            });
                                            return;
                                        }
                                        else
                                        {
                                            this.元宝数量 -= num4;
                                        }
                                    }
                                    else if (GameCurrency == GameCurrency.名师声望)
                                    {
                                        if (this.师门声望 < num4)
                                        {
                                            客户网络 网络连接4 = this.网络连接;
                                            if (网络连接4 == null)
                                            {
                                                return;
                                            }
                                            网络连接4.发送封包(new GameErrorMessagePacket
                                            {
                                                错误代码 = 13057
                                            });
                                            return;
                                        }
                                        else
                                        {
                                            this.师门声望 -= num4;
                                        }
                                    }
                                    else
                                    {
                                        客户网络 网络连接5 = this.网络连接;
                                        if (网络连接5 == null)
                                        {
                                            return;
                                        }
                                        网络连接5.发送封包(new GameErrorMessagePacket
                                        {
                                            错误代码 = 13057
                                        });
                                        return;
                                    }
                                    客户网络 网络连接6 = this.网络连接;
                                    if (网络连接6 != null)
                                    {
                                        网络连接6.发送封包(new 货币数量变动
                                        {
                                            CurrencyType = (byte)游戏商品.CurrencyType,
                                            货币数量 = this.CharacterData.角色货币[GameCurrency]
                                        });
                                    }
                                }
                                else
                                {
                                    List<ItemData> 物品列表;
                                    if (!this.查找背包物品(num4, 游戏商品.CurrencyType, out 物品列表))
                                    {
                                        return;
                                    }
                                    this.消耗背包物品(num4, 物品列表);
                                }
                                ItemData ItemData2;
                                if (this.角色背包.TryGetValue((byte)num3, out ItemData2))
                                {
                                    ItemData2.当前持久.V += num2;
                                    客户网络 网络连接7 = this.网络连接;
                                    if (网络连接7 == null)
                                    {
                                        return;
                                    }
                                    网络连接7.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = ItemData2.字节描述()
                                    });
                                    return;
                                }
                                else
                                {
                                    EquipmentItem EquipmentItem = GameItems as EquipmentItem;
                                    if (EquipmentItem != null)
                                    {
                                        this.角色背包[(byte)num3] = new EquipmentData(EquipmentItem, this.CharacterData, 1, (byte)num3, false);
                                    }
                                    else
                                    {
                                        int 持久 = 0;
                                        switch (GameItems.PersistType)
                                        {
                                            case PersistentItemType.消耗:
                                            case PersistentItemType.纯度:
                                                持久 = GameItems.MaxDurability;
                                                break;
                                            case PersistentItemType.堆叠:
                                                持久 = num2;
                                                break;
                                            case PersistentItemType.容器:
                                                持久 = 0;
                                                break;
                                        }
                                        this.角色背包[(byte)num3] = new ItemData(GameItems, this.CharacterData, 1, (byte)num3, 持久);
                                    }
                                    客户网络 网络连接8 = this.网络连接;
                                    if (网络连接8 == null)
                                    {
                                        return;
                                    }
                                    网络连接8.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[(byte)num3].字节描述()
                                    });
                                    return;
                                }
                            }
                        }
                    }
                    //goto IL_149;
                }
                return;
            }
        }


        public void 玩家回购物品(byte 物品位置)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            GameStore 游戏商店;
            if (this.对话守卫 != null && this.当前地图 == this.对话守卫.当前地图 && base.网格距离(this.对话守卫) <= 12 && this.打开商店 != 0 && GameStore.DataSheet.TryGetValue(this.打开商店, out 游戏商店) && this.回购清单.Count > (int)物品位置)
            {
                ItemData ItemData = this.回购清单[(int)物品位置];
                int num = -1;
                byte b = 0;
                while (b < this.背包大小)
                {
                    ItemData ItemData2;
                    if (this.角色背包.TryGetValue(b, out ItemData2) && (!ItemData.能否堆叠 || ItemData.Id != ItemData2.Id || ItemData2.当前持久.V + ItemData.当前持久.V > ItemData2.最大持久.V))
                    {
                        b += 1;
                    }
                    else
                    {
                        num = (int)b;
                    IL_F9:
                        if (num == -1)
                        {
                            客户网络 网络连接 = this.网络连接;
                            if (网络连接 == null)
                            {
                                return;
                            }
                            网络连接.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1793
                            });
                            return;
                        }
                        else if (this.NumberGoldCoins < ItemData.SalePrice)
                        {
                            客户网络 网络连接2 = this.网络连接;
                            if (网络连接2 == null)
                            {
                                return;
                            }
                            网络连接2.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1821
                            });
                            return;
                        }
                        else if (游戏商店.BuyItem(ItemData))
                        {
                            this.NumberGoldCoins -= ItemData.SalePrice;
                            ItemData ItemData3;
                            if (this.角色背包.TryGetValue((byte)num, out ItemData3))
                            {
                                ItemData3.当前持久.V += ItemData.当前持久.V;
                                游戏商店.BuyItem(ItemData);
                                ItemData.Delete();
                                客户网络 网络连接3 = this.网络连接;
                                if (网络连接3 == null)
                                {
                                    return;
                                }
                                网络连接3.发送封包(new 玩家物品变动
                                {
                                    物品描述 = ItemData3.字节描述()
                                });
                                return;
                            }
                            else
                            {
                                this.角色背包[(byte)num] = ItemData;
                                ItemData.物品位置.V = (byte)num;
                                ItemData.物品容器.V = 1;
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 == null)
                                {
                                    return;
                                }
                                网络连接4.发送封包(new 玩家物品变动
                                {
                                    物品描述 = this.角色背包[(byte)num].字节描述()
                                });
                                return;
                            }
                        }
                        else
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 12807
                            });
                            return;
                        }
                    }
                }
                //goto IL_F9;
            }
        }


        public void 请求回购清单()
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                GameStore 游戏商店;
                if (this.对话守卫 != null && this.当前地图 == this.对话守卫.当前地图 && base.网格距离(this.对话守卫) <= 12 && this.打开商店 != 0 && GameStore.DataSheet.TryGetValue(this.打开商店, out 游戏商店))
                {
                    this.回购清单 = 游戏商店.AvailableItems.ToList<ItemData>();
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                        {
                            binaryWriter.Write((byte)this.回购清单.Count);
                            foreach (ItemData ItemData in this.回购清单)
                            {
                                binaryWriter.Write(ItemData.字节描述());
                            }
                            客户网络 网络连接 = this.网络连接;
                            if (网络连接 != null)
                            {
                                网络连接.发送封包(new SyncRepoListPacket
                                {
                                    字节描述 = memoryStream.ToArray()
                                });
                            }
                        }
                    }
                }
                return;
            }
        }


        public void 玩家镶嵌灵石(byte 装备类型, byte 装备位置, byte 装备孔位, byte 灵石类型, byte 灵石位置)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.打开界面 != "SoulEmbed")
            {
                this.网络连接.尝试断开连接(new Exception("Error: Player inlaid a spirit stone.  Error: Interface not opened"));
                return;
            }
            if (装备类型 == 1)
            {
                if (灵石类型 == 1)
                {
                    ItemData ItemData;
                    if (this.角色背包.TryGetValue(装备位置, out ItemData))
                    {
                        EquipmentData EquipmentData = ItemData as EquipmentData;
                        if (EquipmentData != null)
                        {
                            ItemData ItemData2;
                            if (!this.角色背包.TryGetValue(灵石位置, out ItemData2))
                            {
                                this.网络连接.尝试断开连接(new Exception("Error: The player set the spirit stone.  Error: No stone found"));
                                return;
                            }
                            if (EquipmentData.孔洞颜色.Count <= (int)装备孔位)
                            {
                                this.网络连接.尝试断开连接(new Exception("Error: Player inlaid a spirit stone.  Error: Wrong hole in equipment"));
                                return;
                            }
                            if (EquipmentData.镶嵌灵石.ContainsKey(装备孔位))
                            {
                                this.网络连接.尝试断开连接(new Exception("Error: The player has set the stone.  Error: There are already stones inlaid"));
                                return;
                            }
                            if ((EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.绿色 && ItemData2.Name.IndexOf("精绿灵石") == -1) || (EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.黄色 && ItemData2.Name.IndexOf("守阳灵石") == -1) || (EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.蓝色 && ItemData2.Name.IndexOf("蔚蓝灵石") == -1) || (EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.紫色 && ItemData2.Name.IndexOf("纯紫灵石") == -1) || (EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.灰色 && ItemData2.Name.IndexOf("深灰灵石") == -1) || (EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.橙色 && ItemData2.Name.IndexOf("橙黄灵石") == -1) || (EquipmentData.孔洞颜色[(int)装备孔位] == EquipHoleColor.红色 && ItemData2.Name.IndexOf("驭朱灵石") == -1 && ItemData2.Name.IndexOf("命朱灵石") == -1))
                            {
                                this.网络连接.尝试断开连接(new Exception("Wrong action: Player inlaid a spirit stone.  Error: Error specifying a stone"));
                                return;
                            }
                            this.ConsumeBackpackItem(1, ItemData2);
                            EquipmentData.镶嵌灵石[装备孔位] = ItemData2.物品模板;
                            客户网络 网络连接 = this.网络连接;
                            if (网络连接 != null)
                            {
                                网络连接.发送封包(new 玩家物品变动
                                {
                                    物品描述 = EquipmentData.字节描述()
                                });
                            }
                            客户网络 网络连接2 = this.网络连接;
                            if (网络连接2 == null)
                            {
                                return;
                            }
                            网络连接2.发送封包(new 成功镶嵌灵石
                            {
                                灵石状态 = 1
                            });
                            return;
                        }
                    }
                    this.网络连接.尝试断开连接(new Exception("MISTAKE: Player inlaid spirit stones.  Error: Equipment not found"));
                    return;
                }
            }
            this.网络连接.尝试断开连接(new Exception("Error: The player set the spirit stone.  Error: Not in character backpack"));
        }


        public void 玩家拆除灵石(byte 装备类型, byte 装备位置, byte 装备孔位)
        {
            int num = 0;
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.打开界面 != "SoulEmbed")
            {
                this.网络连接.尝试断开连接(new Exception("Error: Player inlaid a spirit stone.  Error: Interface not opened"));
                return;
            }
            if (装备类型 != 1)
            {
                this.网络连接.尝试断开连接(new Exception("Error: Player inlaid a spirit stone.  Error: Not in character backpack"));
                return;
            }
            if (this.背包剩余 > 0)
            {
                ItemData ItemData;
                if (this.角色背包.TryGetValue(装备位置, out ItemData))
                {
                    EquipmentData EquipmentData = ItemData as EquipmentData;
                    if (EquipmentData != null)
                    {
                        GameItems GameItems;
                        if (!EquipmentData.镶嵌灵石.TryGetValue(装备孔位, out GameItems))
                        {
                            this.网络连接.尝试断开连接(new Exception("Error: The player has set a spirit stone.  Error: No stones are inlaid"));
                            return;
                        }
                        if (GameItems.Name.IndexOf("1级") > 0)
                        {
                            int NumberGoldCoins = this.NumberGoldCoins;
                            int num2 = 100000;
                            num = 100000;
                            if (NumberGoldCoins < num2)
                            {
                                goto IL_199;
                            }
                        }
                        if (GameItems.Name.IndexOf("2级") > 0)
                        {
                            int NumberGoldCoins2 = this.NumberGoldCoins;
                            int num3 = 500000;
                            num = 500000;
                            if (NumberGoldCoins2 < num3)
                            {
                                goto IL_199;
                            }
                        }
                        if (GameItems.Name.IndexOf("3级") > 0)
                        {
                            int NumberGoldCoins3 = this.NumberGoldCoins;
                            int num4 = 2500000;
                            num = 2500000;
                            if (NumberGoldCoins3 < num4)
                            {
                                goto IL_199;
                            }
                        }
                        if (GameItems.Name.IndexOf("4级") > 0)
                        {
                            int NumberGoldCoins4 = this.NumberGoldCoins;
                            int num5 = 10000000;
                            num = 10000000;
                            if (NumberGoldCoins4 < num5)
                            {
                                goto IL_199;
                            }
                        }
                        if (GameItems.Name.IndexOf("5级") > 0)
                        {
                            int NumberGoldCoins5 = this.NumberGoldCoins;
                            int num6 = 25000000;
                            num = 25000000;
                            if (NumberGoldCoins5 < num6)
                            {
                                goto IL_199;
                            }
                        }
                        byte b = 0;
                        while (b < this.背包大小)
                        {
                            if (this.角色背包.ContainsKey(b))
                            {
                                b += 1;
                            }
                            else
                            {
                                this.NumberGoldCoins -= num;
                                EquipmentData.镶嵌灵石.Remove(装备孔位);
                                客户网络 网络连接 = this.网络连接;
                                if (网络连接 != null)
                                {
                                    网络连接.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = EquipmentData.字节描述()
                                    });
                                }
                                this.角色背包[b] = new ItemData(GameItems, this.CharacterData, 1, b, 1);
                                客户网络 网络连接2 = this.网络连接;
                                if (网络连接2 != null)
                                {
                                    网络连接2.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b].字节描述()
                                    });
                                }
                                客户网络 网络连接3 = this.网络连接;
                                if (网络连接3 == null)
                                {
                                    return;
                                }
                                网络连接3.发送封包(new 成功取下灵石
                                {
                                    灵石状态 = 1
                                });
                                return;
                            }
                        }
                        return;
                    IL_199:
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 1821
                        });
                        return;
                    }
                }
                this.网络连接.尝试断开连接(new Exception("MISTAKE: Player inlaid spirit stones.  Error: Equipment not found"));
                return;
            }
            客户网络 网络连接5 = this.网络连接;
            if (网络连接5 == null)
            {
                return;
            }
            网络连接5.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 1793
            });
        }


        public void OrdinaryInscriptionRefinementPacket(byte 装备类型, byte 装备位置, int Id)
        {
            EquipmentData EquipmentData = null;
            EquipmentData EquipmentData2;
            if (装备类型 == 0 && this.角色装备.TryGetValue(装备位置, out EquipmentData2))
            {
                EquipmentData = EquipmentData2;
            }
            ItemData ItemData;
            if (装备类型 == 1 && this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                EquipmentData EquipmentData3 = ItemData as EquipmentData;
                if (EquipmentData3 != null)
                {
                    EquipmentData = EquipmentData3;
                }
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: No interface opened"));
                return;
            }
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.NumberGoldCoins < 10000)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else if (EquipmentData == null)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                if (EquipmentData.物品类型 != ItemType.武器)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong item type."));
                    return;
                }
                if (Id <= 0)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong material number."));
                    return;
                }
                ItemData ItemData2;
                if (!this.查找背包物品(Id, out ItemData2))
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1835
                    });
                    return;
                }
                else
                {
                    if (ItemData2.物品类型 != ItemType.普通铭文)
                    {
                        this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong material type."));
                        return;
                    }
                    this.NumberGoldCoins -= 10000;
                    this.ConsumeBackpackItem(1, ItemData2);
                    byte 洗练职业 = 0;
                    switch (Id)
                    {
                        case 21001:
                            洗练职业 = 0;
                            break;
                        case 21002:
                            洗练职业 = 1;
                            break;
                        case 21003:
                            洗练职业 = 4;
                            break;
                        case 21004:
                            洗练职业 = 2;
                            break;
                        case 21005:
                            洗练职业 = 3;
                            break;
                        case 21006:
                            洗练职业 = 5;
                            break;
                    }
                    if (EquipmentData.第一铭文 == null)
                    {
                        EquipmentData.第一铭文 = InscriptionSkill.RandomWashing(洗练职业);
                        this.玩家装卸铭文(EquipmentData.第一铭文.SkillId, EquipmentData.第一铭文.Id);
                        if (EquipmentData.第一铭文.BroadcastNotification)
                        {
                            NetworkServiceGateway.发送公告(string.Concat(new string[]
                            {
                                "Congratulations to [",
                                this.对象名字,
                                "] For Obtain rare inscriptions in the inscription refining [",
                                EquipmentData.第一铭文.SkillName.Split(new char[]
                                {
                                    '-'
                                }).Last<string>(),
                                "]"
                            }), false);
                        }
                    }
                    else if (EquipmentData.传承材料 != 0 && (EquipmentData.双铭文点 += MainProcess.RandomNumber.Next(1, 6)) >= 1200 && EquipmentData.第二铭文 == null)
                    {
                        int SkillId;
                        int? num2;
                        do
                        {
                            SkillId = (int)(EquipmentData.第二铭文 = InscriptionSkill.RandomWashing(洗练职业)).SkillId;
                            InscriptionSkill 第一铭文 = EquipmentData.第一铭文;
                            ushort? num = (第一铭文 == null) ? null : new ushort?(第一铭文.SkillId);
                            num2 = ((num != null) ? new int?((int)num.GetValueOrDefault()) : null);
                        }
                        while (SkillId == num2.GetValueOrDefault() & num2 != null);
                        this.玩家装卸铭文(EquipmentData.第二铭文.SkillId, EquipmentData.第二铭文.Id);
                        if (EquipmentData.第二铭文.BroadcastNotification)
                        {
                            NetworkServiceGateway.发送公告(string.Concat(new string[]
                            {
                                "Congratulations to [",
                                this.对象名字,
                                "] For Obtain rare inscriptions in the inscription wash [",
                                EquipmentData.第二铭文.SkillName.Split(new char[]
                                {
                                    '-'
                                }).Last<string>(),
                                "]"
                            }), false);
                        }
                    }
                    else
                    {
                        if (装备类型 == 0)
                        {
                            this.玩家装卸铭文(EquipmentData.第一铭文.SkillId, 0);
                        }
                        int? num2;
                        int SkillId2;
                        do
                        {
                            SkillId2 = (int)(EquipmentData.第一铭文 = InscriptionSkill.RandomWashing(洗练职业)).SkillId;
                            InscriptionSkill 第二铭文 = EquipmentData.第二铭文;
                            ushort? num = (第二铭文 == null) ? null : new ushort?(第二铭文.SkillId);
                            num2 = ((num != null) ? new int?((int)num.GetValueOrDefault()) : null);
                        }
                        while (SkillId2 == num2.GetValueOrDefault() & num2 != null);
                        if (装备类型 == 0)
                        {
                            this.玩家装卸铭文(EquipmentData.第一铭文.SkillId, EquipmentData.第一铭文.Id);
                        }
                        if (EquipmentData.第一铭文.BroadcastNotification)
                        {
                            NetworkServiceGateway.发送公告(string.Concat(new string[]
                            {
                                "Congratulations to [",
                                this.对象名字,
                                "]For Obtain rare inscriptions in the Inscription Wash[",
                                EquipmentData.第一铭文.SkillName.Split(new char[]
                                {
                                    '-'
                                }).Last<string>(),
                                "]"
                            }), false);
                        }
                    }
                    客户网络 网络连接4 = this.网络连接;
                    if (网络连接4 != null)
                    {
                        网络连接4.发送封包(new 玩家物品变动
                        {
                            物品描述 = EquipmentData.字节描述()
                        });
                    }
                    客户网络 网络连接5 = this.网络连接;
                    if (网络连接5 == null)
                    {
                        return;
                    }
                    玩家普通洗练 玩家普通洗练 = new 玩家普通洗练();
                    InscriptionSkill 第一铭文2 = EquipmentData.第一铭文;
                    玩家普通洗练.铭文位一 = ((ushort)((第一铭文2 != null) ? 第一铭文2.Index : 0));
                    InscriptionSkill 第二铭文2 = EquipmentData.第二铭文;
                    玩家普通洗练.铭文位二 = ((ushort)((第二铭文2 != null) ? 第二铭文2.Index : 0));
                    网络连接5.发送封包(玩家普通洗练);
                    return;
                }
            }
        }


        public void 高级铭文洗练(byte 装备类型, byte 装备位置, int Id)
        {
            EquipmentData EquipmentData = null;
            EquipmentData EquipmentData2;
            if (装备类型 == 0 && this.角色装备.TryGetValue(装备位置, out EquipmentData2))
            {
                EquipmentData = EquipmentData2;
            }
            ItemData ItemData;
            if (装备类型 == 1 && this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                EquipmentData EquipmentData3 = ItemData as EquipmentData;
                if (EquipmentData3 != null)
                {
                    EquipmentData = EquipmentData3;
                }
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: No interface opened"));
                return;
            }
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.NumberGoldCoins < 100000)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else if (EquipmentData == null)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                if (EquipmentData.物品类型 != ItemType.武器)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong item type."));
                    return;
                }
                if (EquipmentData.第二铭文 == null)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Second inscription is empty."));
                    return;
                }
                if (Id <= 0)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong material number."));
                    return;
                }
                ItemData ItemData2;
                if (!this.查找背包物品(Id, out ItemData2))
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1835
                    });
                    return;
                }
                else
                {
                    if (ItemData2.物品类型 != ItemType.普通铭文)
                    {
                        this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong material type."));
                        return;
                    }
                    this.NumberGoldCoins -= 100000;
                    this.ConsumeBackpackItem(1, ItemData2);
                    byte 洗练职业 = 0;
                    switch (Id)
                    {
                        case 21001:
                            洗练职业 = 0;
                            break;
                        case 21002:
                            洗练职业 = 1;
                            break;
                        case 21003:
                            洗练职业 = 4;
                            break;
                        case 21004:
                            洗练职业 = 2;
                            break;
                        case 21005:
                            洗练职业 = 3;
                            break;
                        case 21006:
                            洗练职业 = 5;
                            break;
                    }
                    while ((this.InscriptionSkill = InscriptionSkill.RandomWashing(洗练职业)).SkillId == EquipmentData.最优铭文.SkillId)
                    {
                    }
                    if (EquipmentData.最优铭文 == EquipmentData.第一铭文)
                    {
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 != null)
                        {
                            网络连接4.发送封包(new 玩家高级洗练
                            {
                                洗练结果 = 1,
                                铭文位一 = EquipmentData.最优铭文.Index,
                                铭文位二 = this.InscriptionSkill.Index
                            });
                        }
                    }
                    else
                    {
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 != null)
                        {
                            网络连接5.发送封包(new 玩家高级洗练
                            {
                                洗练结果 = 1,
                                铭文位一 = this.InscriptionSkill.Index,
                                铭文位二 = EquipmentData.最优铭文.Index
                            });
                        }
                    }
                    if (this.InscriptionSkill.BroadcastNotification)
                    {
                        NetworkServiceGateway.发送公告(string.Concat(new string[]
                        {
                            "Congratulations to [",
                            this.对象名字,
                            "] For Obtain rare inscriptions in the Inscription Wash[",
                            this.InscriptionSkill.SkillName.Split(new char[]
                            {
                                '-'
                            }).Last<string>(),
                            "]"
                        }), false);
                    }
                    return;
                }
            }
        }


        public void ReplaceInscriptionRefinementPacket(byte 装备类型, byte 装备位置, int Id)
        {
            EquipmentData EquipmentData = null;
            int num = 10;
            EquipmentData EquipmentData2;
            if (装备类型 == 0 && this.角色装备.TryGetValue(装备位置, out EquipmentData2))
            {
                EquipmentData = EquipmentData2;
            }
            ItemData ItemData;
            if (装备类型 == 1 && this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                EquipmentData EquipmentData3 = ItemData as EquipmentData;
                if (EquipmentData3 != null)
                {
                    EquipmentData = EquipmentData3;
                }
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: No interface opened"));
                return;
            }
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.NumberGoldCoins < 1000000)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else if (EquipmentData == null)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                if (EquipmentData.物品类型 != ItemType.武器)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong item type."));
                    return;
                }
                if (EquipmentData.第二铭文 == null)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: OrdinaryInscriptionRefinementPacket. Wrong: Second inscription is empty."));
                    return;
                }
                if (Id <= 0)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong material number."));
                    return;
                }
                List<ItemData> list;
                if (!this.查找背包物品(num, Id, out list))
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1835
                    });
                    return;
                }
                else
                {
                    if (list.FirstOrDefault((ItemData O) => O.物品类型 != ItemType.普通铭文) != null)
                    {
                        this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong material type."));
                        return;
                    }
                    this.NumberGoldCoins -= 1000000;
                    this.消耗背包物品(num, list);
                    byte 洗练职业 = 0;
                    switch (Id)
                    {
                        case 21001:
                            洗练职业 = 0;
                            break;
                        case 21002:
                            洗练职业 = 1;
                            break;
                        case 21003:
                            洗练职业 = 4;
                            break;
                        case 21004:
                            洗练职业 = 2;
                            break;
                        case 21005:
                            洗练职业 = 3;
                            break;
                        case 21006:
                            洗练职业 = 5;
                            break;
                    }
                    while ((this.InscriptionSkill = InscriptionSkill.RandomWashing(洗练职业)).SkillId == EquipmentData.最差铭文.SkillId)
                    {
                    }
                    客户网络 网络连接4 = this.网络连接;
                    if (网络连接4 != null)
                    {
                        网络连接4.发送封包(new 玩家高级洗练
                        {
                            洗练结果 = 1,
                            铭文位一 = EquipmentData.最差铭文.Index,
                            铭文位二 = this.InscriptionSkill.Index
                        });
                    }
                    if (this.InscriptionSkill.BroadcastNotification)
                    {
                        NetworkServiceGateway.发送公告(string.Concat(new string[]
                        {
                            "Congratulations to [",
                            this.对象名字,
                            "] For Obtain rare inscriptions in the Inscription Wash[",
                            this.InscriptionSkill.SkillName.Split(new char[]
                            {
                                '-'
                            }).Last<string>(),
                            "]"
                        }), false);
                    }
                    return;
                }
            }
        }


        public void 高级洗练确认(byte 装备类型, byte 装备位置)
        {
            EquipmentData EquipmentData = null;
            EquipmentData EquipmentData2;
            if (装备类型 == 0 && this.角色装备.TryGetValue(装备位置, out EquipmentData2))
            {
                EquipmentData = EquipmentData2;
            }
            ItemData ItemData;
            if (装备类型 == 1 && this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                EquipmentData EquipmentData3 = ItemData as EquipmentData;
                if (EquipmentData3 != null)
                {
                    EquipmentData = EquipmentData3;
                }
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: No interface opened"));
                return;
            }
            if (EquipmentData == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                if (this.InscriptionSkill == null)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Confirmation of replacement inscription.  Error: There is no no record of the inscription.."));
                    return;
                }
                if (EquipmentData.物品类型 != ItemType.武器)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong item type."));
                    return;
                }
                if (EquipmentData.第二铭文 == null)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: OrdinaryInscriptionRefinementPacket. Wrong: Second inscription is empty."));
                    return;
                }
                if (装备类型 == 0)
                {
                    this.玩家装卸铭文(EquipmentData.最差铭文.SkillId, 0);
                }
                EquipmentData.最差铭文 = this.InscriptionSkill;
                if (装备类型 == 0)
                {
                    this.玩家装卸铭文(this.InscriptionSkill.SkillId, this.InscriptionSkill.Id);
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 != null)
                {
                    网络连接2.发送封包(new 玩家物品变动
                    {
                        物品描述 = EquipmentData.字节描述()
                    });
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 确认替换铭文
                {
                    确定替换 = true
                });
                return;
            }
        }


        public void 替换洗练确认(byte 装备类型, byte 装备位置)
        {
            EquipmentData EquipmentData = null;
            EquipmentData EquipmentData2;
            if (装备类型 == 0 && this.角色装备.TryGetValue(装备位置, out EquipmentData2))
            {
                EquipmentData = EquipmentData2;
            }
            ItemData ItemData;
            if (装备类型 == 1 && this.角色背包.TryGetValue(装备位置, out ItemData))
            {
                EquipmentData EquipmentData3 = ItemData as EquipmentData;
                if (EquipmentData3 != null)
                {
                    EquipmentData = EquipmentData3;
                }
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: No interface opened"));
                return;
            }
            if (EquipmentData == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1802
                });
                return;
            }
            else
            {
                if (this.InscriptionSkill == null)
                {
                    this.网络连接.尝试断开连接(new Exception("Mistake: Confirmation of replacement inscription.  Error: There is no record of the inscription."));
                    return;
                }
                if (EquipmentData.物品类型 != ItemType.武器)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Operation: OrdinaryInscriptionRefinementPacket. Error: Wrong item type."));
                    return;
                }
                if (EquipmentData.第二铭文 == null)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: OrdinaryInscriptionRefinementPacket. Wrong: Second inscription is empty."));
                    return;
                }
                if (装备类型 == 0)
                {
                    this.玩家装卸铭文(EquipmentData.最优铭文.SkillId, 0);
                }
                EquipmentData.最优铭文 = this.InscriptionSkill;
                if (装备类型 == 0)
                {
                    this.玩家装卸铭文(this.InscriptionSkill.SkillId, this.InscriptionSkill.Id);
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 != null)
                {
                    网络连接2.发送封包(new 玩家物品变动
                    {
                        物品描述 = EquipmentData.字节描述()
                    });
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 确认替换铭文
                {
                    确定替换 = true
                });
                return;
            }
        }


        public void 放弃替换铭文()
        {
            this.InscriptionSkill = null;
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new 确认替换铭文
            {
                确定替换 = false
            });
        }


        public void UnlockDoubleInscriptionSlotPacket(byte 装备类型, byte 装备位置, byte 操作参数)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Error: UnlockDoubleInscriptionSlotPacket. Error: No interface opened"));
                return;
            }
            if (装备类型 == 1)
            {
                ItemData ItemData;
                if (this.角色背包.TryGetValue(装备位置, out ItemData))
                {
                    EquipmentData EquipmentData = ItemData as EquipmentData;
                    if (EquipmentData != null)
                    {
                        if (EquipmentData.物品类型 != ItemType.武器)
                        {
                            this.网络连接.尝试断开连接(new Exception("Error Action: UnlockDoubleInscriptionSlotPacket. Error: Wrong item type."));
                            return;
                        }
                        if (操作参数 == 1)
                        {
                            int num = 2000000;
                            if (EquipmentData.双铭文栏.V)
                            {
                                客户网络 网络连接 = this.网络连接;
                                if (网络连接 == null)
                                {
                                    return;
                                }
                                网络连接.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1909
                                });
                                return;
                            }
                            else if (this.NumberGoldCoins < num)
                            {
                                客户网络 网络连接2 = this.网络连接;
                                if (网络连接2 == null)
                                {
                                    return;
                                }
                                网络连接2.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1821
                                });
                                return;
                            }
                            else
                            {
                                this.NumberGoldCoins -= num;
                                EquipmentData.双铭文栏.V = true;
                                客户网络 网络连接3 = this.网络连接;
                                if (网络连接3 != null)
                                {
                                    网络连接3.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = EquipmentData.字节描述()
                                    });
                                }
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 == null)
                                {
                                    return;
                                }
                                DoubleInscriptionPositionSwitchPacket DoubleInscriptionPositionSwitchPacket = new DoubleInscriptionPositionSwitchPacket();
                                DoubleInscriptionPositionSwitchPacket.当前栏位 = (ushort)EquipmentData.当前铭栏.V;
                                InscriptionSkill 第一铭文 = EquipmentData.第一铭文;
                                DoubleInscriptionPositionSwitchPacket.第一铭文 = ((ushort)((第一铭文 != null) ? 第一铭文.Index : 0));
                                InscriptionSkill 第二铭文 = EquipmentData.第二铭文;
                                DoubleInscriptionPositionSwitchPacket.第二铭文 = ((ushort)((第二铭文 != null) ? 第二铭文.Index : 0));
                                网络连接4.发送封包(DoubleInscriptionPositionSwitchPacket);
                            }
                        }
                        return;
                    }
                }
                this.网络连接.尝试断开连接(new Exception("Error Operation: UnlockDoubleInscriptionSlotPacket. Error: Not an equipment type."));
                return;
            }
            客户网络 网络连接5 = this.网络连接;
            if (网络连接5 == null)
            {
                return;
            }
            网络连接5.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 1839
            });
        }


        public void ToggleDoubleInscriptionBitPacket(byte 装备类型, byte 装备位置, byte 操作参数)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: ToggleDoubleInscriptionBitPacket. Error: No interface opened"));
                return;
            }
            if (装备类型 == 1)
            {
                ItemData ItemData;
                if (this.角色背包.TryGetValue(装备位置, out ItemData))
                {
                    EquipmentData EquipmentData = ItemData as EquipmentData;
                    if (EquipmentData != null)
                    {
                        if (EquipmentData.物品类型 != ItemType.武器)
                        {
                            this.网络连接.尝试断开连接(new Exception("Error Action: ToggleDoubleInscriptionBitPacket. Error: Wrong item type."));
                            return;
                        }
                        if (!EquipmentData.双铭文栏.V)
                        {
                            客户网络 网络连接 = this.网络连接;
                            if (网络连接 == null)
                            {
                                return;
                            }
                            网络连接.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1926
                            });
                            return;
                        }
                        else
                        {
                            if (操作参数 == EquipmentData.当前铭栏.V)
                            {
                                this.网络连接.尝试断开连接(new Exception("Error Action: ToggleDoubleInscriptionBitPacket. Error: Toggle inscription error."));
                                return;
                            }
                            EquipmentData.当前铭栏.V = 操作参数;
                            客户网络 网络连接2 = this.网络连接;
                            if (网络连接2 != null)
                            {
                                网络连接2.发送封包(new 玩家物品变动
                                {
                                    物品描述 = EquipmentData.字节描述()
                                });
                            }
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            DoubleInscriptionPositionSwitchPacket DoubleInscriptionPositionSwitchPacket = new DoubleInscriptionPositionSwitchPacket();
                            DoubleInscriptionPositionSwitchPacket.当前栏位 = (ushort)EquipmentData.当前铭栏.V;
                            InscriptionSkill 第一铭文 = EquipmentData.第一铭文;
                            DoubleInscriptionPositionSwitchPacket.第一铭文 = ((ushort)((第一铭文 != null) ? 第一铭文.Index : 0));
                            InscriptionSkill 第二铭文 = EquipmentData.第二铭文;
                            DoubleInscriptionPositionSwitchPacket.第二铭文 = ((ushort)((第二铭文 != null) ? 第二铭文.Index : 0));
                            网络连接3.发送封包(DoubleInscriptionPositionSwitchPacket);
                            return;
                        }
                    }
                }
                this.网络连接.尝试断开连接(new Exception("Error Action: ToggleDoubleInscriptionBitPacket. Error: Not an equipment type."));
                return;
            }
            客户网络 网络连接4 = this.网络连接;
            if (网络连接4 == null)
            {
                return;
            }
            网络连接4.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 1839
            });
        }


        public void 传承武器铭文(byte 来源类型, byte 来源位置, byte 目标类型, byte 目标位置)
        {
            int num = 1000000;
            int num2 = 150;
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.打开界面 != "WeaponRune")
            {
                this.网络连接.尝试断开连接(new Exception("Bug: Inherited weapon inscription.  Error: Interface not opened"));
                return;
            }
            if (来源类型 == 1)
            {
                if (目标类型 == 1)
                {
                    ItemData ItemData;
                    if (this.角色背包.TryGetValue(来源位置, out ItemData))
                    {
                        EquipmentData EquipmentData = ItemData as EquipmentData;
                        ItemData ItemData2;
                        if (EquipmentData != null && this.角色背包.TryGetValue(目标位置, out ItemData2))
                        {
                            EquipmentData EquipmentData2 = ItemData2 as EquipmentData;
                            if (EquipmentData2 != null)
                            {
                                if (EquipmentData.物品类型 == ItemType.武器)
                                {
                                    if (EquipmentData2.物品类型 == ItemType.武器)
                                    {
                                        if (EquipmentData.传承材料 != 0 && EquipmentData2.传承材料 != 0)
                                        {
                                            if (EquipmentData.传承材料 == EquipmentData2.传承材料)
                                            {
                                                if (EquipmentData.第二铭文 != null && EquipmentData2.第二铭文 != null)
                                                {
                                                    List<ItemData> 物品列表;
                                                    if (this.NumberGoldCoins < num)
                                                    {
                                                        客户网络 网络连接 = this.网络连接;
                                                        if (网络连接 == null)
                                                        {
                                                            return;
                                                        }
                                                        网络连接.发送封包(new GameErrorMessagePacket
                                                        {
                                                            错误代码 = 1821
                                                        });
                                                        return;
                                                    }
                                                    else if (!this.查找背包物品(num2, EquipmentData.传承材料, out 物品列表))
                                                    {
                                                        客户网络 网络连接2 = this.网络连接;
                                                        if (网络连接2 == null)
                                                        {
                                                            return;
                                                        }
                                                        网络连接2.发送封包(new GameErrorMessagePacket
                                                        {
                                                            错误代码 = 1835
                                                        });
                                                        return;
                                                    }
                                                    else
                                                    {
                                                        this.NumberGoldCoins -= num;
                                                        this.消耗背包物品(num2, 物品列表);
                                                        EquipmentData2.第一铭文 = EquipmentData.第一铭文;
                                                        EquipmentData2.第二铭文 = EquipmentData.第二铭文;
                                                        EquipmentData.铭文技能.Remove((byte)(EquipmentData.当前铭栏.V * 2));
                                                        EquipmentData.铭文技能.Remove((byte)(EquipmentData.当前铭栏.V * 2 + 1));
                                                        客户网络 网络连接3 = this.网络连接;
                                                        if (网络连接3 != null)
                                                        {
                                                            网络连接3.发送封包(new 玩家物品变动
                                                            {
                                                                物品描述 = EquipmentData.字节描述()
                                                            });
                                                        }
                                                        客户网络 网络连接4 = this.网络连接;
                                                        if (网络连接4 != null)
                                                        {
                                                            网络连接4.发送封包(new 玩家物品变动
                                                            {
                                                                物品描述 = EquipmentData2.字节描述()
                                                            });
                                                        }
                                                        客户网络 网络连接5 = this.网络连接;
                                                        if (网络连接5 == null)
                                                        {
                                                            return;
                                                        }
                                                        网络连接5.发送封包(new 铭文传承应答());
                                                        return;
                                                    }
                                                }
                                                else
                                                {
                                                    客户网络 网络连接6 = this.网络连接;
                                                    if (网络连接6 == null)
                                                    {
                                                        return;
                                                    }
                                                    网络连接6.发送封包(new GameErrorMessagePacket
                                                    {
                                                        错误代码 = 1887
                                                    });
                                                    return;
                                                }
                                            }
                                        }
                                        客户网络 网络连接7 = this.网络连接;
                                        if (网络连接7 == null)
                                        {
                                            return;
                                        }
                                        网络连接7.发送封包(new GameErrorMessagePacket
                                        {
                                            错误代码 = 1887
                                        });
                                        return;
                                    }
                                }
                                this.网络连接.尝试断开连接(new Exception("Mishap: Inherited weapon inscription.  Error: Wrong item type."));
                                return;
                            }
                        }
                    }
                    this.网络连接.尝试断开连接(new Exception("Mistake: Inherited weapon inscription.  Error: Not an equipment type."));
                    return;
                }
            }
            客户网络 网络连接8 = this.网络连接;
            if (网络连接8 == null)
            {
                return;
            }
            网络连接8.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 1839
            });
        }


        public void 升级武器普通(byte[] 首饰组, byte[] 材料组)
        {
            if (this.对象死亡 || this.摆摊状态 > 0 || this.交易状态 >= 3)
            {
                return;
            }
            EquipmentData EquipmentData;
            if (this.CharacterData.升级装备.V != null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1854
                });
                return;
            }
            else if (this.NumberGoldCoins < 10000)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1821
                });
                return;
            }
            else if (!this.角色装备.TryGetValue(0, out EquipmentData))
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1853
                });
                return;
            }
            else if (EquipmentData.最大持久.V > 3000 && (float)EquipmentData.最大持久.V > (float)EquipmentData.默认持久 * 0.5f)
            {
                if (EquipmentData.升级次数.V >= 9)
                {
                    客户网络 网络连接4 = this.网络连接;
                    if (网络连接4 == null)
                    {
                        return;
                    }
                    网络连接4.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 1815
                    });
                    return;
                }
                else
                {
                    Dictionary<byte, EquipmentData> dictionary = new Dictionary<byte, EquipmentData>();
                    foreach (byte b in 首饰组)
                    {
                        if (b != 255)
                        {
                            ItemData ItemData;
                            if (this.角色背包.TryGetValue(b, out ItemData))
                            {
                                EquipmentData EquipmentData2 = ItemData as EquipmentData;
                                if (EquipmentData2 != null)
                                {
                                    if (EquipmentData2.物品类型 == ItemType.项链 || EquipmentData2.物品类型 == ItemType.手镯 || EquipmentData2.物品类型 == ItemType.戒指)
                                    {
                                        if (!dictionary.ContainsKey(b))
                                        {
                                            dictionary.Add(b, EquipmentData2);
                                            goto IL_19A;
                                        }
                                        客户网络 网络连接5 = this.网络连接;
                                        if (网络连接5 == null)
                                        {
                                            return;
                                        }
                                        网络连接5.发送封包(new GameErrorMessagePacket
                                        {
                                            错误代码 = 1859
                                        });
                                        return;
                                    }
                                }
                                客户网络 网络连接6 = this.网络连接;
                                if (网络连接6 == null)
                                {
                                    return;
                                }
                                网络连接6.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1859
                                });
                                return;
                            }
                            else
                            {
                                客户网络 网络连接7 = this.网络连接;
                                if (网络连接7 == null)
                                {
                                    return;
                                }
                                网络连接7.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1859
                                });
                                return;
                            }
                        }
                    IL_19A:;
                    }
                    Dictionary<byte, ItemData> dictionary2 = new Dictionary<byte, ItemData>();
                    foreach (byte b2 in 材料组)
                    {
                        if (b2 != 255)
                        {
                            ItemData ItemData2;
                            if (this.角色背包.TryGetValue(b2, out ItemData2))
                            {
                                if (ItemData2.物品类型 != ItemType.武器锻造)
                                {
                                    客户网络 网络连接8 = this.网络连接;
                                    if (网络连接8 == null)
                                    {
                                        return;
                                    }
                                    网络连接8.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 1859
                                    });
                                    return;
                                }
                                else if (!dictionary2.ContainsKey(b2))
                                {
                                    dictionary2.Add(b2, ItemData2);
                                }
                                else
                                {
                                    客户网络 网络连接9 = this.网络连接;
                                    if (网络连接9 == null)
                                    {
                                        return;
                                    }
                                    网络连接9.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 1859
                                    });
                                    return;
                                }
                            }
                            else
                            {
                                客户网络 网络连接10 = this.网络连接;
                                if (网络连接10 == null)
                                {
                                    return;
                                }
                                网络连接10.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 1859
                                });
                                return;
                            }
                        }
                    }
                    this.NumberGoldCoins -= 10000;
                    foreach (byte b3 in 首饰组)
                    {
                        if (b3 != 255)
                        {
                            this.角色背包[b3].Delete();
                            this.角色背包.Remove(b3);
                            客户网络 网络连接11 = this.网络连接;
                            if (网络连接11 != null)
                            {
                                网络连接11.发送封包(new 删除玩家物品
                                {
                                    背包类型 = 1,
                                    物品位置 = b3
                                });
                            }
                        }
                    }
                    foreach (byte b4 in 材料组)
                    {
                        if (b4 != 255)
                        {
                            this.角色背包[b4].Delete();
                            this.角色背包.Remove(b4);
                            客户网络 网络连接12 = this.网络连接;
                            if (网络连接12 != null)
                            {
                                网络连接12.发送封包(new 删除玩家物品
                                {
                                    背包类型 = 1,
                                    物品位置 = b4
                                });
                            }
                        }
                    }
                    this.角色装备.Remove(0);
                    this.玩家穿卸装备(EquipmentWearingParts.武器, EquipmentData, null);
                    客户网络 网络连接13 = this.网络连接;
                    if (网络连接13 != null)
                    {
                        网络连接13.发送封包(new 删除玩家物品
                        {
                            背包类型 = 0,
                            物品位置 = 0
                        });
                    }
                    客户网络 网络连接14 = this.网络连接;
                    if (网络连接14 != null)
                    {
                        网络连接14.发送封包(new PutInUpgradedWeaponPacket());
                    }
                    Dictionary<byte, Dictionary<EquipmentData, int>> dictionary3 = new Dictionary<byte, Dictionary<EquipmentData, int>>();
                    dictionary3[0] = new Dictionary<EquipmentData, int>();
                    dictionary3[1] = new Dictionary<EquipmentData, int>();
                    dictionary3[2] = new Dictionary<EquipmentData, int>();
                    dictionary3[3] = new Dictionary<EquipmentData, int>();
                    dictionary3[4] = new Dictionary<EquipmentData, int>();
                    Dictionary<byte, Dictionary<EquipmentData, int>> dictionary4 = dictionary3;
                    foreach (EquipmentData EquipmentData3 in dictionary.Values)
                    {
                        Dictionary<GameObjectStats, int> 装备Stat = EquipmentData3.装备Stat;
                        int value;
                        if ((value = (装备Stat.ContainsKey(GameObjectStats.MinAttack) ? 装备Stat[GameObjectStats.MinAttack] : 0) + (装备Stat.ContainsKey(GameObjectStats.MaxAttack) ? 装备Stat[GameObjectStats.MaxAttack] : 0)) > 0)
                        {
                            dictionary4[0][EquipmentData3] = value;
                        }
                        if ((value = (装备Stat.ContainsKey(GameObjectStats.MinMagic) ? 装备Stat[GameObjectStats.MinMagic] : 0) + (装备Stat.ContainsKey(GameObjectStats.MaxMagic) ? 装备Stat[GameObjectStats.MaxMagic] : 0)) > 0)
                        {
                            dictionary4[1][EquipmentData3] = value;
                        }
                        if ((value = (装备Stat.ContainsKey(GameObjectStats.Minimalist) ? 装备Stat[GameObjectStats.Minimalist] : 0) + (装备Stat.ContainsKey(GameObjectStats.GreatestTaoism) ? 装备Stat[GameObjectStats.GreatestTaoism] : 0)) > 0)
                        {
                            dictionary4[2][EquipmentData3] = value;
                        }
                        if ((value = (装备Stat.ContainsKey(GameObjectStats.MinNeedle) ? 装备Stat[GameObjectStats.MinNeedle] : 0) + (装备Stat.ContainsKey(GameObjectStats.MaxNeedle) ? 装备Stat[GameObjectStats.MaxNeedle] : 0)) > 0)
                        {
                            dictionary4[3][EquipmentData3] = value;
                        }
                        if ((value = (装备Stat.ContainsKey(GameObjectStats.MinBow) ? 装备Stat[GameObjectStats.MinBow] : 0) + (装备Stat.ContainsKey(GameObjectStats.MaxBow) ? 装备Stat[GameObjectStats.MaxBow] : 0)) > 0)
                        {
                            dictionary4[4][EquipmentData3] = value;
                        }
                    }
                    List<KeyValuePair<byte, Dictionary<EquipmentData, int>>> 排序Stat = (from x in dictionary4.ToList<KeyValuePair<byte, Dictionary<EquipmentData, int>>>()
                                                                                       orderby x.Value.Values.Sum() descending
                                                                                       select x).ToList<KeyValuePair<byte, Dictionary<EquipmentData, int>>>();
                    List<KeyValuePair<byte, Dictionary<EquipmentData, int>>> list = (from O in 排序Stat
                                                                                     where O.Value.Values.Sum() == 排序Stat[0].Value.Values.Sum()
                                                                                     select O).ToList<KeyValuePair<byte, Dictionary<EquipmentData, int>>>();
                    byte key = list[MainProcess.RandomNumber.Next(list.Count)].Key;
                    List<KeyValuePair<EquipmentData, int>> list2 = (from x in dictionary4[key].ToList<KeyValuePair<EquipmentData, int>>()
                                                                    orderby x.Value descending
                                                                    select x).ToList<KeyValuePair<EquipmentData, int>>();
                    float num = Math.Min(10f, (float)((list2.Count >= 1) ? list2[0].Value : 1) + (float)((list2.Count >= 2) ? list2[1].Value : 0) / 3f);
                    int num2 = dictionary2.Values.Sum((ItemData x) => x.当前持久.V);
                    float num3 = Math.Max(0f, (float)(num2 - 146));
                    int num4 = (int)(90 - EquipmentData.升级次数.V * 10);
                    float 概率 = num * (float)num4 * 0.001f + num3 * 0.01f;
                    this.CharacterData.升级装备.V = EquipmentData;
                    this.CharacterData.取回时间.V = MainProcess.CurrentTime.AddHours(2.0);
                    if (this.CharacterData.升级成功.V = ComputingClass.计算概率(概率))
                    {
                        DataMonitor<byte> 升级次数 = EquipmentData.升级次数;
                        升级次数.V += 1;
                        if (key == 0)
                        {
                            DataMonitor<byte> 升级Attack = EquipmentData.升级Attack;
                            升级Attack.V += 1;
                        }
                        else if (key == 1)
                        {
                            DataMonitor<byte> 升级Magic = EquipmentData.升级Magic;
                            升级Magic.V += 1;
                        }
                        else if (key == 2)
                        {
                            DataMonitor<byte> 升级Taoism = EquipmentData.升级Taoism;
                            升级Taoism.V += 1;
                        }
                        else if (key == 3)
                        {
                            DataMonitor<byte> 升级Needle = EquipmentData.升级Needle;
                            升级Needle.V += 1;
                        }
                        else if (key == 4)
                        {
                            DataMonitor<byte> 升级Archery = EquipmentData.升级Archery;
                            升级Archery.V += 1;
                        }
                    }
                    if (num2 < 30)
                    {
                        EquipmentData.最大持久.V -= 3000;
                        EquipmentData.当前持久.V = Math.Min(EquipmentData.当前持久.V, EquipmentData.最大持久.V);
                        return;
                    }
                    if (num2 < 60)
                    {
                        EquipmentData.最大持久.V -= 2000;
                        EquipmentData.当前持久.V = Math.Min(EquipmentData.当前持久.V, EquipmentData.最大持久.V);
                        return;
                    }
                    if (num2 < 99)
                    {
                        EquipmentData.最大持久.V -= 1000;
                        EquipmentData.当前持久.V = Math.Min(EquipmentData.当前持久.V, EquipmentData.最大持久.V);
                        return;
                    }
                    if (num2 > 130 && ComputingClass.计算概率(1f - (float)EquipmentData.最大持久.V * 0.5f / (float)EquipmentData.默认持久))
                    {
                        EquipmentData.最大持久.V += 1000;
                        EquipmentData.当前持久.V = Math.Min(EquipmentData.当前持久.V, EquipmentData.最大持久.V);
                    }
                    return;
                }
            }
            else
            {
                客户网络 网络连接15 = this.网络连接;
                if (网络连接15 == null)
                {
                    return;
                }
                网络连接15.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 1856
                });
                return;
            }
        }


        public bool 玩家取回装备(int DeductCoinsCommand)
        {
            if (this.CharacterData.升级装备.V == null)
            {
                return false;
            }
            if (this.CharacterData.升级成功.V)
            {
                for (byte b = 0; b < this.背包大小; b += 1)
                {
                    if (!this.角色背包.ContainsKey(b))
                    {
                        this.NumberGoldCoins -= DeductCoinsCommand;
                        this.角色背包[b] = this.CharacterData.升级装备.V;
                        this.角色背包[b].物品位置.V = b;
                        this.角色背包[b].物品容器.V = 1;
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 != null)
                        {
                            网络连接.发送封包(new 玩家物品变动
                            {
                                物品描述 = this.角色背包[b].字节描述()
                            });
                        }
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new 取回升级武器());
                        }
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 != null)
                        {
                            网络连接3.发送封包(new 武器升级结果());
                        }
                        if (this.CharacterData.升级装备.V.升级次数.V >= 5)
                        {
                            NetworkServiceGateway.发送公告(string.Format("[{0}] successfully upgraded [{1}] to level {2}.", this.对象名字, this.CharacterData.升级装备.V.Name, this.CharacterData.升级装备.V.升级次数.V), false);
                        }
                        this.CharacterData.升级装备.V = null;
                        return this.CharacterData.升级成功.V;
                    }
                }
                this.CharacterData.升级装备.V = null;
            }
            return this.CharacterData.升级成功.V;
        }


        public void 放弃升级武器()
        {
            EquipmentData v = this.CharacterData.升级装备.V;
            if (v != null)
            {
                v.Delete();
            }
            this.CharacterData.升级装备.V = null;
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new 武器升级结果
            {
                升级结果 = 1
            });
        }


        public void 玩家发送广播(byte[] 数据)
        {
            uint num = BitConverter.ToUInt32(数据, 0);
            byte b = 数据[4];
            byte[] array = 数据.Skip(5).ToArray<byte>();
            if (num == 2415919105U)
            {
                byte[] 字节描述 = null;
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                    {
                        binaryWriter.Write(2415919105U);
                        binaryWriter.Write(this.MapId);
                        binaryWriter.Write(1);
                        binaryWriter.Write((int)this.当前等级);
                        binaryWriter.Write(array);
                        binaryWriter.Write(Encoding.UTF8.GetBytes(this.对象名字));
                        binaryWriter.Write((byte)0);
                        字节描述 = memoryStream.ToArray();
                    }
                }
                base.发送封包(new ReceiveChatMessagesNearbyPacket
                {
                    字节描述 = 字节描述
                });
                MainProcess.AddChatLog("[General][" + this.对象名字 + "]: ", array);
                return;
            }
            if (num == 2415919107U)
            {
                if (b == 1)
                {
                    if (this.NumberGoldCoins < 1000)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 4873
                        });
                        return;
                    }
                    else
                    {
                        this.NumberGoldCoins -= 1000;
                    }
                }
                else
                {
                    if (b != 6)
                    {
                        this.网络连接.尝试断开连接(new Exception(string.Format("Incorrect channel parameters are provided when transmitting or broadcasting, disconnect. Channel: {0:X8} Parameter: {1}", num, b)));
                        return;
                    }
                    ItemData 当前物品;
                    if (this.查找背包物品(2201, out 当前物品))
                    {
                        this.ConsumeBackpackItem(1, 当前物品);
                    }
                    else
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 4869
                        });
                        return;
                    }
                }
                byte[] 字节描述2 = null;
                using (MemoryStream memoryStream2 = new MemoryStream())
                {
                    using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2))
                    {
                        binaryWriter2.Write(this.MapId);
                        binaryWriter2.Write(2415919107U);
                        binaryWriter2.Write((int)b);
                        binaryWriter2.Write((int)this.当前等级);
                        binaryWriter2.Write(array);
                        binaryWriter2.Write(Encoding.UTF8.GetBytes(this.对象名字));
                        binaryWriter2.Write((byte)0);
                        字节描述2 = memoryStream2.ToArray();
                    }
                }
                NetworkServiceGateway.发送封包(new ReceiveChatMessagesPacket
                {
                    字节描述 = 字节描述2
                });
                MainProcess.AddChatLog(string.Concat(new string[]
                {
                    "[",
                    (b == 1) ? "广播" : "传音",
                    "][",
                    this.对象名字,
                    "]: "
                }), array);
                return;
            }
            this.网络连接.尝试断开连接(new Exception(string.Format("When a player sends a broadcast, the wrong channel parameter is provided. Channel: {0:X8}", num)));
        }


        public void 玩家发送消息(byte[] 数据)
        {
            int num = BitConverter.ToInt32(数据, 0);
            byte[] array = 数据.Skip(4).ToArray<byte>();
            int num2 = num >> 28;
            if (num2 != 0)
            {
                if (num2 != 6)
                {
                    if (num2 != 7)
                    {
                        return;
                    }
                    if (this.所属队伍 == null)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 3853
                        });
                        return;
                    }
                    else
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(this.MapId);
                                binaryWriter.Write(1879048192);
                                binaryWriter.Write(1);
                                binaryWriter.Write((int)this.当前等级);
                                binaryWriter.Write(array);
                                binaryWriter.Write(Encoding.UTF8.GetBytes(this.对象名字 + "\0"));
                                this.所属队伍.发送封包(new ReceiveChatMessagesPacket
                                {
                                    字节描述 = memoryStream.ToArray()
                                });
                                MainProcess.AddChatLog("[Team][" + this.对象名字 + "]: ", array);
                                return;
                            }
                        }
                    }
                }
                if (this.所属行会 == null)
                {
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new 社交错误提示
                    {
                        错误编号 = 6668
                    });
                    return;
                }
                else if (this.所属行会.行会禁言.ContainsKey(this.CharacterData))
                {
                    客户网络 网络连接3 = this.网络连接;
                    if (网络连接3 == null)
                    {
                        return;
                    }
                    网络连接3.发送封包(new 社交错误提示
                    {
                        错误编号 = 4870
                    });
                    return;
                }
                else
                {
                    using (MemoryStream memoryStream2 = new MemoryStream())
                    {
                        using (BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2))
                        {
                            binaryWriter2.Write(this.MapId);
                            binaryWriter2.Write(1610612736);
                            binaryWriter2.Write(1);
                            binaryWriter2.Write((int)this.当前等级);
                            binaryWriter2.Write(array);
                            binaryWriter2.Write(Encoding.UTF8.GetBytes(this.对象名字));
                            binaryWriter2.Write((byte)0);
                            this.所属行会.发送封包(new ReceiveChatMessagesPacket
                            {
                                字节描述 = memoryStream2.ToArray()
                            });
                            MainProcess.AddChatLog("[Guild][" + this.对象名字 + "]: ", array);
                        }
                    }
                }
                return;
            }
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(num, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.MapId == CharacterData.角色编号)
                    {
                        return;
                    }
                    if (this.CharacterData.黑名单表.Contains(this.CharacterData))
                    {
                        return;
                    }
                    if (CharacterData.ActiveConnection == null)
                    {
                        return;
                    }
                    byte[] 字节描述 = null;
                    using (MemoryStream memoryStream3 = new MemoryStream())
                    {
                        using (BinaryWriter binaryWriter3 = new BinaryWriter(memoryStream3))
                        {
                            binaryWriter3.Write(CharacterData.角色编号);
                            binaryWriter3.Write(this.MapId);
                            binaryWriter3.Write(1);
                            binaryWriter3.Write((int)this.当前等级);
                            binaryWriter3.Write(array);
                            binaryWriter3.Write(Encoding.UTF8.GetBytes(this.对象名字));
                            binaryWriter3.Write((byte)0);
                            字节描述 = memoryStream3.ToArray();
                        }
                    }
                    客户网络 网络连接4 = this.网络连接;
                    if (网络连接4 != null)
                    {
                        网络连接4.发送封包(new ReceiveChatMessagesPacket
                        {
                            字节描述 = 字节描述
                        });
                    }
                    byte[] 字节描述2 = null;
                    using (MemoryStream memoryStream4 = new MemoryStream())
                    {
                        using (BinaryWriter binaryWriter4 = new BinaryWriter(memoryStream4))
                        {
                            binaryWriter4.Write(this.MapId);
                            binaryWriter4.Write(CharacterData.角色编号);
                            binaryWriter4.Write(1);
                            binaryWriter4.Write((int)this.当前等级);
                            binaryWriter4.Write(array);
                            binaryWriter4.Write(Encoding.UTF8.GetBytes(this.对象名字));
                            binaryWriter4.Write((byte)0);
                            字节描述2 = memoryStream4.ToArray();
                        }
                    }
                    CharacterData.ActiveConnection.发送封包(new ReceiveChatMessagesPacket
                    {
                        字节描述 = 字节描述2
                    });
                    MainProcess.AddChatLog(string.Format("[Whisper][{0}]=>[{1}]: ", this.对象名字, CharacterData.CharName), array);
                    return;
                }
            }
            客户网络 网络连接5 = this.网络连接;
            if (网络连接5 == null)
            {
                return;
            }
            网络连接5.发送封包(new 社交错误提示
            {
                错误编号 = 4868
            });
        }


        public void 玩家好友聊天(byte[] 数据)
        {
            int key = BitConverter.ToInt32(数据, 0);
            byte[] array = 数据.Skip(4).ToArray<byte>();
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(key, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null && this.好友列表.Contains(CharacterData))
                {
                    if (CharacterData.ActiveConnection != null)
                    {
                        byte[] 字节数据 = null;
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                            {
                                binaryWriter.Write(this.MapId);
                                binaryWriter.Write((int)this.当前等级);
                                binaryWriter.Write(array);
                                字节数据 = memoryStream.ToArray();
                            }
                        }
                        CharacterData.ActiveConnection.发送封包(new SendFriendMessagePacket
                        {
                            字节数据 = 字节数据
                        });
                        MainProcess.AddChatLog(string.Format("[Friend][{0}]=>[{1}]: ", this.对象名字, CharacterData), array);
                        return;
                    }
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new 社交错误提示
                    {
                        错误编号 = 5124
                    });
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new 社交错误提示
            {
                错误编号 = 4868
            });
        }


        public void 玩家添加关注(int 对象编号, string 对象名字)
        {
            if (this.偶像列表.Count >= 100)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 5125
                });
                return;
            }
            else
            {
                if (对象编号 != 0)
                {
                    GameData GameData;
                    if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                    {
                        CharacterData CharacterData = GameData as CharacterData;
                        if (CharacterData != null)
                        {
                            if (this.偶像列表.Contains(CharacterData))
                            {
                                客户网络 网络连接2 = this.网络连接;
                                if (网络连接2 == null)
                                {
                                    return;
                                }
                                网络连接2.发送封包(new 社交错误提示
                                {
                                    错误编号 = 5122
                                });
                                return;
                            }
                            else
                            {
                                if (this.黑名单表.Contains(CharacterData))
                                {
                                    this.玩家解除屏蔽(CharacterData.角色编号);
                                }
                                if (this.仇人列表.Contains(CharacterData))
                                {
                                    this.玩家删除仇人(CharacterData.角色编号);
                                }
                                this.偶像列表.Add(CharacterData);
                                CharacterData.粉丝列表.Add(this.CharacterData);
                                客户网络 网络连接3 = this.网络连接;
                                if (网络连接3 != null)
                                {
                                    网络连接3.发送封包(new 玩家添加关注
                                    {
                                        对象编号 = CharacterData.数据索引.V,
                                        对象名字 = CharacterData.CharName.V,
                                        是否好友 = (this.粉丝列表.Contains(CharacterData) || CharacterData.偶像列表.Contains(this.CharacterData))
                                    });
                                }
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 != null)
                                {
                                    网络连接4.发送封包(new 好友上线下线
                                    {
                                        对象编号 = CharacterData.数据索引.V,
                                        对象名字 = CharacterData.CharName.V,
                                        对象职业 = (byte)CharacterData.角色职业.V,
                                        对象性别 = (byte)CharacterData.角色性别.V,
                                        上线下线 = ((byte)((CharacterData.ActiveConnection != null) ? 0 : 3))
                                    });
                                }
                                if (this.粉丝列表.Contains(CharacterData) || CharacterData.偶像列表.Contains(this.CharacterData))
                                {
                                    this.好友列表.Add(CharacterData);
                                    CharacterData.好友列表.Add(this.CharacterData);
                                }
                                客户网络 网络连接5 = CharacterData.ActiveConnection;
                                if (网络连接5 == null)
                                {
                                    return;
                                }
                                网络连接5.发送封包(new OtherPersonPaysAttentionToHimselfPacket
                                {
                                    对象编号 = this.MapId,
                                    对象名字 = this.对象名字
                                });
                                return;
                            }
                        }
                    }
                    this.网络连接.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 6732
                    });
                    return;
                }
                GameData GameData2;
                if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(对象名字, out GameData2))
                {
                    CharacterData CharacterData2 = GameData2 as CharacterData;
                    if (CharacterData2 != null)
                    {
                        if (this.偶像列表.Contains(CharacterData2))
                        {
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new 社交错误提示
                            {
                                错误编号 = 5122
                            });
                            return;
                        }
                        else
                        {
                            if (this.黑名单表.Contains(CharacterData2))
                            {
                                this.玩家解除屏蔽(CharacterData2.角色编号);
                            }
                            if (this.仇人列表.Contains(CharacterData2))
                            {
                                this.玩家删除仇人(CharacterData2.角色编号);
                            }
                            this.偶像列表.Add(CharacterData2);
                            CharacterData2.粉丝列表.Add(this.CharacterData);
                            客户网络 网络连接7 = this.网络连接;
                            if (网络连接7 != null)
                            {
                                网络连接7.发送封包(new 玩家添加关注
                                {
                                    对象编号 = CharacterData2.数据索引.V,
                                    对象名字 = CharacterData2.CharName.V,
                                    是否好友 = (this.粉丝列表.Contains(CharacterData2) || CharacterData2.偶像列表.Contains(this.CharacterData))
                                });
                            }
                            客户网络 网络连接8 = this.网络连接;
                            if (网络连接8 != null)
                            {
                                网络连接8.发送封包(new 好友上线下线
                                {
                                    对象编号 = CharacterData2.数据索引.V,
                                    对象名字 = CharacterData2.CharName.V,
                                    对象职业 = (byte)CharacterData2.角色职业.V,
                                    对象性别 = (byte)CharacterData2.角色性别.V,
                                    上线下线 = ((byte)((CharacterData2.ActiveConnection != null) ? 0 : 3))
                                });
                            }
                            if (this.粉丝列表.Contains(CharacterData2) || CharacterData2.偶像列表.Contains(this.CharacterData))
                            {
                                this.好友列表.Add(CharacterData2);
                                CharacterData2.好友列表.Add(this.CharacterData);
                            }
                            客户网络 网络连接9 = CharacterData2.ActiveConnection;
                            if (网络连接9 == null)
                            {
                                return;
                            }
                            网络连接9.发送封包(new OtherPersonPaysAttentionToHimselfPacket
                            {
                                对象编号 = this.MapId,
                                对象名字 = this.对象名字
                            });
                            return;
                        }
                    }
                }
                this.网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6732
                });
                return;
            }
        }


        public void 玩家取消关注(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(this.对象名字, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (!this.偶像列表.Contains(CharacterData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5121
                        });
                        return;
                    }
                    else
                    {
                        this.偶像列表.Remove(CharacterData);
                        CharacterData.粉丝列表.Remove(this.CharacterData);
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new 玩家取消关注
                            {
                                对象编号 = CharacterData.角色编号
                            });
                        }
                        if (this.好友列表.Contains(CharacterData) || CharacterData.好友列表.Contains(this.CharacterData))
                        {
                            this.好友列表.Remove(CharacterData);
                            CharacterData.好友列表.Remove(this.CharacterData);
                        }
                        客户网络 网络连接3 = CharacterData.ActiveConnection;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new OtherPartyUnfollowsPacket
                        {
                            对象编号 = this.MapId,
                            对象名字 = this.对象名字
                        });
                        return;
                    }
                }
            }
            this.网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6732
            });
        }


        public void 玩家添加仇人(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.仇人列表.Count >= 100)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5125
                        });
                        return;
                    }
                    else if (this.偶像列表.Contains(CharacterData))
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 5122
                        });
                        return;
                    }
                    else
                    {
                        this.仇人列表.Add(CharacterData);
                        CharacterData.仇恨列表.Add(this.CharacterData);
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 != null)
                        {
                            网络连接3.发送封包(new 玩家标记仇人
                            {
                                对象编号 = CharacterData.数据索引.V
                            });
                        }
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new 好友上线下线
                        {
                            对象编号 = CharacterData.数据索引.V,
                            对象名字 = CharacterData.CharName.V,
                            对象职业 = (byte)CharacterData.角色职业.V,
                            对象性别 = (byte)CharacterData.角色性别.V,
                            上线下线 = ((byte)((CharacterData.ActiveConnection != null) ? 0 : 3))
                        });
                        return;
                    }
                }
            }
            this.网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6732
            });
        }


        public void 玩家删除仇人(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(this.对象名字, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (!this.仇人列表.Contains(CharacterData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5126
                        });
                        return;
                    }
                    else
                    {
                        this.仇人列表.Remove(CharacterData);
                        CharacterData.仇恨列表.Remove(this.CharacterData);
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 玩家移除仇人
                        {
                            对象编号 = CharacterData.数据索引.V
                        });
                        return;
                    }
                }
            }
            this.网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6732
            });
        }


        public void 玩家屏蔽目标(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(this.对象名字, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.黑名单表.Count >= 100)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 7428
                        });
                        return;
                    }
                    else if (this.黑名单表.Contains(CharacterData))
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 7426
                        });
                        return;
                    }
                    else if (对象编号 == this.MapId)
                    {
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new 社交错误提示
                        {
                            错误编号 = 7429
                        });
                        return;
                    }
                    else
                    {
                        if (this.偶像列表.Contains(CharacterData))
                        {
                            this.玩家取消关注(CharacterData.角色编号);
                        }
                        this.黑名单表.Add(CharacterData);
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new 玩家屏蔽目标
                        {
                            对象编号 = CharacterData.数据索引.V,
                            对象名字 = CharacterData.CharName.V
                        });
                        return;
                    }
                }
            }
            this.网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6732
            });
        }


        public void 玩家解除屏蔽(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (!this.黑名单表.Contains(CharacterData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 7427
                        });
                        return;
                    }
                    else
                    {
                        this.黑名单表.Remove(CharacterData);
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new UnblockPlayerPacket
                        {
                            对象编号 = CharacterData.数据索引.V
                        });
                        return;
                    }
                }
            }
            this.网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6732
            });
        }


        public void 请求对象外观(int 对象编号, int 状态编号)
        {
            MapObject MapObject;
            if (!MapGatewayProcess.Objects.TryGetValue(对象编号, out MapObject))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
            else
            {
                PlayerObject PlayerObject = MapObject as PlayerObject;
                if (PlayerObject != null)
                {
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    SyncPlayerAppearancePacket SyncPlayerAppearancePacket = new SyncPlayerAppearancePacket();
                    SyncPlayerAppearancePacket.对象编号 = PlayerObject.MapId;
                    SyncPlayerAppearancePacket.对象PK值 = PlayerObject.PK值惩罚;
                    SyncPlayerAppearancePacket.对象职业 = (byte)PlayerObject.角色职业;
                    SyncPlayerAppearancePacket.对象性别 = (byte)PlayerObject.角色性别;
                    SyncPlayerAppearancePacket.对象发型 = (byte)PlayerObject.角色发型;
                    SyncPlayerAppearancePacket.对象发色 = (byte)PlayerObject.角色发色;
                    SyncPlayerAppearancePacket.对象脸型 = (byte)PlayerObject.角色脸型;
                    SyncPlayerAppearancePacket.摆摊状态 = PlayerObject.摆摊状态;
                    SyncPlayerAppearancePacket.摊位名字 = PlayerObject.摊位名字;
                    EquipmentData EquipmentData;
                    SyncPlayerAppearancePacket.武器等级 = ((byte)(PlayerObject.角色装备.TryGetValue(0, out EquipmentData) ? ((EquipmentData != null) ? EquipmentData.升级次数.V : 0) : 0));
                    SyncPlayerAppearancePacket.身上武器 = ((EquipmentData != null) ? EquipmentData.对应模板.V.Id : 0);
                    EquipmentData EquipmentData2;
                    int 身上衣服;
                    if (!PlayerObject.角色装备.TryGetValue(1, out EquipmentData2))
                    {
                        身上衣服 = 0;
                    }
                    else
                    {
                        int? num;
                        if (EquipmentData2 == null)
                        {
                            num = null;
                        }
                        else
                        {
                            DataMonitor<GameItems> 对应模板 = EquipmentData2.对应模板;
                            if (对应模板 == null)
                            {
                                num = null;
                            }
                            else
                            {
                                GameItems v = 对应模板.V;
                                num = ((v != null) ? new int?(v.Id) : null);
                            }
                        }
                        int? num2 = num;
                        身上衣服 = num2.GetValueOrDefault();
                    }
                    SyncPlayerAppearancePacket.身上衣服 = 身上衣服;
                    EquipmentData EquipmentData3;
                    int 身上披风;
                    if (!PlayerObject.角色装备.TryGetValue(2, out EquipmentData3))
                    {
                        身上披风 = 0;
                    }
                    else
                    {
                        int? num3;
                        if (EquipmentData3 == null)
                        {
                            num3 = null;
                        }
                        else
                        {
                            DataMonitor<GameItems> 对应模板2 = EquipmentData3.对应模板;
                            if (对应模板2 == null)
                            {
                                num3 = null;
                            }
                            else
                            {
                                GameItems v2 = 对应模板2.V;
                                num3 = ((v2 != null) ? new int?(v2.Id) : null);
                            }
                        }
                        int? num2 = num3;
                        身上披风 = num2.GetValueOrDefault();
                    }
                    SyncPlayerAppearancePacket.身上披风 = 身上披风;
                    SyncPlayerAppearancePacket.当前体力 = PlayerObject[GameObjectStats.MaxPhysicalStrength];
                    SyncPlayerAppearancePacket.当前魔力 = PlayerObject[GameObjectStats.MaxMagic2];
                    SyncPlayerAppearancePacket.对象名字 = PlayerObject.对象名字;
                    GuildData 所属行会 = PlayerObject.所属行会;
                    SyncPlayerAppearancePacket.行会编号 = ((所属行会 != null) ? 所属行会.数据索引.V : 0);
                    网络连接2.发送封包(SyncPlayerAppearancePacket);
                    return;
                }
                else
                {
                    MonsterObject MonsterObject = MapObject as MonsterObject;
                    if (MonsterObject != null)
                    {
                        if (MonsterObject.出生地图 == null)
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new SyncExtendedDataPacket
                            {
                                对象类型 = 1,
                                主人编号 = 0,
                                主人名字 = "",
                                对象等级 = MonsterObject.当前等级,
                                对象编号 = MonsterObject.MapId,
                                模板编号 = MonsterObject.模板编号,
                                当前等级 = MonsterObject.宠物等级,
                                对象质量 = (byte)MonsterObject.Category,
                                MaxPhysicalStrength = MonsterObject[GameObjectStats.MaxPhysicalStrength]
                            });
                            return;
                        }
                        else
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            同步Npcc数据 同步Npcc数据 = new 同步Npcc数据();
                            同步Npcc数据.对象编号 = MonsterObject.MapId;
                            同步Npcc数据.对象等级 = MonsterObject.当前等级;
                            同步Npcc数据.对象质量 = (byte)MonsterObject.Category;
                            Monsters 对象模板 = MonsterObject.对象模板;
                            同步Npcc数据.对象模板 = ((ushort)((对象模板 != null) ? 对象模板.Id : 0));
                            同步Npcc数据.体力上限 = MonsterObject[GameObjectStats.MaxPhysicalStrength];
                            网络连接4.发送封包(同步Npcc数据);
                            return;
                        }
                    }
                    else
                    {
                        PetObject PetObject = MapObject as PetObject;
                        if (PetObject == null)
                        {
                            GuardInstance GuardInstance = MapObject as GuardInstance;
                            if (GuardInstance != null)
                            {
                                客户网络 网络连接5 = this.网络连接;
                                if (网络连接5 == null)
                                {
                                    return;
                                }
                                同步Npcc数据 同步Npcc数据2 = new 同步Npcc数据();
                                同步Npcc数据2.对象质量 = 3;
                                同步Npcc数据2.对象编号 = GuardInstance.MapId;
                                同步Npcc数据2.对象等级 = GuardInstance.当前等级;
                                Guards 对象模板2 = GuardInstance.对象模板;
                                同步Npcc数据2.对象模板 = ((ushort)((对象模板2 != null) ? 对象模板2.GuardNumber : 0));
                                同步Npcc数据2.体力上限 = GuardInstance[GameObjectStats.MaxPhysicalStrength];
                                网络连接5.发送封包(同步Npcc数据2);
                            }
                            return;
                        }
                        客户网络 网络连接6 = this.网络连接;
                        if (网络连接6 == null)
                        {
                            return;
                        }
                        SyncExtendedDataPacket SyncExtendedDataPacket = new SyncExtendedDataPacket();
                        SyncExtendedDataPacket.对象类型 = 2;
                        SyncExtendedDataPacket.对象编号 = PetObject.MapId;
                        SyncExtendedDataPacket.模板编号 = PetObject.模板编号;
                        SyncExtendedDataPacket.当前等级 = PetObject.宠物等级;
                        SyncExtendedDataPacket.对象等级 = PetObject.当前等级;
                        SyncExtendedDataPacket.对象质量 = (byte)PetObject.宠物级别;
                        SyncExtendedDataPacket.MaxPhysicalStrength = PetObject[GameObjectStats.MaxPhysicalStrength];
                        PlayerObject 宠物主人 = PetObject.宠物主人;
                        SyncExtendedDataPacket.主人编号 = ((宠物主人 != null) ? 宠物主人.MapId : 0);
                        PlayerObject 宠物主人2 = PetObject.宠物主人;
                        string 主人名字;
                        if (宠物主人2 != null)
                        {
                            if ((主人名字 = 宠物主人2.对象名字) != null)
                            {
                                goto IL_3C8;
                            }
                        }
                        主人名字 = "";
                    IL_3C8:
                        SyncExtendedDataPacket.主人名字 = 主人名字;
                        网络连接6.发送封包(SyncExtendedDataPacket);
                        return;
                    }
                }
            }
        }


        public void 请求角色资料(int 角色编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(角色编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    同步角色信息 同步角色信息 = new 同步角色信息();
                    同步角色信息.对象编号 = CharacterData.数据索引.V;
                    同步角色信息.对象名字 = CharacterData.CharName.V;
                    同步角色信息.会员等级 = CharacterData.本期特权.V;
                    同步角色信息.对象职业 = (byte)CharacterData.角色职业.V;
                    同步角色信息.对象性别 = (byte)CharacterData.角色性别.V;
                    GuildData v = CharacterData.所属行会.V;
                    string 行会名字;
                    if (v != null)
                    {
                        if ((行会名字 = v.行会名字.V) != null)
                        {
                            goto IL_B1;
                        }
                    }
                    行会名字 = "";
                IL_B1:
                    同步角色信息.行会名字 = 行会名字;
                    网络连接.发送封包(同步角色信息);
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new 社交错误提示
            {
                错误编号 = 6732
            });
        }


        public void 查询玩家战力(int 对象编号)
        {
            MapObject MapObject;
            if (MapGatewayProcess.Objects.TryGetValue(对象编号, out MapObject))
            {
                PlayerObject PlayerObject = MapObject as PlayerObject;
                if (PlayerObject != null)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new SyncPlayerPowerPacket
                    {
                        角色编号 = PlayerObject.MapId,
                        角色战力 = PlayerObject.当前战力
                    });
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 7171
            });
        }


        public void 查看对象装备(int 对象编号)
        {
            MapObject MapObject;
            if (MapGatewayProcess.Objects.TryGetValue(对象编号, out MapObject))
            {
                PlayerObject PlayerObject = MapObject as PlayerObject;
                if (PlayerObject != null)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new 同步角色装备
                        {
                            对象编号 = PlayerObject.MapId,
                            装备数量 = (byte)PlayerObject.角色装备.Count,
                            字节描述 = PlayerObject.装备物品描述()
                        });
                    }
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new SyncMarfaPrivilegesPacket
                    {
                        玛法特权 = PlayerObject.本期特权
                    });
                    return;
                }
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 == null)
            {
                return;
            }
            网络连接3.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 7171
            });
        }


        public void 查询排名榜单(int 榜单类型, int 起始位置)
        {
            if (起始位置 < 0 || 起始位置 > 29)
            {
                return;
            }
            byte b = (byte)榜单类型;
            int num = 0;
            int num2 = 起始位置 * 10;
            int num3 = 起始位置 * 10 + 10;
            ListMonitor<CharacterData> ListMonitor = null;
            switch (榜单类型)
            {
                case 0:
                    ListMonitor = SystemData.数据.个人等级排名;
                    num = 0;
                    break;
                case 1:
                    ListMonitor = SystemData.数据.战士等级排名;
                    num = 0;
                    break;
                case 2:
                    ListMonitor = SystemData.数据.法师等级排名;
                    num = 0;
                    break;
                case 3:
                    ListMonitor = SystemData.数据.道士等级排名;
                    num = 0;
                    break;
                case 4:
                    ListMonitor = SystemData.数据.刺客等级排名;
                    num = 0;
                    break;
                case 5:
                    ListMonitor = SystemData.数据.弓手等级排名;
                    num = 0;
                    break;
                case 6:
                    ListMonitor = SystemData.数据.个人战力排名;
                    num = 1;
                    break;
                case 7:
                    ListMonitor = SystemData.数据.战士战力排名;
                    num = 1;
                    break;
                case 8:
                    ListMonitor = SystemData.数据.法师战力排名;
                    num = 1;
                    break;
                case 9:
                    ListMonitor = SystemData.数据.道士战力排名;
                    num = 1;
                    break;
                case 10:
                    ListMonitor = SystemData.数据.刺客战力排名;
                    num = 1;
                    break;
                case 11:
                    ListMonitor = SystemData.数据.弓手战力排名;
                    num = 1;
                    break;
                case 12:
                case 13:
                    break;
                case 14:
                    ListMonitor = SystemData.数据.个人声望排名;
                    num = 2;
                    break;
                case 15:
                    ListMonitor = SystemData.数据.个人PK值排名;
                    num = 3;
                    break;
                default:
                    if (榜单类型 != 36)
                    {
                        if (榜单类型 == 37)
                        {
                            ListMonitor = SystemData.数据.龙枪战力排名;
                            num = 1;
                        }
                    }
                    else
                    {
                        ListMonitor = SystemData.数据.龙枪等级排名;
                        num = 0;
                    }
                    break;
            }
            if (ListMonitor != null && ListMonitor.Count != 0)
            {
                using (MemoryStream memoryStream = new MemoryStream(new byte[189]))
                {
                    using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                    {
                        binaryWriter.Write(b);
                        binaryWriter.Write((ushort)this.CharacterData.当前排名[b]);
                        binaryWriter.Write((ushort)this.CharacterData.历史排名[b]);
                        binaryWriter.Write(ListMonitor.Count);
                        for (int i = num2; i < num3; i++)
                        {
                            BinaryWriter binaryWriter2 = binaryWriter;
                            CharacterData CharacterData = ListMonitor[i];
                            binaryWriter2.Write((long)((CharacterData != null) ? CharacterData.角色编号 : 0));
                        }
                        for (int j = num2; j < num3; j++)
                        {
                            switch (num)
                            {
                                case 0:
                                    {
                                        BinaryWriter binaryWriter3 = binaryWriter;
                                        CharacterData CharacterData2 = ListMonitor[j];
                                        binaryWriter3.Write((long)((long)((ulong)((CharacterData2 != null) ? CharacterData2.角色等级 : 0)) << 56));
                                        break;
                                    }
                                case 1:
                                    {
                                        BinaryWriter binaryWriter4 = binaryWriter;
                                        CharacterData CharacterData3 = ListMonitor[j];
                                        binaryWriter4.Write((long)((CharacterData3 != null) ? CharacterData3.角色战力 : 0));
                                        break;
                                    }
                                case 2:
                                    {
                                        BinaryWriter binaryWriter5 = binaryWriter;
                                        CharacterData CharacterData4 = ListMonitor[j];
                                        binaryWriter5.Write((long)((CharacterData4 != null) ? CharacterData4.师门声望 : 0));
                                        break;
                                    }
                                case 3:
                                    {
                                        BinaryWriter binaryWriter6 = binaryWriter;
                                        CharacterData CharacterData5 = ListMonitor[j];
                                        binaryWriter6.Write((long)((CharacterData5 != null) ? CharacterData5.角色PK值 : 0));
                                        break;
                                    }
                                default:
                                    binaryWriter.Write(0);
                                    break;
                            }
                        }
                        for (int k = num2; k < num3; k++)
                        {
                            BinaryWriter binaryWriter7 = binaryWriter;
                            CharacterData CharacterData6 = ListMonitor[k];
                            binaryWriter7.Write((ushort)((CharacterData6 != null) ? CharacterData6.历史排名[b] : 0));
                        }
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 != null)
                        {
                            网络连接.发送封包(new 查询排行榜单
                            {
                                字节数据 = memoryStream.ToArray()
                            });
                        }
                    }
                }
                return;
            }
        }


        public void 查询附近队伍()
        {
        }


        public void 查询队伍信息(int 对象编号)
        {
            if (对象编号 == this.MapId)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 3852
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        查询队伍应答 查询队伍应答 = new 查询队伍应答();
                        TeamData 当前队伍 = CharacterData.当前队伍;
                        查询队伍应答.队伍编号 = ((当前队伍 != null) ? 当前队伍.队伍编号 : 0);
                        TeamData 当前队伍2 = CharacterData.当前队伍;
                        查询队伍应答.队长编号 = ((当前队伍2 != null) ? 当前队伍2.队长编号 : 0);
                        TeamData 当前队伍3 = CharacterData.当前队伍;
                        string 队伍名字;
                        if (当前队伍3 != null)
                        {
                            if ((队伍名字 = 当前队伍3.队长名字) != null)
                            {
                                goto IL_A4;
                            }
                        }
                        队伍名字 = "";
                    IL_A4:
                        查询队伍应答.队伍名字 = 队伍名字;
                        网络连接2.发送封包(查询队伍应答);
                        return;
                    }
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 申请创建队伍(int 对象编号, byte 分配方式)
        {
            if (this.所属队伍 != null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 3847
                });
                return;
            }
            else if (this.MapId == 对象编号)
            {
                this.所属队伍 = new TeamData(this.CharacterData, 1);
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 玩家加入队伍
                {
                    字节描述 = this.所属队伍.队伍描述()
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (CharacterData.当前队伍 != null)
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 3847
                            });
                            return;
                        }
                        else
                        {
                            客户网络 客户网络;
                            if (CharacterData.角色在线(out 客户网络))
                            {
                                this.所属队伍 = new TeamData(this.CharacterData, 1);
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 != null)
                                {
                                    网络连接4.发送封包(new 玩家加入队伍
                                    {
                                        字节描述 = this.所属队伍.队伍描述()
                                    });
                                }
                                this.所属队伍.邀请列表[CharacterData] = MainProcess.CurrentTime.AddMinutes(5.0);
                                客户网络 网络连接5 = this.网络连接;
                                if (网络连接5 != null)
                                {
                                    网络连接5.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3842
                                    });
                                }
                                客户网络.发送封包(new SendTeamRequestBPacket
                                {
                                    组队方式 = 0,
                                    对象编号 = this.MapId,
                                    对象职业 = (byte)this.角色职业,
                                    对象名字 = this.对象名字
                                });
                                return;
                            }
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new 社交错误提示
                            {
                                错误编号 = 3844
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接7 = this.网络连接;
                if (网络连接7 == null)
                {
                    return;
                }
                网络连接7.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void SendTeamRequestPacket(int 对象编号)
        {
            if (对象编号 == this.MapId)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 3852
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (this.所属队伍 == null)
                        {
                            客户网络 客户网络;
                            if (CharacterData.当前队伍 == null)
                            {
                                客户网络 网络连接2 = this.网络连接;
                                if (网络连接2 == null)
                                {
                                    return;
                                }
                                网络连接2.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3860
                                });
                                return;
                            }
                            else if (CharacterData.当前队伍.队员数量 >= 11)
                            {
                                客户网络 网络连接3 = this.网络连接;
                                if (网络连接3 == null)
                                {
                                    return;
                                }
                                网络连接3.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3848
                                });
                                return;
                            }
                            else if (CharacterData.当前队伍.队长数据.角色在线(out 客户网络))
                            {
                                CharacterData.当前队伍.申请列表[this.CharacterData] = MainProcess.CurrentTime.AddMinutes(5.0);
                                客户网络.发送封包(new SendTeamRequestBPacket
                                {
                                    组队方式 = 1,
                                    对象编号 = this.MapId,
                                    对象职业 = (byte)this.角色职业,
                                    对象名字 = this.对象名字
                                });
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 == null)
                                {
                                    return;
                                }
                                网络连接4.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3842
                                });
                                return;
                            }
                            else
                            {
                                客户网络 网络连接5 = this.网络连接;
                                if (网络连接5 == null)
                                {
                                    return;
                                }
                                网络连接5.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3844
                                });
                                return;
                            }
                        }
                        else if (this.MapId != this.所属队伍.队长编号)
                        {
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new 社交错误提示
                            {
                                错误编号 = 3850
                            });
                            return;
                        }
                        else if (CharacterData.当前队伍 != null)
                        {
                            客户网络 网络连接7 = this.网络连接;
                            if (网络连接7 == null)
                            {
                                return;
                            }
                            网络连接7.发送封包(new 社交错误提示
                            {
                                错误编号 = 3847
                            });
                            return;
                        }
                        else if (this.所属队伍.队员数量 >= 11)
                        {
                            客户网络 网络连接8 = this.网络连接;
                            if (网络连接8 == null)
                            {
                                return;
                            }
                            网络连接8.发送封包(new 社交错误提示
                            {
                                错误编号 = 3848
                            });
                            return;
                        }
                        else
                        {
                            客户网络 客户网络2;
                            if (CharacterData.角色在线(out 客户网络2))
                            {
                                this.所属队伍.邀请列表[CharacterData] = MainProcess.CurrentTime.AddMinutes(5.0);
                                客户网络 网络连接9 = this.网络连接;
                                if (网络连接9 != null)
                                {
                                    网络连接9.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3842
                                    });
                                }
                                客户网络2.发送封包(new SendTeamRequestBPacket
                                {
                                    组队方式 = 0,
                                    对象编号 = this.MapId,
                                    对象职业 = (byte)this.角色职业,
                                    对象名字 = this.对象名字
                                });
                                return;
                            }
                            客户网络 网络连接10 = this.网络连接;
                            if (网络连接10 == null)
                            {
                                return;
                            }
                            网络连接10.发送封包(new 社交错误提示
                            {
                                错误编号 = 3844
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接11 = this.网络连接;
                if (网络连接11 == null)
                {
                    return;
                }
                网络连接11.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 回应组队请求(int 对象编号, byte 组队方式, byte 回应方式)
        {
            if (this.MapId != 对象编号)
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (组队方式 == 0)
                        {
                            if (回应方式 == 0)
                            {
                                if (CharacterData.当前队伍 == null)
                                {
                                    客户网络 网络连接 = this.网络连接;
                                    if (网络连接 == null)
                                    {
                                        return;
                                    }
                                    网络连接.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3860
                                    });
                                    return;
                                }
                                else if (this.所属队伍 != null)
                                {
                                    客户网络 网络连接2 = this.网络连接;
                                    if (网络连接2 == null)
                                    {
                                        return;
                                    }
                                    网络连接2.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3847
                                    });
                                    return;
                                }
                                else if (CharacterData.当前队伍.队员数量 >= 11)
                                {
                                    客户网络 网络连接3 = this.网络连接;
                                    if (网络连接3 == null)
                                    {
                                        return;
                                    }
                                    网络连接3.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3848
                                    });
                                    return;
                                }
                                else if (!CharacterData.当前队伍.邀请列表.ContainsKey(this.CharacterData))
                                {
                                    客户网络 网络连接4 = this.网络连接;
                                    if (网络连接4 == null)
                                    {
                                        return;
                                    }
                                    网络连接4.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3860
                                    });
                                    return;
                                }
                                else if (CharacterData.当前队伍.邀请列表[this.CharacterData] < MainProcess.CurrentTime)
                                {
                                    客户网络 网络连接5 = this.网络连接;
                                    if (网络连接5 == null)
                                    {
                                        return;
                                    }
                                    网络连接5.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3860
                                    });
                                    return;
                                }
                                else
                                {
                                    CharacterData.当前队伍.发送封包(new AddMembersToTeamPacket
                                    {
                                        队伍编号 = CharacterData.当前队伍.队伍编号,
                                        对象编号 = this.MapId,
                                        对象名字 = this.对象名字,
                                        对象性别 = (byte)this.角色性别,
                                        对象职业 = (byte)this.角色职业,
                                        在线离线 = 0
                                    });
                                    this.所属队伍 = CharacterData.当前队伍;
                                    CharacterData.当前队伍.队伍成员.Add(this.CharacterData);
                                    客户网络 网络连接6 = this.网络连接;
                                    if (网络连接6 == null)
                                    {
                                        return;
                                    }
                                    网络连接6.发送封包(new 玩家加入队伍
                                    {
                                        字节描述 = this.所属队伍.队伍描述()
                                    });
                                    return;
                                }
                            }
                            else
                            {
                                TeamData 当前队伍 = CharacterData.当前队伍;
                                客户网络 客户网络;
                                if (当前队伍 != null && 当前队伍.邀请列表.Remove(this.CharacterData) && CharacterData.角色在线(out 客户网络))
                                {
                                    客户网络.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 3856
                                    });
                                }
                                客户网络 网络连接7 = this.网络连接;
                                if (网络连接7 == null)
                                {
                                    return;
                                }
                                网络连接7.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3855
                                });
                                return;
                            }
                        }
                        else if (回应方式 == 0)
                        {
                            if (this.所属队伍 == null)
                            {
                                客户网络 网络连接8 = this.网络连接;
                                if (网络连接8 == null)
                                {
                                    return;
                                }
                                网络连接8.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3860
                                });
                                return;
                            }
                            else if (this.所属队伍.队员数量 >= 11)
                            {
                                客户网络 网络连接9 = this.网络连接;
                                if (网络连接9 == null)
                                {
                                    return;
                                }
                                网络连接9.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3848
                                });
                                return;
                            }
                            else if (this.MapId != this.所属队伍.队长编号)
                            {
                                客户网络 网络连接10 = this.网络连接;
                                if (网络连接10 == null)
                                {
                                    return;
                                }
                                网络连接10.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3850
                                });
                                return;
                            }
                            else if (!this.所属队伍.申请列表.ContainsKey(CharacterData))
                            {
                                客户网络 网络连接11 = this.网络连接;
                                if (网络连接11 == null)
                                {
                                    return;
                                }
                                网络连接11.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3860
                                });
                                return;
                            }
                            else if (this.所属队伍.申请列表[CharacterData] < MainProcess.CurrentTime)
                            {
                                客户网络 网络连接12 = this.网络连接;
                                if (网络连接12 == null)
                                {
                                    return;
                                }
                                网络连接12.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3860
                                });
                                return;
                            }
                            else if (CharacterData.当前队伍 != null)
                            {
                                客户网络 网络连接13 = this.网络连接;
                                if (网络连接13 == null)
                                {
                                    return;
                                }
                                网络连接13.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3847
                                });
                                return;
                            }
                            else
                            {
                                客户网络 客户网络2;
                                if (CharacterData.角色在线(out 客户网络2))
                                {
                                    this.所属队伍.发送封包(new AddMembersToTeamPacket
                                    {
                                        队伍编号 = this.所属队伍.队伍编号,
                                        对象编号 = CharacterData.角色编号,
                                        对象名字 = CharacterData.CharName.V,
                                        对象性别 = (byte)CharacterData.角色性别.V,
                                        对象职业 = (byte)CharacterData.角色职业.V,
                                        在线离线 = 0
                                    });
                                    CharacterData.当前队伍 = this.所属队伍;
                                    this.所属队伍.队伍成员.Add(CharacterData);
                                    客户网络2.发送封包(new 玩家加入队伍
                                    {
                                        字节描述 = this.所属队伍.队伍描述()
                                    });
                                    return;
                                }
                                return;
                            }
                        }
                        else
                        {
                            TeamData 所属队伍 = this.所属队伍;
                            客户网络 客户网络3;
                            if (所属队伍 != null && 所属队伍.申请列表.Remove(CharacterData) && CharacterData.角色在线(out 客户网络3))
                            {
                                客户网络3.发送封包(new 社交错误提示
                                {
                                    错误编号 = 3858
                                });
                            }
                            客户网络 网络连接14 = this.网络连接;
                            if (网络连接14 == null)
                            {
                                return;
                            }
                            网络连接14.发送封包(new 社交错误提示
                            {
                                错误编号 = 3857
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接15 = this.网络连接;
                if (网络连接15 == null)
                {
                    return;
                }
                网络连接15.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
            客户网络 网络连接16 = this.网络连接;
            if (网络连接16 == null)
            {
                return;
            }
            网络连接16.发送封包(new 社交错误提示
            {
                错误编号 = 3852
            });
        }


        public void 申请队员离队(int 对象编号)
        {
            if (this.所属队伍 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 3854
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (this.CharacterData == CharacterData)
                        {
                            this.所属队伍.队伍成员.Remove(this.CharacterData);
                            this.所属队伍.发送封包(new TeamMembersLeavePacket
                            {
                                对象编号 = this.MapId,
                                队伍编号 = this.所属队伍.数据索引.V
                            });
                            客户网络 网络连接2 = this.网络连接;
                            if (网络连接2 != null)
                            {
                                网络连接2.发送封包(new 玩家离开队伍
                                {
                                    队伍编号 = this.所属队伍.数据索引.V
                                });
                            }
                            if (this.CharacterData == this.所属队伍.队长数据)
                            {
                                CharacterData CharacterData2 = this.所属队伍.队伍成员.FirstOrDefault((CharacterData O) => O.ActiveConnection != null);
                                if (CharacterData2 != null)
                                {
                                    this.所属队伍.队长数据 = CharacterData2;
                                    this.所属队伍.发送封包(new TeamStatusChangePacket
                                    {
                                        成员上限 = 11,
                                        队伍编号 = this.所属队伍.队伍编号,
                                        队伍名字 = this.所属队伍.队长名字,
                                        分配方式 = this.所属队伍.拾取方式,
                                        队长编号 = this.所属队伍.队长编号
                                    });
                                }
                                else
                                {
                                    this.所属队伍.Delete();
                                }
                            }
                            this.CharacterData.当前队伍 = null;
                            return;
                        }
                        if (!this.所属队伍.队伍成员.Contains(CharacterData))
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 6732
                            });
                            return;
                        }
                        else if (this.CharacterData != this.所属队伍.队长数据)
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 3850
                            });
                            return;
                        }
                        else
                        {
                            this.所属队伍.队伍成员.Remove(CharacterData);
                            CharacterData.当前队伍 = null;
                            this.所属队伍.发送封包(new TeamMembersLeavePacket
                            {
                                队伍编号 = this.所属队伍.数据索引.V,
                                对象编号 = CharacterData.角色编号
                            });
                            客户网络 网络连接5 = CharacterData.ActiveConnection;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new 玩家离开队伍
                            {
                                队伍编号 = this.所属队伍.数据索引.V
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接6 = this.网络连接;
                if (网络连接6 == null)
                {
                    return;
                }
                网络连接6.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 申请移交队长(int 对象编号)
        {
            if (this.所属队伍 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 3854
                });
                return;
            }
            else if (this.CharacterData != this.所属队伍.队长数据)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 3850
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (CharacterData == this.CharacterData)
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 3852
                            });
                            return;
                        }
                        else
                        {
                            if (this.所属队伍.队伍成员.Contains(CharacterData))
                            {
                                this.所属队伍.队长数据 = CharacterData;
                                this.所属队伍.发送封包(new TeamStatusChangePacket
                                {
                                    成员上限 = 11,
                                    队伍编号 = this.所属队伍.队伍编号,
                                    队伍名字 = this.所属队伍.队长名字,
                                    分配方式 = this.所属队伍.拾取方式,
                                    队长编号 = this.所属队伍.队长编号
                                });
                                return;
                            }
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 6732
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接5 = this.网络连接;
                if (网络连接5 == null)
                {
                    return;
                }
                网络连接5.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void QueryMailboxContentPacket()
        {
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new 同步邮箱内容
            {
                字节数据 = this.全部邮件描述()
            });
        }


        public void 申请发送邮件(byte[] 数据)
        {
            if (数据.Length < 94 || 数据.Length > 839)
            {
                this.网络连接.尝试断开连接(new Exception("错误操作: 申请发送邮件.  错误: 数据长度错误."));
                return;
            }
            if (MainProcess.CurrentTime < this.邮件时间)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6151
                });
                return;
            }
            else if (this.NumberGoldCoins < 1000)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6149
                });
                return;
            }
            else
            {
                byte[] array = 数据.Take(32).ToArray<byte>();
                byte[] array2 = 数据.Skip(32).Take(61).ToArray<byte>();
                数据.Skip(93).Take(4).ToArray<byte>();
                byte[] array3 = 数据.Skip(97).ToArray<byte>();
                if (array[0] == 0 || array2[0] == 0 || array3[0] == 0)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Request to send an email.  Error: Incorrect message text."));
                    return;
                }
                string key = Encoding.UTF8.GetString(array).Split(new char[1], StringSplitOptions.RemoveEmptyEntries)[0];
                string 标题 = Encoding.UTF8.GetString(array2).Split(new char[1], StringSplitOptions.RemoveEmptyEntries)[0];
                string 正文 = Encoding.UTF8.GetString(array3).Split(new char[1], StringSplitOptions.RemoveEmptyEntries)[0];
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(key, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (CharacterData.角色邮件.Count >= 100)
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 6147
                            });
                            return;
                        }
                        else
                        {
                            this.NumberGoldCoins -= 1000;
                            CharacterData.发送邮件(new MailData(this.CharacterData, 标题, 正文, null));
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 成功发送邮件());
                            return;
                        }
                    }
                }
                客户网络 网络连接5 = this.网络连接;
                if (网络连接5 == null)
                {
                    return;
                }
                网络连接5.发送封包(new 社交错误提示
                {
                    错误编号 = 6146
                });
                return;
            }
        }


        public void 查看邮件内容(int 邮件编号)
        {
            GameData GameData;
            if (GameDataGateway.MailData表.DataSheet.TryGetValue(邮件编号, out GameData))
            {
                MailData MailData = GameData as MailData;
                if (MailData != null)
                {
                    if (!this.全部邮件.Contains(MailData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 6148
                        });
                        return;
                    }
                    else
                    {
                        this.未读邮件.Remove(MailData);
                        MailData.未读邮件.V = false;
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 同步邮件内容
                        {
                            字节数据 = MailData.邮件内容描述()
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 == null)
            {
                return;
            }
            网络连接3.发送封包(new 社交错误提示
            {
                错误编号 = 6148
            });
        }


        public void 删除指定邮件(int 邮件编号)
        {
            GameData GameData;
            if (GameDataGateway.MailData表.DataSheet.TryGetValue(邮件编号, out GameData))
            {
                MailData MailData = GameData as MailData;
                if (MailData != null)
                {
                    if (this.全部邮件.Contains(MailData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 != null)
                        {
                            网络连接.发送封包(new EmailDeletedOkPacket
                            {
                                邮件编号 = MailData.邮件编号
                            });
                        }
                        this.未读邮件.Remove(MailData);
                        this.全部邮件.Remove(MailData);
                        ItemData v = MailData.邮件附件.V;
                        if (v != null)
                        {
                            v.Delete();
                        }
                        MailData.Delete();
                        return;
                    }
                    客户网络 网络连接2 = this.网络连接;
                    if (网络连接2 == null)
                    {
                        return;
                    }
                    网络连接2.发送封包(new 社交错误提示
                    {
                        错误编号 = 6148
                    });
                    return;
                }
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 == null)
            {
                return;
            }
            网络连接3.发送封包(new 社交错误提示
            {
                错误编号 = 6148
            });
        }


        public void 提取邮件附件(int 邮件编号)
        {
            GameData GameData;
            if (GameDataGateway.MailData表.DataSheet.TryGetValue(邮件编号, out GameData))
            {
                MailData MailData = GameData as MailData;
                if (MailData != null)
                {
                    if (!this.全部邮件.Contains(MailData))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 6148
                        });
                        return;
                    }
                    else if (MailData.邮件附件.V == null)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 6150
                        });
                        return;
                    }
                    else
                    {
                        if (this.背包剩余 > 0)
                        {
                            int num = -1;
                            byte b = 0;
                            while (b < this.背包大小)
                            {
                                if (this.角色背包.ContainsKey(b))
                                {
                                    b += 1;
                                }
                                else
                                {
                                    num = (int)b;
                                IL_D1:
                                    if (num != -1)
                                    {
                                        客户网络 网络连接3 = this.网络连接;
                                        if (网络连接3 != null)
                                        {
                                            网络连接3.发送封包(new 成功提取附件
                                            {
                                                邮件编号 = MailData.邮件编号
                                            });
                                        }
                                        this.角色背包[(byte)num] = MailData.邮件附件.V;
                                        MailData.邮件附件.V.物品位置.V = (byte)num;
                                        MailData.邮件附件.V.物品容器.V = 1;
                                        MailData.邮件附件.V = null;
                                        return;
                                    }
                                    客户网络 网络连接4 = this.网络连接;
                                    if (网络连接4 == null)
                                    {
                                        return;
                                    }
                                    网络连接4.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 1793
                                    });
                                    return;
                                }
                            }
                            //goto IL_D1;
                        }
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 == null)
                        {
                            return;
                        }
                        网络连接5.发送封包(new 社交错误提示
                        {
                            错误编号 = 1793
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接6 = this.网络连接;
            if (网络连接6 == null)
            {
                return;
            }
            网络连接6.发送封包(new 社交错误提示
            {
                错误编号 = 6148
            });
        }


        public void 查询行会信息(int 行会编号)
        {
            GameData GameData;
            if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData))
            {
                GuildData GuildData = GameData as GuildData;
                if (GuildData != null)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new GuildNameAnswerPAcket
                    {
                        行会编号 = GuildData.数据索引.V,
                        行会名字 = GuildData.行会名字.V,
                        创建时间 = GuildData.CreatedDate.V,
                        会长编号 = GuildData.行会会长.V.数据索引.V,
                        行会人数 = (byte)GuildData.行会成员.Count,
                        行会等级 = GuildData.行会等级.V
                    });
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new 社交错误提示
            {
                错误编号 = 6669
            });
        }


        public void 更多行会信息()
        {
        }


        public void 更多GuildEvents()
        {
        }


        public void 查看行会列表(int 行会编号, byte 查看方式)
        {
            GameData value;
            int num = Math.Max(0, (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out value) && value is GuildData 行会数据) ? (行会数据.行会排名.V - 1) : 0);
            int num2 = ((查看方式 == 2) ? Math.Max(0, num) : Math.Max(0, num - 11));
            int num3 = Math.Min(12, SystemData.数据.行会人数排名.Count - num2);
            if (num3 > 0)
            {
                List<GuildData> range = SystemData.数据.行会人数排名.GetRange(num2, num3);
                using MemoryStream memoryStream = new MemoryStream();
                using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
                binaryWriter.Write(查看方式);
                binaryWriter.Write((byte)num3);
                foreach (GuildData item in range)
                {
                    binaryWriter.Write(item.行会检索描述());
                }
                网络连接?.发送封包(new 同步行会列表
                {
                    字节数据 = memoryStream.ToArray()
                });
                return;
            }
            using MemoryStream memoryStream2 = new MemoryStream();
            using BinaryWriter binaryWriter2 = new BinaryWriter(memoryStream2);
            binaryWriter2.Write(查看方式);
            binaryWriter2.Write((byte)0);
            网络连接?.发送封包(new 同步行会列表
            {
                字节数据 = memoryStream2.ToArray()
            });
        }

        public void FindCorrespondingGuildPacket(int 行会编号, string 行会名字)
        {
            GameData GameData;
            if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData) || GameDataGateway.GuildData表.Keyword.TryGetValue(行会名字, out GameData))
            {
                GuildData GuildData = GameData as GuildData;
                if (GuildData != null)
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new FindGuildAnswersPacket
                    {
                        字节数据 = GuildData.行会检索描述()
                    });
                    return;
                }
            }
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 == null)
            {
                return;
            }
            网络连接2.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6669
            });
        }


        public void 申请解散行会()
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] != GuildJobs.会长)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else if (this.所属行会.结盟行会.Count != 0)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6739
                });
                return;
            }
            else if (this.所属行会.结盟行会.Count != 0)
            {
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new 社交错误提示
                {
                    错误编号 = 6740
                });
                return;
            }
            else if (MapGatewayProcess.攻城行会.Contains(this.所属行会))
            {
                客户网络 网络连接5 = this.网络连接;
                if (网络连接5 == null)
                {
                    return;
                }
                网络连接5.发送封包(new 社交错误提示
                {
                    错误编号 = 6819
                });
                return;
            }
            else
            {
                if (this.所属行会 != SystemData.数据.占领行会.V)
                {
                    this.所属行会.解散行会();
                    return;
                }
                客户网络 网络连接6 = this.网络连接;
                if (网络连接6 == null)
                {
                    return;
                }
                网络连接6.发送封包(new 社交错误提示
                {
                    错误编号 = 6819
                });
                return;
            }
        }


        public void 申请创建行会(byte[] 数据)
        {
            if (this.打开界面 != "Guild")
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Request to create a guild. Error: Interface not opened."));
                return;
            }
            ItemData 当前物品;
            if (this.所属行会 != null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6707
                });
                return;
            }
            else if (this.当前等级 < 12)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6699
                });
                return;
            }
            else if (this.NumberGoldCoins < 200000)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6699
                });
                return;
            }
            else if (!this.查找背包物品(80002, out 当前物品))
            {
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6664
                });
                return;
            }
            else
            {
                if (数据.Length <= 25 || 数据.Length >= 128)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Request to create a guild. Error: Wrong data length."));
                    return;
                }
                string[] array = Encoding.UTF8.GetString(数据.Take(25).ToArray<byte>()).Split(new char[1], StringSplitOptions.RemoveEmptyEntries);
                string[] array2 = Encoding.UTF8.GetString(数据.Skip(25).ToArray<byte>()).Split(new char[1], StringSplitOptions.RemoveEmptyEntries);
                if (array.Length == 0 || array2.Length == 0 || Encoding.UTF8.GetBytes(array[0]).Length >= 25 || Encoding.UTF8.GetBytes(array2[0]).Length >= 101)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Request to create a guild. Error: Wrong character length."));
                    return;
                }
                if (!GameDataGateway.GuildData表.Keyword.ContainsKey(array[0]))
                {
                    this.NumberGoldCoins -= 200000;
                    this.ConsumeBackpackItem(1, 当前物品);
                    this.所属行会 = new GuildData(this, array[0], array2[0]);
                    客户网络 网络连接5 = this.网络连接;
                    if (网络连接5 != null)
                    {
                        网络连接5.发送封包(new 创建行会应答
                        {
                            行会名字 = this.所属行会.行会名字.V
                        });
                    }
                    客户网络 网络连接6 = this.网络连接;
                    if (网络连接6 != null)
                    {
                        网络连接6.发送封包(new GuildInfoAnnouncementPacket
                        {
                            字节数据 = this.所属行会.行会信息描述()
                        });
                    }
                    base.发送封包(new 同步对象行会
                    {
                        对象编号 = this.MapId,
                        行会编号 = this.所属行会.行会编号
                    });
                    NetworkServiceGateway.发送公告(string.Format("[{0}] created the guild [{1}]", this.对象名字, this.所属行会), false);
                    return;
                }
                客户网络 网络连接7 = this.网络连接;
                if (网络连接7 == null)
                {
                    return;
                }
                网络连接7.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6697
                });
                return;
            }
        }


        public void 更改行会公告(byte[] 数据)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] > GuildJobs.监事)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                if (数据.Length == 0 || 数据.Length >= 255)
                {
                    this.网络连接.尝试断开连接(new Exception("Error: Change of guild notice. Error: Incorrect data length"));
                    return;
                }
                if (数据[0] == 0)
                {
                    this.所属行会.更改公告("");
                    return;
                }
                this.所属行会.更改公告(Encoding.UTF8.GetString(数据).Split(new char[1])[0]);
                return;
            }
        }


        public void 更改行会宣言(byte[] 数据)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] > GuildJobs.监事)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                if (数据.Length == 0 || 数据.Length >= 101)
                {
                    this.网络连接.尝试断开连接(new Exception("Error: Change of guild notice. Error: Incorrect data length"));
                    return;
                }
                if (数据[0] == 0)
                {
                    this.所属行会.更改宣言(this.CharacterData, "");
                    return;
                }
                this.所属行会.更改宣言(this.CharacterData, Encoding.UTF8.GetString(数据).Split(new char[1])[0]);
                return;
            }
        }


        public void 处理入会邀请(int 对象编号, byte 处理类型)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (CharacterData.当前行会 != null && CharacterData.当前行会.邀请列表.Remove(this.CharacterData))
                    {
                        if (处理类型 == 2)
                        {
                            if (this.所属行会 != null)
                            {
                                客户网络 网络连接 = this.网络连接;
                                if (网络连接 == null)
                                {
                                    return;
                                }
                                网络连接.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 6707
                                });
                                return;
                            }
                            else
                            {
                                if (CharacterData.所属行会.V.行会成员.Count < 100)
                                {
                                    客户网络 网络连接2 = CharacterData.ActiveConnection;
                                    if (网络连接2 != null)
                                    {
                                        网络连接2.发送封包(new GuildInvitationAnswerPacket
                                        {
                                            对象名字 = this.对象名字,
                                            应答类型 = 1
                                        });
                                    }
                                    CharacterData.当前行会.添加成员(this.CharacterData, GuildJobs.会员);
                                    return;
                                }
                                客户网络 网络连接3 = this.网络连接;
                                if (网络连接3 == null)
                                {
                                    return;
                                }
                                网络连接3.发送封包(new 社交错误提示
                                {
                                    错误编号 = 6709
                                });
                                return;
                            }
                        }
                        else
                        {
                            客户网络 网络连接4 = CharacterData.ActiveConnection;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new GuildInvitationAnswerPacket
                            {
                                对象名字 = this.对象名字,
                                应答类型 = 2
                            });
                            return;
                        }
                    }
                    else
                    {
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 == null)
                        {
                            return;
                        }
                        网络连接5.发送封包(new 社交错误提示
                        {
                            错误编号 = 6731
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接6 = this.网络连接;
            if (网络连接6 == null)
            {
                return;
            }
            网络连接6.发送封包(new 社交错误提示
            {
                错误编号 = 6732
            });
        }


        public void 处理入会申请(int 对象编号, byte 处理类型)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if ((byte)this.所属行会.行会成员[this.CharacterData] >= 6)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (!this.所属行会.申请列表.Remove(CharacterData))
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 6731
                            });
                            return;
                        }
                        else
                        {
                            if (处理类型 != 2)
                            {
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 != null)
                                {
                                    网络连接4.发送封包(new 入会申请应答
                                    {
                                        对象编号 = CharacterData.角色编号
                                    });
                                }
                                CharacterData.发送邮件(new MailData(null, "Membership application rejected", "Guild [" + this.所属行会.行会名字.V + "] has rejected your membership application.", null));
                                return;
                            }
                            if (CharacterData.当前行会 != null)
                            {
                                客户网络 网络连接5 = this.网络连接;
                                if (网络连接5 == null)
                                {
                                    return;
                                }
                                网络连接5.发送封包(new GameErrorMessagePacket
                                {
                                    错误代码 = 6707
                                });
                                return;
                            }
                            else
                            {
                                this.所属行会.添加成员(CharacterData, GuildJobs.会员);
                                客户网络 网络连接6 = this.网络连接;
                                if (网络连接6 == null)
                                {
                                    return;
                                }
                                网络连接6.发送封包(new 入会申请应答
                                {
                                    对象编号 = CharacterData.角色编号
                                });
                                return;
                            }
                        }
                    }
                }
                客户网络 网络连接7 = this.网络连接;
                if (网络连接7 == null)
                {
                    return;
                }
                网络连接7.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 申请加入行会(int 行会编号, string 行会名字)
        {
            GameData GameData;
            if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData) || GameDataGateway.GuildData表.Keyword.TryGetValue(行会名字, out GameData))
            {
                GuildData GuildData = GameData as GuildData;
                if (GuildData != null)
                {
                    if (this.所属行会 != null)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 6707
                        });
                        return;
                    }
                    else if (this.当前等级 < 8)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 6714
                        });
                        return;
                    }
                    else if (GuildData.行会成员.Count >= 100)
                    {
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 6710
                        });
                        return;
                    }
                    else if (GuildData.申请列表.Count > 20)
                    {
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 6703
                        });
                        return;
                    }
                    else
                    {
                        GuildData.申请列表[this.CharacterData] = MainProcess.CurrentTime.AddHours(1.0);
                        GuildData.行会提醒(GuildJobs.执事, 1);
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 == null)
                        {
                            return;
                        }
                        网络连接5.发送封包(new 加入行会应答
                        {
                            行会编号 = GuildData.行会编号
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接6 = this.网络连接;
            if (网络连接6 == null)
            {
                return;
            }
            网络连接6.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 6669
            });
        }


        public void InviteToJoinGuildPacket(string 对象名字)
        {
            if (this.所属行会 != null)
            {
                foreach (KeyValuePair<CharacterData, DateTime> keyValuePair in this.所属行会.邀请列表.ToList<KeyValuePair<CharacterData, DateTime>>())
                {
                    if (MainProcess.CurrentTime > keyValuePair.Value)
                    {
                        this.所属行会.邀请列表.Remove(keyValuePair.Key);
                    }
                }
            }
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] == GuildJobs.会员)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else if (this.所属行会.行会成员.Count >= 100)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.Keyword.TryGetValue(对象名字, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        客户网络 客户网络;
                        if (!CharacterData.角色在线(out 客户网络))
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6711
                            });
                            return;
                        }
                        else if (CharacterData.当前行会 != null)
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6707
                            });
                            return;
                        }
                        else if (CharacterData.角色等级 < 8)
                        {
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6714
                            });
                            return;
                        }
                        else
                        {
                            this.所属行会.邀请列表[CharacterData] = MainProcess.CurrentTime.AddHours(1.0);
                            客户网络.发送封包(new InviteJoinPacket
                            {
                                对象编号 = this.MapId,
                                对象名字 = this.对象名字,
                                行会名字 = this.所属行会.行会名字.V
                            });
                            客户网络 网络连接7 = this.网络连接;
                            if (网络连接7 == null)
                            {
                                return;
                            }
                            网络连接7.发送封包(new 社交错误提示
                            {
                                错误编号 = 6713
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接8 = this.网络连接;
                if (网络连接8 == null)
                {
                    return;
                }
                网络连接8.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 查看申请列表()
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 查看申请名单
                {
                    字节描述 = this.所属行会.入会申请描述()
                });
                return;
            }
        }


        public void 申请离开行会()
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else
            {
                if (this.所属行会.行会成员[this.CharacterData] != GuildJobs.会长)
                {
                    this.所属行会.退出行会(this.CharacterData);
                    return;
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6718
                });
                return;
            }
        }


        public void DistributeGuildBenefitsPacket()
        {
        }


        public void ExpelMembersPacket(int 对象编号)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.MapId == 对象编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (this.所属行会 == CharacterData.当前行会)
                        {
                            if (this.所属行会.行会成员[this.CharacterData] < GuildJobs.长老 && this.所属行会.行会成员[this.CharacterData] < this.所属行会.行会成员[CharacterData])
                            {
                                this.所属行会.逐出成员(this.CharacterData, CharacterData);
                                CharacterData.发送邮件(new MailData(null, "You are kicked from the Guild", string.Concat(new string[]
                                {
                                    "You have been [",
                                    this.所属行会.行会名字.V,
                                    "]of officers[",
                                    this.对象名字,
                                    "]Expelled from the Guild."
                                }), null));
                                return;
                            }
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 6709
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void TransferPresidentPositionPacket(int 对象编号)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] != GuildJobs.会长)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6719
                });
                return;
            }
            else if (this.MapId == 对象编号)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6681
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (CharacterData.当前行会 == this.所属行会)
                        {
                            this.所属行会.转移会长(this.CharacterData, CharacterData);
                            return;
                        }
                    }
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void DonateGuildFundsPacket(int NumberGoldCoins)
        {
        }


        public void 设置行会禁言(int 对象编号, byte 禁言状态)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.MapId == 对象编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (CharacterData.当前行会 == this.所属行会)
                        {
                            if (this.所属行会.行会成员[this.CharacterData] < GuildJobs.理事 && this.所属行会.行会成员[this.CharacterData] < this.所属行会.行会成员[CharacterData])
                            {
                                this.所属行会.成员禁言(this.CharacterData, CharacterData, 禁言状态);
                                return;
                            }
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 6709
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 变更会员职位(int 对象编号, byte 对象职位)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.MapId == 对象编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6681
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
                {
                    CharacterData CharacterData = GameData as CharacterData;
                    if (CharacterData != null)
                    {
                        if (CharacterData.当前行会 == this.所属行会)
                        {
                            if (this.所属行会.行会成员[this.CharacterData] < GuildJobs.理事 && this.所属行会.行会成员[this.CharacterData] < this.所属行会.行会成员[CharacterData])
                            {
                                if (对象职位 > 1 && 对象职位 < 8)
                                {
                                    if (对象职位 != (byte)this.所属行会.行会成员[CharacterData])
                                    {
                                        if (对象职位 == 2)
                                        {
                                            if ((from O in this.所属行会.行会成员.Values
                                                 where O == GuildJobs.副长
                                                 select O).Count<GuildJobs>() >= 2)
                                            {
                                                客户网络 网络连接3 = this.网络连接;
                                                if (网络连接3 == null)
                                                {
                                                    return;
                                                }
                                                网络连接3.发送封包(new 社交错误提示
                                                {
                                                    错误编号 = 6717
                                                });
                                                return;
                                            }
                                        }
                                        if (对象职位 == 3)
                                        {
                                            if ((from O in this.所属行会.行会成员.Values
                                                 where O == GuildJobs.长老
                                                 select O).Count<GuildJobs>() >= 4)
                                            {
                                                客户网络 网络连接4 = this.网络连接;
                                                if (网络连接4 == null)
                                                {
                                                    return;
                                                }
                                                网络连接4.发送封包(new 社交错误提示
                                                {
                                                    错误编号 = 6717
                                                });
                                                return;
                                            }
                                        }
                                        if (对象职位 == 4)
                                        {
                                            if ((from O in this.所属行会.行会成员.Values
                                                 where O == GuildJobs.监事
                                                 select O).Count<GuildJobs>() >= 4)
                                            {
                                                客户网络 网络连接5 = this.网络连接;
                                                if (网络连接5 == null)
                                                {
                                                    return;
                                                }
                                                网络连接5.发送封包(new 社交错误提示
                                                {
                                                    错误编号 = 6717
                                                });
                                                return;
                                            }
                                        }
                                        if (对象职位 == 5)
                                        {
                                            if ((from O in this.所属行会.行会成员.Values
                                                 where O == GuildJobs.理事
                                                 select O).Count<GuildJobs>() >= 4)
                                            {
                                                客户网络 网络连接6 = this.网络连接;
                                                if (网络连接6 == null)
                                                {
                                                    return;
                                                }
                                                网络连接6.发送封包(new 社交错误提示
                                                {
                                                    错误编号 = 6717
                                                });
                                                return;
                                            }
                                        }
                                        if (对象职位 == 6)
                                        {
                                            if ((from O in this.所属行会.行会成员.Values
                                                 where O == GuildJobs.执事
                                                 select O).Count<GuildJobs>() >= 4)
                                            {
                                                客户网络 网络连接7 = this.网络连接;
                                                if (网络连接7 == null)
                                                {
                                                    return;
                                                }
                                                网络连接7.发送封包(new 社交错误提示
                                                {
                                                    错误编号 = 6717
                                                });
                                                return;
                                            }
                                        }
                                        this.所属行会.更改职位(this.CharacterData, CharacterData, (GuildJobs)对象职位);
                                        return;
                                    }
                                }
                                客户网络 网络连接8 = this.网络连接;
                                if (网络连接8 == null)
                                {
                                    return;
                                }
                                网络连接8.发送封包(new 社交错误提示
                                {
                                    错误编号 = 6704
                                });
                                return;
                            }
                            else
                            {
                                客户网络 网络连接9 = this.网络连接;
                                if (网络连接9 == null)
                                {
                                    return;
                                }
                                网络连接9.发送封包(new 社交错误提示
                                {
                                    错误编号 = 6709
                                });
                                return;
                            }
                        }
                    }
                }
                客户网络 网络连接10 = this.网络连接;
                if (网络连接10 == null)
                {
                    return;
                }
                网络连接10.发送封包(new 社交错误提示
                {
                    错误编号 = 6732
                });
                return;
            }
        }


        public void 申请行会外交(byte 外交类型, byte 外交时间, string 行会名字)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会名字.V == 行会名字)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6694
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] >= GuildJobs.长老)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.GuildData表.Keyword.TryGetValue(行会名字, out GameData))
                {
                    GuildData GuildData = GameData as GuildData;
                    if (GuildData != null)
                    {
                        if (this.所属行会.结盟行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 6727
                            });
                            return;
                        }
                        else if (this.所属行会.Hostility行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new 社交错误提示
                            {
                                错误编号 = 6726
                            });
                            return;
                        }
                        else
                        {
                            if (外交时间 < 1 || 外交时间 > 3)
                            {
                                this.网络连接.尝试断开连接(new Exception("Wrong action: Application for Guild Diplomacy.  Error: Wrong time parameter"));
                                return;
                            }
                            if (外交类型 == 1)
                            {
                                if (this.所属行会.结盟行会.Count >= 10)
                                {
                                    客户网络 网络连接6 = this.网络连接;
                                    if (网络连接6 == null)
                                    {
                                        return;
                                    }
                                    网络连接6.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 6668
                                    });
                                    return;
                                }
                                else
                                {
                                    if (GuildData.结盟行会.Count < 10)
                                    {
                                        this.所属行会.申请结盟(this.CharacterData, GuildData, 外交时间);
                                        return;
                                    }
                                    客户网络 网络连接7 = this.网络连接;
                                    if (网络连接7 == null)
                                    {
                                        return;
                                    }
                                    网络连接7.发送封包(new 社交错误提示
                                    {
                                        错误编号 = 6668
                                    });
                                    return;
                                }
                            }
                            else
                            {
                                if (外交类型 == 2)
                                {
                                    this.所属行会.行会Hostility(GuildData, 外交时间);
                                    NetworkServiceGateway.发送公告(string.Format("[{0}] and [{1}] become enemy guilds.", this.所属行会, GuildData), false);
                                    return;
                                }
                                this.网络连接.尝试断开连接(new Exception("Wrong action: Application for Guild Diplomacy.  Error: Wrong type parameter"));
                                return;
                            }
                        }
                    }
                }
                客户网络 网络连接8 = this.网络连接;
                if (网络连接8 == null)
                {
                    return;
                }
                网络连接8.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6669
                });
                return;
            }
        }


        public void 申请行会Hostility(byte Hostility时间, string 行会名字)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会名字.V == 行会名字)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6694
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] >= GuildJobs.长老)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.GuildData表.Keyword.TryGetValue(行会名字, out GameData))
                {
                    GuildData GuildData = GameData as GuildData;
                    if (GuildData != null)
                    {
                        if (this.所属行会.结盟行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 6727
                            });
                            return;
                        }
                        else if (this.所属行会.Hostility行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new 社交错误提示
                            {
                                错误编号 = 6726
                            });
                            return;
                        }
                        else
                        {
                            if (Hostility时间 >= 1 && Hostility时间 <= 3)
                            {
                                this.所属行会.行会Hostility(GuildData, Hostility时间);
                                NetworkServiceGateway.发送公告(string.Format("[{0}] and [{1}] have become rival guilds.", this.所属行会, GuildData), false);
                                return;
                            }
                            this.网络连接.尝试断开连接(new Exception("Wrong action: Application for guild hostility.  Error: Wrong time parameter"));
                            return;
                        }
                    }
                }
                客户网络 网络连接6 = this.网络连接;
                if (网络连接6 == null)
                {
                    return;
                }
                网络连接6.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6669
                });
                return;
            }
        }


        public void 查看结盟申请()
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 同步结盟申请
                {
                    字节描述 = this.所属行会.结盟申请描述()
                });
                return;
            }
        }


        public void 处理结盟申请(byte 处理类型, int 行会编号)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会编号 == 行会编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6694
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] >= GuildJobs.长老)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData))
                {
                    GuildData GuildData = GameData as GuildData;
                    if (GuildData != null)
                    {
                        if (this.所属行会.结盟行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6727
                            });
                            return;
                        }
                        else if (this.所属行会.Hostility行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new 社交错误提示
                            {
                                错误编号 = 6726
                            });
                            return;
                        }
                        else if (!this.所属行会.结盟申请.ContainsKey(GuildData))
                        {
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new 社交错误提示
                            {
                                错误编号 = 6695
                            });
                            return;
                        }
                        else
                        {
                            if (处理类型 == 1)
                            {
                                客户网络 网络连接7 = this.网络连接;
                                if (网络连接7 != null)
                                {
                                    网络连接7.发送封包(new AffiliateAppResponsePacket
                                    {
                                        行会编号 = GuildData.行会编号
                                    });
                                }
                                GuildData.发送邮件(GuildJobs.副长, "Alliance request rejected", "Guild[" + this.所属行会.行会名字.V + "]has denied your guild's request for an alliance.");
                                this.所属行会.结盟申请.Remove(GuildData);
                                return;
                            }
                            if (处理类型 == 2)
                            {
                                this.所属行会.行会结盟(GuildData);
                                NetworkServiceGateway.发送公告(string.Format("[{0}] and [{1}] become allied guilds.", this.所属行会, GuildData), false);
                                this.所属行会.结盟申请.Remove(GuildData);
                                return;
                            }
                            this.网络连接.尝试断开连接(new Exception("Wrong action: Processing of an alliance request.  Error: Wrong type of processing."));
                            return;
                        }
                    }
                }
                客户网络 网络连接8 = this.网络连接;
                if (网络连接8 == null)
                {
                    return;
                }
                网络连接8.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6669
                });
                return;
            }
        }


        public void 申请解除结盟(int 行会编号)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会编号 == 行会编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6694
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] >= GuildJobs.长老)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData))
                {
                    GuildData GuildData = GameData as GuildData;
                    if (GuildData != null)
                    {
                        if (this.所属行会.结盟行会.ContainsKey(GuildData))
                        {
                            this.所属行会.解除结盟(this.CharacterData, GuildData);
                            NetworkServiceGateway.发送公告(string.Format("[{0}] has dissolved the guild alliance with [{1}].", this.所属行会, GuildData), false);
                            return;
                        }
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 6728
                        });
                        return;
                    }
                }
                客户网络 网络连接5 = this.网络连接;
                if (网络连接5 == null)
                {
                    return;
                }
                网络连接5.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6669
                });
                return;
            }
        }


        public void 申请解除Hostility(int 行会编号)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会编号 == 行会编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6694
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] >= GuildJobs.长老)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData))
                {
                    GuildData GuildData = GameData as GuildData;
                    if (GuildData != null)
                    {
                        if (!this.所属行会.Hostility行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6826
                            });
                            return;
                        }
                        else
                        {
                            if (!GuildData.解除申请.ContainsKey(this.所属行会))
                            {
                                this.所属行会.申请解敌(this.CharacterData, GuildData);
                                return;
                            }
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6708
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接6 = this.网络连接;
                if (网络连接6 == null)
                {
                    return;
                }
                网络连接6.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6669
                });
                return;
            }
        }


        public void 处理解除申请(int 行会编号, byte 应答类型)
        {
            if (this.所属行会 == null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new 社交错误提示
                {
                    错误编号 = 6668
                });
                return;
            }
            else if (this.所属行会.行会编号 == 行会编号)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new 社交错误提示
                {
                    错误编号 = 6694
                });
                return;
            }
            else if (this.所属行会.行会成员[this.CharacterData] >= GuildJobs.长老)
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new 社交错误提示
                {
                    错误编号 = 6709
                });
                return;
            }
            else
            {
                GameData GameData;
                if (GameDataGateway.GuildData表.DataSheet.TryGetValue(行会编号, out GameData))
                {
                    GuildData GuildData = GameData as GuildData;
                    if (GuildData != null)
                    {
                        if (!this.所属行会.Hostility行会.ContainsKey(GuildData))
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6826
                            });
                            return;
                        }
                        else if (!this.所属行会.解除申请.ContainsKey(GuildData))
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 5899
                            });
                            return;
                        }
                        else
                        {
                            if (应答类型 != 2)
                            {
                                this.所属行会.发送封包(new DisarmHostileListPacket
                                {
                                    申请类型 = 2,
                                    行会编号 = GuildData.行会编号
                                });
                                this.所属行会.解除申请.Remove(GuildData);
                                return;
                            }
                            if (MapGatewayProcess.沙城节点 < 2 || ((this.所属行会 != SystemData.数据.占领行会.V || !MapGatewayProcess.攻城行会.Contains(GuildData)) && (GuildData != SystemData.数据.占领行会.V || !MapGatewayProcess.攻城行会.Contains(this.所属行会))))
                            {
                                this.所属行会.解除Hostility(GuildData);
                                NetworkServiceGateway.发送公告(string.Format("[{0}] has released the guild from hostilities with [{1}].", this.所属行会, GuildData), false);
                                this.所属行会.解除申请.Remove(GuildData);
                                return;
                            }
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 6800
                            });
                            return;
                        }
                    }
                }
                客户网络 网络连接7 = this.网络连接;
                if (网络连接7 == null)
                {
                    return;
                }
                网络连接7.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 6669
                });
                return;
            }
        }


        public void 查询师门成员()
        {
            if (this.所属师门 != null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new SyncGuildMemberPacket
                {
                    字节数据 = this.所属师门.成员数据()
                });
            }
        }


        public void 查询师门奖励()
        {
            if (this.所属师门 != null)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new SyncMasterRewardPacket
                {
                    字节数据 = this.所属师门.奖励数据(this.CharacterData)
                });
            }
        }


        public void 查询拜师名册()
        {
        }


        public void 查询收徒名册()
        {
        }


        public void 玩家申请拜师(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.所属师门 != null)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5895
                        });
                        return;
                    }
                    else if (this.当前等级 >= 30)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 5915
                        });
                        return;
                    }
                    else if (CharacterData.角色等级 < 30)
                    {
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new 社交错误提示
                        {
                            错误编号 = 5894
                        });
                        return;
                    }
                    else if (CharacterData.当前师门 != null && CharacterData.角色编号 != CharacterData.当前师门.师父编号)
                    {
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new 社交错误提示
                        {
                            错误编号 = 5890
                        });
                        return;
                    }
                    else if (CharacterData.当前师门 != null && CharacterData.当前师门.徒弟数量 >= 3)
                    {
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 == null)
                        {
                            return;
                        }
                        网络连接5.发送封包(new 社交错误提示
                        {
                            错误编号 = 5891
                        });
                        return;
                    }
                    else
                    {
                        客户网络 客户网络;
                        if (CharacterData.角色在线(out 客户网络))
                        {
                            if (CharacterData.当前师门 == null)
                            {
                                CharacterData.当前师门 = new TeacherData(CharacterData);
                            }
                            CharacterData.当前师门.申请列表[this.MapId] = MainProcess.CurrentTime;
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 != null)
                            {
                                网络连接6.发送封包(new 申请拜师应答
                                {
                                    对象编号 = CharacterData.角色编号
                                });
                            }
                            客户网络.发送封包(new 申请拜师提示
                            {
                                对象编号 = this.MapId
                            });
                            return;
                        }
                        客户网络 网络连接7 = this.网络连接;
                        if (网络连接7 == null)
                        {
                            return;
                        }
                        网络连接7.发送封包(new 社交错误提示
                        {
                            错误编号 = 5892
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接8 = this.网络连接;
            if (网络连接8 == null)
            {
                return;
            }
            网络连接8.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void 同意拜师申请(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.当前等级 < 30)
                    {
                        this.网络连接.尝试断开连接(new Exception("Mistake: Agreeing to the application, Error: Insufficient level."));
                        return;
                    }
                    if (CharacterData.角色等级 >= 30)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5894
                        });
                        return;
                    }
                    else if (CharacterData.当前师门 != null)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 5895
                        });
                        return;
                    }
                    else
                    {
                        if (this.所属师门 == null)
                        {
                            this.网络连接.尝试断开连接(new Exception("Wrong action: Agree to the application, Error: No master has been created."));
                            return;
                        }
                        if (this.所属师门.师父编号 != this.MapId)
                        {
                            this.网络连接.尝试断开连接(new Exception("Wrong action: Agree to apply for a teacher, Error: Not yet a teacher myself."));
                            return;
                        }
                        if (!this.所属师门.申请列表.ContainsKey(CharacterData.角色编号))
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 5898
                            });
                            return;
                        }
                        else if (this.所属师门.徒弟数量 >= 3)
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 5891
                            });
                            return;
                        }
                        else
                        {
                            客户网络 客户网络;
                            if (CharacterData.角色在线(out 客户网络))
                            {
                                if (this.所属师门 == null)
                                {
                                    this.所属师门 = new TeacherData(this.CharacterData);
                                }
                                this.所属师门.添加徒弟(CharacterData);
                                this.所属师门.发送封包(new 收徒成功提示
                                {
                                    对象编号 = CharacterData.角色编号
                                });
                                客户网络 网络连接5 = this.网络连接;
                                if (网络连接5 != null)
                                {
                                    网络连接5.发送封包(new 拜师申请通过
                                    {
                                        对象编号 = CharacterData.角色编号
                                    });
                                }
                                客户网络 网络连接6 = this.网络连接;
                                if (网络连接6 != null)
                                {
                                    网络连接6.发送封包(new SyncGuildMemberPacket
                                    {
                                        字节数据 = this.所属师门.成员数据()
                                    });
                                }
                                客户网络.发送封包(new SyncGuildMemberPacket
                                {
                                    字节数据 = this.所属师门.成员数据()
                                });
                                客户网络.发送封包(new SyncTeacherInfoPacket
                                {
                                    师门参数 = 1
                                });
                                return;
                            }
                            客户网络 网络连接7 = this.网络连接;
                            if (网络连接7 == null)
                            {
                                return;
                            }
                            网络连接7.发送封包(new 社交错误提示
                            {
                                错误编号 = 5893
                            });
                            return;
                        }
                    }
                }
            }
            客户网络 网络连接8 = this.网络连接;
            if (网络连接8 == null)
            {
                return;
            }
            网络连接8.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void RefusedApplyApprenticeshipPacket(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.所属师门 == null)
                    {
                        this.网络连接.尝试断开连接(new Exception("Wrong action: RefusedApplyApprenticeshipPacket, Error: Division not yet created."));
                        return;
                    }
                    if (this.所属师门.师父编号 != this.MapId)
                    {
                        this.网络连接.尝试断开连接(new Exception("Wrong operation: RefusedApplyApprenticeshipPacket, Error: Self not yet mastered."));
                        return;
                    }
                    if (!this.所属师门.申请列表.ContainsKey(CharacterData.角色编号))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5898
                        });
                        return;
                    }
                    else
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new 拜师申请拒绝
                            {
                                对象编号 = CharacterData.角色编号
                            });
                        }
                        if (!this.所属师门.申请列表.Remove(CharacterData.角色编号))
                        {
                            return;
                        }
                        客户网络 网络连接3 = CharacterData.ActiveConnection;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new RefusalApprenticePacket
                        {
                            对象编号 = this.MapId
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接4 = this.网络连接;
            if (网络连接4 == null)
            {
                return;
            }
            网络连接4.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void 玩家申请收徒(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.当前等级 < 30)
                    {
                        this.网络连接.尝试断开连接(new Exception("Error: Player applied for an apprentice, Error: Insufficient level."));
                        return;
                    }
                    if (CharacterData.角色等级 >= 30)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5894
                        });
                        return;
                    }
                    else if (CharacterData.当前师门 != null)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 5895
                        });
                        return;
                    }
                    else
                    {
                        if (this.所属师门 != null && this.所属师门.师父编号 != this.MapId)
                        {
                            this.网络连接.尝试断开连接(new Exception("Error: The player has applied for an apprenticeship, Error: He is not yet a master."));
                            return;
                        }
                        if (this.所属师门 != null && this.所属师门.徒弟数量 >= 3)
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 5891
                            });
                            return;
                        }
                        else
                        {
                            客户网络 客户网络;
                            if (CharacterData.角色在线(out 客户网络))
                            {
                                if (this.所属师门 == null)
                                {
                                    this.所属师门 = new TeacherData(this.CharacterData);
                                }
                                this.所属师门.邀请列表[CharacterData.角色编号] = MainProcess.CurrentTime;
                                客户网络 网络连接4 = this.网络连接;
                                if (网络连接4 != null)
                                {
                                    网络连接4.发送封包(new 申请收徒应答
                                    {
                                        对象编号 = CharacterData.角色编号
                                    });
                                }
                                客户网络.发送封包(new 申请收徒提示
                                {
                                    对象编号 = this.MapId,
                                    对象等级 = this.当前等级,
                                    对象声望 = this.师门声望
                                });
                                return;
                            }
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 == null)
                            {
                                return;
                            }
                            网络连接5.发送封包(new 社交错误提示
                            {
                                错误编号 = 5893
                            });
                            return;
                        }
                    }
                }
            }
            客户网络 网络连接6 = this.网络连接;
            if (网络连接6 == null)
            {
                return;
            }
            网络连接6.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void 同意收徒申请(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (this.当前等级 > 30)
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5915
                        });
                        return;
                    }
                    else if (this.所属师门 != null)
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new 社交错误提示
                        {
                            错误编号 = 5895
                        });
                        return;
                    }
                    else
                    {
                        if (CharacterData.角色等级 < 30)
                        {
                            this.网络连接.尝试断开连接(new Exception("Incorrect action: Agree to apprentice application, Error: The other party is not of sufficient level."));
                            return;
                        }
                        if (CharacterData.当前师门 == null)
                        {
                            this.网络连接.尝试断开连接(new Exception("Wrong action: Agree to apprentice application, Error: The other party does not have a master."));
                            return;
                        }
                        if (CharacterData.当前师门.师父编号 != CharacterData.角色编号)
                        {
                            this.网络连接.尝试断开连接(new Exception("Wrong action: Agree to apprentice application, Error: The other party is not yet a student."));
                            return;
                        }
                        客户网络 客户网络;
                        if (!CharacterData.当前师门.邀请列表.ContainsKey(this.MapId))
                        {
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new 社交错误提示
                            {
                                错误编号 = 5899
                            });
                            return;
                        }
                        else if (CharacterData.当前师门.徒弟数量 >= 3)
                        {
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new 社交错误提示
                            {
                                错误编号 = 5891
                            });
                            return;
                        }
                        else if (CharacterData.角色在线(out 客户网络))
                        {
                            客户网络 网络连接5 = this.网络连接;
                            if (网络连接5 != null)
                            {
                                网络连接5.发送封包(new 收徒申请同意
                                {
                                    对象编号 = CharacterData.角色编号
                                });
                            }
                            if (CharacterData.当前师门 == null)
                            {
                                CharacterData.当前师门 = new TeacherData(CharacterData);
                            }
                            客户网络.发送封包(new 收徒成功提示
                            {
                                对象编号 = this.MapId
                            });
                            CharacterData.当前师门.发送封包(new 收徒成功提示
                            {
                                对象编号 = this.MapId
                            });
                            CharacterData.当前师门.添加徒弟(this.CharacterData);
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 != null)
                            {
                                网络连接6.发送封包(new SyncGuildMemberPacket
                                {
                                    字节数据 = CharacterData.当前师门.成员数据()
                                });
                            }
                            客户网络 网络连接7 = this.网络连接;
                            if (网络连接7 == null)
                            {
                                return;
                            }
                            网络连接7.发送封包(new SyncTeacherInfoPacket
                            {
                                师门参数 = 1
                            });
                            return;
                        }
                        else
                        {
                            客户网络 网络连接8 = this.网络连接;
                            if (网络连接8 == null)
                            {
                                return;
                            }
                            网络连接8.发送封包(new 社交错误提示
                            {
                                错误编号 = 5892
                            });
                            return;
                        }
                    }
                }
            }
            客户网络 网络连接9 = this.网络连接;
            if (网络连接9 == null)
            {
                return;
            }
            网络连接9.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void RejectionApprenticeshipAppPacket(int 对象编号)
        {
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null)
                {
                    if (CharacterData.所属师门 == null)
                    {
                        this.网络连接.尝试断开连接(new Exception("Wrong action: RejectionApprenticeshipAppPacket, Error: Division not yet created."));
                        return;
                    }
                    if (CharacterData.当前师门.师父编号 != CharacterData.角色编号)
                    {
                        this.网络连接.尝试断开连接(new Exception("Wrong operation: RefusedApplyApprenticeshipPacket, Error: Self not yet mastered."));
                        return;
                    }
                    if (!CharacterData.当前师门.邀请列表.ContainsKey(this.MapId))
                    {
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new 社交错误提示
                        {
                            错误编号 = 5899
                        });
                        return;
                    }
                    else
                    {
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 != null)
                        {
                            网络连接2.发送封包(new 收徒申请拒绝
                            {
                                对象编号 = CharacterData.角色编号
                            });
                        }
                        if (!CharacterData.当前师门.邀请列表.Remove(this.MapId))
                        {
                            return;
                        }
                        客户网络 网络连接3 = CharacterData.ActiveConnection;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new RejectionTipsPacket
                        {
                            对象编号 = this.MapId
                        });
                        return;
                    }
                }
            }
            客户网络 网络连接4 = this.网络连接;
            if (网络连接4 == null)
            {
                return;
            }
            网络连接4.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void AppForExpulsionPacket(int 对象编号)
        {
            if (this.所属师门 == null)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong operation: AppForExpulsionPacket, Error: Self does not have a division."));
                return;
            }
            if (this.所属师门.师父编号 != this.MapId)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: AppForExpulsionPacket, Error: Not a Master."));
                return;
            }
            GameData GameData;
            if (GameDataGateway.CharacterDataTable.DataSheet.TryGetValue(对象编号, out GameData))
            {
                CharacterData CharacterData = GameData as CharacterData;
                if (CharacterData != null && this.所属师门.师门成员.Contains(CharacterData))
                {
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 != null)
                    {
                        网络连接.发送封包(new ExpulsionDoorAnswerPacket
                        {
                            对象编号 = CharacterData.角色编号
                        });
                    }
                    this.所属师门.发送封包(new ExpulsionDivisionDoorPacket
                    {
                        对象编号 = CharacterData.角色编号
                    });
                    int num = this.所属师门.徒弟出师金币(CharacterData);
                    int num2 = this.所属师门.徒弟出师经验(CharacterData);
                    PlayerObject PlayerObject;
                    if (MapGatewayProcess.玩家对象表.TryGetValue(CharacterData.角色编号, out PlayerObject))
                    {
                        PlayerObject.NumberGoldCoins += num;
                        PlayerObject.玩家增加经验(null, num2);
                    }
                    else
                    {
                        CharacterData.获得经验(num2);
                        CharacterData.NumberGoldCoins += num;
                    }
                    this.所属师门.移除徒弟(CharacterData);
                    CharacterData.当前师门 = null;
                    客户网络 网络连接2 = CharacterData.ActiveConnection;
                    if (网络连接2 != null)
                    {
                        网络连接2.发送封包(new SyncTeacherInfoPacket
                        {
                            师门参数 = ((byte)((CharacterData.角色等级 < 30) ? 0 : 2))
                        });
                    }
                    CharacterData.发送邮件(new MailData(null, "You have been kicked from the school", "You have been[" + this.对象名字 + "]Expelled from the division.", null));
                    return;
                }
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 == null)
            {
                return;
            }
            网络连接3.发送封包(new 社交错误提示
            {
                错误编号 = 5913
            });
        }


        public void 离开师门申请()
        {
            if (this.所属师门 == null)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Request to leave a division, Error: No division."));
                return;
            }
            if (!this.所属师门.师门成员.Contains(this.CharacterData))
            {
                this.网络连接.尝试断开连接(new Exception("Incorrect action: Request to leave the division, Error: Self is not a disciple."));
                return;
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 != null)
            {
                网络连接.发送封包(new 离开师门应答());
            }
            客户网络 网络连接2 = this.所属师门.师父数据.ActiveConnection;
            if (网络连接2 != null)
            {
                网络连接2.发送封包(new 离开师门提示
                {
                    对象编号 = this.MapId
                });
            }
            this.所属师门.发送封包(new 离开师门提示
            {
                对象编号 = this.MapId
            });
            this.所属师门.师父数据.发送邮件(new MailData(null, "The disciple's rebellion against his master", "Your apprentice[" + this.对象名字 + "]Has defected from the school.", null));
            int num = this.所属师门.徒弟提供金币(this.CharacterData);
            int num2 = this.所属师门.徒弟提供声望(this.CharacterData);
            int num3 = this.所属师门.徒弟提供金币(this.CharacterData);
            PlayerObject PlayerObject;
            if (MapGatewayProcess.玩家对象表.TryGetValue(this.所属师门.师父数据.角色编号, out PlayerObject))
            {
                PlayerObject.NumberGoldCoins += num;
                PlayerObject.师门声望 += num2;
                PlayerObject.玩家增加经验(null, num3);
            }
            else
            {
                this.所属师门.师父数据.获得经验(num3);
                this.所属师门.师父数据.NumberGoldCoins += num;
                this.所属师门.师父数据.师门声望 += num2;
            }
            this.所属师门.移除徒弟(this.CharacterData);
            this.CharacterData.当前师门 = null;
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 == null)
            {
                return;
            }
            网络连接3.发送封包(new SyncTeacherInfoPacket
            {
                师门参数 = this.师门参数
            });
        }


        public void 提交出师申请()
        {
            if (this.所属师门 == null)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Submit a request to leave the division, Error: No division."));
                return;
            }
            if (this.当前等级 < 30)
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Submit a request to become a teacher, Error: Insufficient level."));
                return;
            }
            if (!this.所属师门.师门成员.Contains(this.CharacterData))
            {
                this.网络连接.尝试断开连接(new Exception("Wrong action: Submit a request to become a disciple, Error: You are not a disciple."));
                return;
            }
            int num = this.所属师门.徒弟提供金币(this.CharacterData);
            int num2 = this.所属师门.徒弟提供声望(this.CharacterData);
            int num3 = this.所属师门.徒弟提供金币(this.CharacterData);
            PlayerObject PlayerObject;
            if (MapGatewayProcess.玩家对象表.TryGetValue(this.所属师门.师父数据.角色编号, out PlayerObject))
            {
                PlayerObject.NumberGoldCoins += num;
                PlayerObject.师门声望 += num2;
                PlayerObject.玩家增加经验(null, num3);
            }
            else
            {
                this.所属师门.师父数据.获得经验(num3);
                this.所属师门.师父数据.NumberGoldCoins += num;
                this.所属师门.师父数据.师门声望 += num2;
            }
            this.NumberGoldCoins += this.所属师门.徒弟出师金币(this.CharacterData);
            this.玩家增加经验(null, this.所属师门.徒弟出师经验(this.CharacterData));
            客户网络 网络连接 = this.所属师门.师父数据.ActiveConnection;
            if (网络连接 != null)
            {
                网络连接.发送封包(new ApprenticeSuccessfullyPacket
                {
                    对象编号 = this.MapId
                });
            }
            this.所属师门.移除徒弟(this.CharacterData);
            this.CharacterData.当前师门 = null;
            客户网络 网络连接2 = this.网络连接;
            if (网络连接2 != null)
            {
                网络连接2.发送封包(new ApprenticeSuccessfullyPacket
                {
                    对象编号 = this.MapId
                });
            }
            客户网络 网络连接3 = this.网络连接;
            if (网络连接3 != null)
            {
                网络连接3.发送封包(new ClearGuildInfoPacket());
            }
            客户网络 网络连接4 = this.网络连接;
            if (网络连接4 == null)
            {
                return;
            }
            网络连接4.发送封包(new SyncTeacherInfoPacket
            {
                师门参数 = this.师门参数
            });
        }


        public void 更改收徒推送(bool 收徒推送)
        {
        }


        public void 玩家申请交易(int 对象编号)
        {
            if (!this.对象死亡 && this.摆摊状态 <= 0 && this.交易状态 < 3)
            {
                if (this.当前等级 < 30 && this.本期特权 == 0)
                {
                    PlayerDeals PlayerDeals = this.当前交易;
                    if (PlayerDeals != null)
                    {
                        PlayerDeals.结束交易();
                    }
                    客户网络 网络连接 = this.网络连接;
                    if (网络连接 == null)
                    {
                        return;
                    }
                    网络连接.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 65538
                    });
                    return;
                }
                else
                {
                    if (对象编号 == this.MapId)
                    {
                        PlayerDeals PlayerDeals2 = this.当前交易;
                        if (PlayerDeals2 != null)
                        {
                            PlayerDeals2.结束交易();
                        }
                        this.网络连接.尝试断开连接(new Exception("Error: Player requested a trade. Error: Cannot trade myself"));
                        return;
                    }
                    PlayerObject PlayerObject;
                    if (!MapGatewayProcess.玩家对象表.TryGetValue(对象编号, out PlayerObject))
                    {
                        PlayerDeals PlayerDeals3 = this.当前交易;
                        if (PlayerDeals3 != null)
                        {
                            PlayerDeals3.结束交易();
                        }
                        客户网络 网络连接2 = this.网络连接;
                        if (网络连接2 == null)
                        {
                            return;
                        }
                        网络连接2.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 5635
                        });
                        return;
                    }
                    else if (this.当前地图 != PlayerObject.当前地图)
                    {
                        PlayerDeals PlayerDeals4 = this.当前交易;
                        if (PlayerDeals4 != null)
                        {
                            PlayerDeals4.结束交易();
                        }
                        客户网络 网络连接3 = this.网络连接;
                        if (网络连接3 == null)
                        {
                            return;
                        }
                        网络连接3.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 5636
                        });
                        return;
                    }
                    else if (base.网格距离(PlayerObject) > 12)
                    {
                        PlayerDeals PlayerDeals5 = this.当前交易;
                        if (PlayerDeals5 != null)
                        {
                            PlayerDeals5.结束交易();
                        }
                        客户网络 网络连接4 = this.网络连接;
                        if (网络连接4 == null)
                        {
                            return;
                        }
                        网络连接4.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 5636
                        });
                        return;
                    }
                    else if (!PlayerObject.对象死亡 && PlayerObject.摆摊状态 == 0 && PlayerObject.交易状态 < 3)
                    {
                        PlayerDeals PlayerDeals6 = this.当前交易;
                        if (PlayerDeals6 != null)
                        {
                            PlayerDeals6.结束交易();
                        }
                        PlayerDeals PlayerDeals7 = PlayerObject.当前交易;
                        if (PlayerDeals7 != null)
                        {
                            PlayerDeals7.结束交易();
                        }
                        this.当前交易 = (PlayerObject.当前交易 = new PlayerDeals(this, PlayerObject));
                        客户网络 网络连接5 = this.网络连接;
                        if (网络连接5 == null)
                        {
                            return;
                        }
                        网络连接5.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 5633
                        });
                        return;
                    }
                    else
                    {
                        PlayerDeals PlayerDeals8 = this.当前交易;
                        if (PlayerDeals8 != null)
                        {
                            PlayerDeals8.结束交易();
                        }
                        客户网络 网络连接6 = this.网络连接;
                        if (网络连接6 == null)
                        {
                            return;
                        }
                        网络连接6.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 5637
                        });
                        return;
                    }
                }
            }
            else
            {
                PlayerDeals PlayerDeals9 = this.当前交易;
                if (PlayerDeals9 != null)
                {
                    PlayerDeals9.结束交易();
                }
                客户网络 网络连接7 = this.网络连接;
                if (网络连接7 == null)
                {
                    return;
                }
                网络连接7.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5634
                });
                return;
            }
        }


        public void 玩家同意交易(int 对象编号)
        {
            if (!this.对象死亡 && this.摆摊状态 == 0)
            {
                if (this.交易状态 == 2)
                {
                    if (this.当前等级 < 30 && this.本期特权 == 0)
                    {
                        PlayerDeals PlayerDeals = this.当前交易;
                        if (PlayerDeals != null)
                        {
                            PlayerDeals.结束交易();
                        }
                        客户网络 网络连接 = this.网络连接;
                        if (网络连接 == null)
                        {
                            return;
                        }
                        网络连接.发送封包(new GameErrorMessagePacket
                        {
                            错误代码 = 65538
                        });
                        return;
                    }
                    else
                    {
                        if (对象编号 == this.MapId)
                        {
                            PlayerDeals PlayerDeals2 = this.当前交易;
                            if (PlayerDeals2 != null)
                            {
                                PlayerDeals2.结束交易();
                            }
                            this.网络连接.尝试断开连接(new Exception("Error: Player requested a trade. Error: Cannot trade myself"));
                            return;
                        }
                        PlayerObject PlayerObject;
                        if (!MapGatewayProcess.玩家对象表.TryGetValue(对象编号, out PlayerObject))
                        {
                            PlayerDeals PlayerDeals3 = this.当前交易;
                            if (PlayerDeals3 != null)
                            {
                                PlayerDeals3.结束交易();
                            }
                            客户网络 网络连接2 = this.网络连接;
                            if (网络连接2 == null)
                            {
                                return;
                            }
                            网络连接2.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 5635
                            });
                            return;
                        }
                        else if (this.当前地图 != PlayerObject.当前地图)
                        {
                            PlayerDeals PlayerDeals4 = this.当前交易;
                            if (PlayerDeals4 != null)
                            {
                                PlayerDeals4.结束交易();
                            }
                            客户网络 网络连接3 = this.网络连接;
                            if (网络连接3 == null)
                            {
                                return;
                            }
                            网络连接3.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 5636
                            });
                            return;
                        }
                        else if (base.网格距离(PlayerObject) > 12)
                        {
                            PlayerDeals PlayerDeals5 = this.当前交易;
                            if (PlayerDeals5 != null)
                            {
                                PlayerDeals5.结束交易();
                            }
                            客户网络 网络连接4 = this.网络连接;
                            if (网络连接4 == null)
                            {
                                return;
                            }
                            网络连接4.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 5636
                            });
                            return;
                        }
                        else
                        {
                            if (!PlayerObject.对象死亡 && PlayerObject.摆摊状态 == 0)
                            {
                                if (PlayerObject.交易状态 == 1)
                                {
                                    if (PlayerObject == this.当前交易.交易申请方)
                                    {
                                        if (this == PlayerObject.当前交易.交易接收方)
                                        {
                                            this.当前交易.更改状态(3, null);
                                            return;
                                        }
                                    }
                                    PlayerDeals PlayerDeals6 = this.当前交易;
                                    if (PlayerDeals6 != null)
                                    {
                                        PlayerDeals6.结束交易();
                                    }
                                    客户网络 网络连接5 = this.网络连接;
                                    if (网络连接5 == null)
                                    {
                                        return;
                                    }
                                    网络连接5.发送封包(new GameErrorMessagePacket
                                    {
                                        错误代码 = 5634
                                    });
                                    return;
                                }
                            }
                            PlayerDeals PlayerDeals7 = this.当前交易;
                            if (PlayerDeals7 != null)
                            {
                                PlayerDeals7.结束交易();
                            }
                            客户网络 网络连接6 = this.网络连接;
                            if (网络连接6 == null)
                            {
                                return;
                            }
                            网络连接6.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 5637
                            });
                            return;
                        }
                    }
                }
            }
            PlayerDeals PlayerDeals8 = this.当前交易;
            if (PlayerDeals8 != null)
            {
                PlayerDeals8.结束交易();
            }
            客户网络 网络连接7 = this.网络连接;
            if (网络连接7 == null)
            {
                return;
            }
            网络连接7.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 5634
            });
        }


        public void 玩家结束交易()
        {
            PlayerDeals PlayerDeals = this.当前交易;
            if (PlayerDeals == null)
            {
                return;
            }
            PlayerDeals.结束交易();
        }


        public void 玩家放入金币(int NumberGoldCoins)
        {
            if (this.交易状态 != 3)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5634
                });
                return;
            }
            else if (this.当前地图 != this.当前交易.对方玩家(this).当前地图)
            {
                PlayerDeals PlayerDeals2 = this.当前交易;
                if (PlayerDeals2 != null)
                {
                    PlayerDeals2.结束交易();
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else if (base.网格距离(this.当前交易.对方玩家(this)) > 12)
            {
                PlayerDeals PlayerDeals3 = this.当前交易;
                if (PlayerDeals3 != null)
                {
                    PlayerDeals3.结束交易();
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else
            {
                if (NumberGoldCoins <= 0 || this.NumberGoldCoins < NumberGoldCoins + (int)Math.Ceiling((double)((float)NumberGoldCoins * 0.04f)))
                {
                    PlayerDeals PlayerDeals4 = this.当前交易;
                    if (PlayerDeals4 != null)
                    {
                        PlayerDeals4.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Player put in gold coins. Error: Wrong number of coins"));
                    return;
                }
                if (this.当前交易.金币重复(this))
                {
                    PlayerDeals PlayerDeals5 = this.当前交易;
                    if (PlayerDeals5 != null)
                    {
                        PlayerDeals5.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Error: Player inserted gold coins. Error: Repeated coin placement"));
                    return;
                }
                this.当前交易.放入金币(this, NumberGoldCoins);
                return;
            }
        }


        public void 玩家放入物品(byte 放入位置, byte 放入物品, byte 背包类型, byte 物品位置)
        {
            if (this.交易状态 != 3)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5634
                });
                return;
            }
            else if (this.当前地图 != this.当前交易.对方玩家(this).当前地图)
            {
                PlayerDeals PlayerDeals2 = this.当前交易;
                if (PlayerDeals2 != null)
                {
                    PlayerDeals2.结束交易();
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else if (base.网格距离(this.当前交易.对方玩家(this)) > 12)
            {
                PlayerDeals PlayerDeals3 = this.当前交易;
                if (PlayerDeals3 != null)
                {
                    PlayerDeals3.结束交易();
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else
            {
                if (放入位置 >= 6)
                {
                    PlayerDeals PlayerDeals4 = this.当前交易;
                    if (PlayerDeals4 != null)
                    {
                        PlayerDeals4.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Player placed an item. Error: Incorrectly placed"));
                    return;
                }
                if (this.当前交易.物品重复(this, 放入位置))
                {
                    PlayerDeals PlayerDeals5 = this.当前交易;
                    if (PlayerDeals5 != null)
                    {
                        PlayerDeals5.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Error: Player placed an item. Error: Duplicate placements"));
                    return;
                }
                if (放入物品 != 1)
                {
                    PlayerDeals PlayerDeals6 = this.当前交易;
                    if (PlayerDeals6 != null)
                    {
                        PlayerDeals6.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Error: Player put in an item. Error: Forbidden to retrieve items"));
                    return;
                }
                if (背包类型 != 1)
                {
                    PlayerDeals PlayerDeals7 = this.当前交易;
                    if (PlayerDeals7 != null)
                    {
                        PlayerDeals7.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Error: Player put in an item. Error: Wrong type of backpack"));
                    return;
                }
                ItemData ItemData;
                if (!this.角色背包.TryGetValue(物品位置, out ItemData))
                {
                    PlayerDeals PlayerDeals8 = this.当前交易;
                    if (PlayerDeals8 != null)
                    {
                        PlayerDeals8.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Player put in an item. Error: ItemData error"));
                    return;
                }
                if (ItemData.IsBound)
                {
                    PlayerDeals PlayerDeals9 = this.当前交易;
                    if (PlayerDeals9 != null)
                    {
                        PlayerDeals9.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Player puts in an item. Error: Binding item added"));
                    return;
                }
                if (this.当前交易.物品重复(this, ItemData))
                {
                    PlayerDeals PlayerDeals10 = this.当前交易;
                    if (PlayerDeals10 != null)
                    {
                        PlayerDeals10.结束交易();
                    }
                    this.网络连接.尝试断开连接(new Exception("MISTAKE: Player puts in an item. Error: Repeated item placement"));
                    return;
                }
                this.当前交易.放入物品(this, ItemData, 放入位置);
                return;
            }
        }


        public void 玩家锁定交易()
        {
            if (this.交易状态 != 3)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5634
                });
                return;
            }
            else if (this.当前地图 != this.当前交易.对方玩家(this).当前地图)
            {
                PlayerDeals PlayerDeals2 = this.当前交易;
                if (PlayerDeals2 != null)
                {
                    PlayerDeals2.结束交易();
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else
            {
                if (base.网格距离(this.当前交易.对方玩家(this)) <= 12)
                {
                    this.当前交易.更改状态(4, this);
                    return;
                }
                PlayerDeals PlayerDeals3 = this.当前交易;
                if (PlayerDeals3 != null)
                {
                    PlayerDeals3.结束交易();
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
        }


        public void 玩家解锁交易()
        {
            if (this.交易状态 < 4)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5634
                });
                return;
            }
            else if (this.当前地图 != this.当前交易.对方玩家(this).当前地图)
            {
                PlayerDeals PlayerDeals2 = this.当前交易;
                if (PlayerDeals2 != null)
                {
                    PlayerDeals2.结束交易();
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else
            {
                if (base.网格距离(this.当前交易.对方玩家(this)) <= 12)
                {
                    this.当前交易.更改状态(3, null);
                    return;
                }
                PlayerDeals PlayerDeals3 = this.当前交易;
                if (PlayerDeals3 != null)
                {
                    PlayerDeals3.结束交易();
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
        }


        public void 玩家确认交易()
        {
            if (this.交易状态 != 4)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5634
                });
                return;
            }
            else if (this.当前地图 != this.当前交易.对方玩家(this).当前地图)
            {
                PlayerDeals PlayerDeals2 = this.当前交易;
                if (PlayerDeals2 != null)
                {
                    PlayerDeals2.结束交易();
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else if (base.网格距离(this.当前交易.对方玩家(this)) > 12)
            {
                PlayerDeals PlayerDeals3 = this.当前交易;
                if (PlayerDeals3 != null)
                {
                    PlayerDeals3.结束交易();
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 5636
                });
                return;
            }
            else
            {
                if (this.当前交易.对方状态(this) != 5)
                {
                    this.当前交易.更改状态(5, this);
                    return;
                }
                PlayerObject PlayerObject;
                if (this.当前交易.背包已满(out PlayerObject))
                {
                    PlayerDeals PlayerDeals4 = this.当前交易;
                    if (PlayerDeals4 != null)
                    {
                        PlayerDeals4.结束交易();
                    }
                    this.当前交易.发送封包(new GameErrorMessagePacket
                    {
                        错误代码 = 5639,
                        第一参数 = PlayerObject.MapId
                    });
                    return;
                }
                this.当前交易.更改状态(5, this);
                this.当前交易.交换物品();
                return;
            }
        }


        public void 玩家准备摆摊()
        {
            if (this.对象死亡 || this.交易状态 >= 3)
            {
                return;
            }
            if (this.当前等级 < 30 && this.本期特权 == 0)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 65538
                });
                return;
            }
            else if (this.当前摊位 != null)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2825
                });
                return;
            }
            else if (!this.当前地图.摆摊区内(this.当前坐标))
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2818
                });
                return;
            }
            else
            {
                if (this.当前地图[this.当前坐标].FirstOrDefault(delegate (MapObject O)
                {
                    PlayerObject PlayerObject = O as PlayerObject;
                    return PlayerObject != null && PlayerObject.当前摊位 != null;
                }) == null)
                {
                    this.当前摊位 = new PlayerBoth();
                    base.发送封包(new 摆摊状态改变
                    {
                        对象编号 = this.MapId,
                        摊位状态 = 1
                    });
                    return;
                }
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2819
                });
                return;
            }
        }


        public void 玩家重整摊位()
        {
            if (this.摆摊状态 == 2)
            {
                this.当前摊位.摊位状态 = 1;
                base.发送封包(new 摆摊状态改变
                {
                    对象编号 = this.MapId,
                    摊位状态 = this.摆摊状态
                });
                return;
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 2817
            });
        }


        public void 玩家开始摆摊()
        {
            if (this.摆摊状态 != 1)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2817
                });
                return;
            }
            else if (this.当前等级 < 30 && this.本期特权 == 0)
            {
                PlayerDeals PlayerDeals = this.当前交易;
                if (PlayerDeals != null)
                {
                    PlayerDeals.结束交易();
                }
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 65538
                });
                return;
            }
            else
            {
                if (this.当前摊位.物品总价() + (long)this.NumberGoldCoins <= 2147483647L)
                {
                    this.当前摊位.摊位状态 = 2;
                    base.发送封包(new 摆摊状态改变
                    {
                        对象编号 = this.MapId,
                        摊位状态 = this.摆摊状态
                    });
                    return;
                }
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2827
                });
                return;
            }
        }


        public void 玩家收起摊位()
        {
            if (this.摆摊状态 != 0)
            {
                this.当前摊位 = null;
                base.发送封包(new 摆摊状态改变
                {
                    对象编号 = this.MapId,
                    摊位状态 = this.摆摊状态
                });
                return;
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 2817
            });
        }


        public void PutItemsInBoothPacket(byte 放入位置, byte 背包类型, byte 物品位置, ushort 物品数量, int 物品价格)
        {
            if (this.摆摊状态 != 1)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2817
                });
                return;
            }
            else
            {
                if (放入位置 >= 10)
                {
                    this.网络连接.尝试断开连接(new Exception("Error operation: PutItemsInBoothPacket, Error: Put in wrong position"));
                    return;
                }
                if (this.当前摊位.摊位物品.ContainsKey(放入位置))
                {
                    this.网络连接.尝试断开连接(new Exception("Error operation: PutItemsInBoothPacket, Error: Repeat placement"));
                    return;
                }
                if (背包类型 != 1)
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: PutItemsInBoothPacket, Error: Wrong backpack type"));
                    return;
                }
                if (物品价格 < 100)
                {
                    this.网络连接.尝试断开连接(new Exception("Error action: PutItemsInBoothPacket, Error: Item price error"));
                    return;
                }
                ItemData ItemData;
                if (!this.角色背包.TryGetValue(物品位置, out ItemData))
                {
                    this.网络连接.尝试断开连接(new Exception("Error Action: PutItemsInBoothPacket, Error: Selected item is empty"));
                    return;
                }
                if (this.当前摊位.摊位物品.Values.FirstOrDefault((ItemData O) => O.物品位置.V == 物品位置) != null)
                {
                    this.网络连接.尝试断开连接(new Exception("Action error: PutItemsInBoothPacket, Error: Repeated item placement"));
                    return;
                }
                if (ItemData.IsBound)
                {
                    this.网络连接.尝试断开连接(new Exception("Error Action: PutItemsInBoothPacket, Error: Putting in bound items"));
                    return;
                }
                if ((int)物品数量 > (ItemData.能否堆叠 ? ItemData.当前持久.V : 1))
                {
                    this.网络连接.尝试断开连接(new Exception("Error Action: PutItemsInBoothPacket, Error: Wrong number of items"));
                    return;
                }
                this.当前摊位.摊位物品.Add(放入位置, ItemData);
                this.当前摊位.物品数量.Add(ItemData, (int)物品数量);
                this.当前摊位.物品单价.Add(ItemData, 物品价格);
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new AddStallItemsPacket
                {
                    放入位置 = 放入位置,
                    背包类型 = 背包类型,
                    物品位置 = 物品位置,
                    物品数量 = 物品数量,
                    物品价格 = 物品价格
                });
                return;
            }
        }


        public void 取回摊位物品(byte 取回位置)
        {
            if (this.摆摊状态 != 1)
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2817
                });
                return;
            }
            else
            {
                ItemData key;
                if (!this.当前摊位.摊位物品.TryGetValue(取回位置, out key))
                {
                    this.网络连接.尝试断开连接(new Exception("Wrong action: Retrieve stall item, Error: Selected item is empty"));
                    return;
                }
                this.当前摊位.物品单价.Remove(key);
                this.当前摊位.物品数量.Remove(key);
                this.当前摊位.摊位物品.Remove(取回位置);
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new RemoveStallItemsPacket
                {
                    取回位置 = 取回位置
                });
                return;
            }
        }


        public void 更改摊位名字(string 摊位名字)
        {
            if (this.摆摊状态 == 1)
            {
                this.当前摊位.摊位名字 = 摊位名字;
                base.发送封包(new 变更摊位名字
                {
                    对象编号 = this.MapId,
                    摊位名字 = 摊位名字
                });
                return;
            }
            客户网络 网络连接 = this.网络连接;
            if (网络连接 == null)
            {
                return;
            }
            网络连接.发送封包(new GameErrorMessagePacket
            {
                错误代码 = 2817
            });
        }


        public void 升级摊位外观(byte 外观编号)
        {
        }


        public void 玩家打开摊位(int 对象编号)
        {
            PlayerObject PlayerObject;
            if (!MapGatewayProcess.玩家对象表.TryGetValue(对象编号, out PlayerObject))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2828
                });
                return;
            }
            else if (PlayerObject.摆摊状态 != 2)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2828
                });
                return;
            }
            else
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new SyncBoothDataPacket
                {
                    对象编号 = PlayerObject.MapId,
                    字节数据 = PlayerObject.当前摊位.摊位描述()
                });
                return;
            }
        }


        public void 购买摊位物品(int 对象编号, byte 物品位置, ushort 购买数量)
        {
            PlayerObject PlayerObject;
            ItemData ItemData;
            if (!MapGatewayProcess.玩家对象表.TryGetValue(对象编号, out PlayerObject))
            {
                客户网络 网络连接 = this.网络连接;
                if (网络连接 == null)
                {
                    return;
                }
                网络连接.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2828
                });
                return;
            }
            else if (PlayerObject.摆摊状态 != 2)
            {
                客户网络 网络连接2 = this.网络连接;
                if (网络连接2 == null)
                {
                    return;
                }
                网络连接2.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2828
                });
                return;
            }
            else if (!PlayerObject.当前摊位.摊位物品.TryGetValue(物品位置, out ItemData))
            {
                客户网络 网络连接3 = this.网络连接;
                if (网络连接3 == null)
                {
                    return;
                }
                网络连接3.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2824
                });
                return;
            }
            else if (PlayerObject.当前摊位.物品数量[ItemData] < (int)购买数量)
            {
                客户网络 网络连接4 = this.网络连接;
                if (网络连接4 == null)
                {
                    return;
                }
                网络连接4.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2830
                });
                return;
            }
            else
            {
                if (this.NumberGoldCoins >= PlayerObject.当前摊位.物品单价[ItemData] * (int)购买数量)
                {
                    byte b = byte.MaxValue;
                    byte b2 = 0;
                    while (b2 < this.背包大小)
                    {
                        if (this.角色背包.ContainsKey(b2))
                        {
                            b2 += 1;
                        }
                        else
                        {
                            b = b2;
                        IL_12B:
                            if (b != 255)
                            {
                                int num = PlayerObject.当前摊位.物品单价[ItemData] * (int)购买数量;
                                this.NumberGoldCoins -= num;
                                this.CharacterData.转出金币.V += (long)num;
                                PlayerObject.NumberGoldCoins += (int)((float)num * 0.95f);
                                Dictionary<ItemData, int> 物品数量 = PlayerObject.当前摊位.物品数量;
                                ItemData key = ItemData;
                                if ((物品数量[key] -= (int)购买数量) <= 0)
                                {
                                    PlayerObject.角色背包.Remove(ItemData.物品位置.V);
                                    客户网络 网络连接5 = PlayerObject.网络连接;
                                    if (网络连接5 != null)
                                    {
                                        网络连接5.发送封包(new 删除玩家物品
                                        {
                                            背包类型 = 1,
                                            物品位置 = ItemData.物品位置.V
                                        });
                                    }
                                }
                                else
                                {
                                    ItemData.当前持久.V -= (int)购买数量;
                                    客户网络 网络连接6 = PlayerObject.网络连接;
                                    if (网络连接6 != null)
                                    {
                                        网络连接6.发送封包(new 玩家物品变动
                                        {
                                            物品描述 = ItemData.字节描述()
                                        });
                                    }
                                }
                                if (PlayerObject.当前摊位.物品数量[ItemData] <= 0)
                                {
                                    this.角色背包[b] = ItemData;
                                    ItemData.物品位置.V = b;
                                    ItemData.物品容器.V = 1;
                                }
                                else
                                {
                                    this.角色背包[b] = new ItemData(ItemData.物品模板, ItemData.生成来源.V, 1, b, (int)购买数量);
                                }
                                客户网络 网络连接7 = this.网络连接;
                                if (网络连接7 != null)
                                {
                                    网络连接7.发送封包(new 玩家物品变动
                                    {
                                        物品描述 = this.角色背包[b].字节描述()
                                    });
                                }
                                客户网络 网络连接8 = this.网络连接;
                                if (网络连接8 != null)
                                {
                                    网络连接8.发送封包(new 购入摊位物品
                                    {
                                        对象编号 = PlayerObject.MapId,
                                        物品位置 = 物品位置,
                                        剩余数量 = PlayerObject.当前摊位.物品数量[ItemData]
                                    });
                                }
                                客户网络 网络连接9 = PlayerObject.网络连接;
                                if (网络连接9 != null)
                                {
                                    网络连接9.发送封包(new StallItemsSoldPacket
                                    {
                                        物品位置 = 物品位置,
                                        售出数量 = (int)购买数量,
                                        售出收益 = (int)((float)num * 0.95f)
                                    });
                                }
                                MainProcess.AddSystemLog(string.Format("[{0}][Level {1}] purchased [{4}] * {5} of [{2}][{3}] stall items, costing [{6}] coins", new object[]
                                {
                                    this.对象名字,
                                    this.当前等级,
                                    PlayerObject.对象名字,
                                    PlayerObject.当前等级,
                                    this.角色背包[b],
                                    购买数量,
                                    num
                                }));
                                if (PlayerObject.当前摊位.物品数量[ItemData] <= 0)
                                {
                                    PlayerObject.当前摊位.摊位物品.Remove(物品位置);
                                    PlayerObject.当前摊位.物品单价.Remove(ItemData);
                                    PlayerObject.当前摊位.物品数量.Remove(ItemData);
                                }
                                if (PlayerObject.当前摊位.物品数量.Count <= 0)
                                {
                                    PlayerObject.玩家收起摊位();
                                }
                                return;
                            }
                            客户网络 网络连接10 = this.网络连接;
                            if (网络连接10 == null)
                            {
                                return;
                            }
                            网络连接10.发送封包(new GameErrorMessagePacket
                            {
                                错误代码 = 1793
                            });
                            return;
                        }
                    }
                    //goto IL_12B;
                }
                客户网络 网络连接11 = this.网络连接;
                if (网络连接11 == null)
                {
                    return;
                }
                网络连接11.发送封包(new GameErrorMessagePacket
                {
                    错误代码 = 2561
                });
                return;
            }
        }


        public byte[] 玩家StatDescription()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    for (byte b = 0; b <= 100; b += 1)
                    {
                        GameObjectStats GameObjectProperties;
                        if (Enum.TryParse<GameObjectStats>(b.ToString(), out GameObjectProperties) && Enum.IsDefined(typeof(GameObjectStats), GameObjectProperties))
                        {
                            binaryWriter.Write(b);
                            binaryWriter.Write(this[GameObjectProperties]);
                            binaryWriter.Write(new byte[2]);
                        }
                        else
                        {
                            binaryWriter.Write(b);
                            binaryWriter.Write(new byte[6]);
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部技能描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (SkillData SkillData in this.MainSkills表.Values)
                    {
                        binaryWriter.Write(SkillData.SkillId.V);
                        binaryWriter.Write(SkillData.Id);
                        binaryWriter.Write(SkillData.技能等级.V);
                        binaryWriter.Write(SkillData.技能经验.V);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部冷却描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (KeyValuePair<int, DateTime> keyValuePair in this.冷却记录)
                    {
                        if (!(MainProcess.CurrentTime >= keyValuePair.Value))
                        {
                            binaryWriter.Write(keyValuePair.Key);
                            binaryWriter.Write((int)(keyValuePair.Value - MainProcess.CurrentTime).TotalMilliseconds);
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部Buff描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (BuffData BuffData in this.Buff列表.Values)
                    {
                        binaryWriter.Write(BuffData.Id.V);
                        binaryWriter.Write((int)BuffData.Id.V);
                        binaryWriter.Write(BuffData.当前层数.V);
                        binaryWriter.Write((int)BuffData.剩余时间.V.TotalMilliseconds);
                        binaryWriter.Write((int)BuffData.持续时间.V.TotalMilliseconds);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 快捷栏位描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (KeyValuePair<byte, SkillData> keyValuePair in this.快捷栏位)
                    {
                        binaryWriter.Write(keyValuePair.Key);
                        BinaryWriter binaryWriter2 = binaryWriter;
                        SkillData value = keyValuePair.Value;
                        binaryWriter2.Write((value != null) ? value.SkillId.V : 0);
                        binaryWriter.Write(false);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部货币描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    for (int i = 0; i <= 19; i++)
                    {
                        binaryWriter.Seek(i * 48, SeekOrigin.Begin);
                        binaryWriter.Write(this.CharacterData.角色货币[(GameCurrency)i]);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部称号描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(this.当前称号);
                    binaryWriter.Write((byte)this.称号列表.Count);
                    foreach (KeyValuePair<byte, DateTime> keyValuePair in this.称号列表)
                    {
                        binaryWriter.Write(keyValuePair.Key);
                        binaryWriter.Write((keyValuePair.Value == DateTime.MaxValue) ? uint.MaxValue : ((uint)(keyValuePair.Value - MainProcess.CurrentTime).TotalMinutes));
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部物品描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (EquipmentData EquipmentData in this.角色装备.Values.ToList<EquipmentData>())
                    {
                        if (EquipmentData != null)
                        {
                            binaryWriter.Write(EquipmentData.字节描述());
                        }
                    }
                    foreach (ItemData ItemData in this.角色背包.Values.ToList<ItemData>())
                    {
                        if (ItemData != null)
                        {
                            binaryWriter.Write(ItemData.字节描述());
                        }
                    }
                    foreach (ItemData ItemData2 in this.角色仓库.Values.ToList<ItemData>())
                    {
                        if (ItemData2 != null)
                        {
                            binaryWriter.Write(ItemData2.字节描述());
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 全部邮件描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((ushort)this.全部邮件.Count);
                    foreach (MailData MailData in this.全部邮件)
                    {
                        binaryWriter.Write(MailData.邮件检索描述());
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 背包物品描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (ItemData ItemData in this.角色背包.Values.ToList<ItemData>())
                    {
                        if (ItemData != null)
                        {
                            binaryWriter.Write(ItemData.字节描述());
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 仓库物品描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (ItemData ItemData in this.角色仓库.Values.ToList<ItemData>())
                    {
                        if (ItemData != null)
                        {
                            binaryWriter.Write(ItemData.字节描述());
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 装备物品描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    foreach (EquipmentData EquipmentData in this.角色装备.Values.ToList<EquipmentData>())
                    {
                        if (EquipmentData != null)
                        {
                            binaryWriter.Write(EquipmentData.字节描述());
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 玛法特权描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(this.CharacterData.预定特权.V);
                    binaryWriter.Write(this.本期特权);
                    binaryWriter.Write((this.本期特权 == 0) ? 0 : ComputingClass.TimeShift(this.本期日期));
                    binaryWriter.Write((this.本期特权 == 0) ? 0U : this.本期记录);
                    binaryWriter.Write(this.上期特权);
                    binaryWriter.Write((this.上期特权 == 0) ? 0 : ComputingClass.TimeShift(this.上期日期));
                    binaryWriter.Write((this.上期特权 == 0) ? 0U : this.上期记录);
                    binaryWriter.Write((byte)5);
                    for (byte b = 1; b <= 5; b += 1)
                    {
                        binaryWriter.Write(b);
                        int num;
                        binaryWriter.Write(this.剩余特权.TryGetValue(b, out num) ? num : 0);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 社交列表描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((byte)this.好友列表.Count);
                    binaryWriter.Write((byte)(this.偶像列表.Count + this.仇人列表.Count));
                    foreach (CharacterData CharacterData in this.偶像列表)
                    {
                        binaryWriter.Write(CharacterData.数据索引.V);
                        byte[] array = new byte[69];
                        byte[] array2 = CharacterData.名字描述();
                        Buffer.BlockCopy(array2, 0, array, 0, array2.Length);
                        binaryWriter.Write(array);
                        binaryWriter.Write((byte)CharacterData.角色职业.V);
                        binaryWriter.Write((byte)CharacterData.角色性别.V);
                        binaryWriter.Write((CharacterData.ActiveConnection != null) ? 0 : 3);
                        binaryWriter.Write(0U);
                        binaryWriter.Write((byte)0);
                        binaryWriter.Write((byte)(this.好友列表.Contains(CharacterData) ? 1 : 0));
                    }
                    foreach (CharacterData CharacterData2 in this.仇人列表)
                    {
                        binaryWriter.Write(CharacterData2.数据索引.V);
                        byte[] array3 = new byte[69];
                        byte[] array4 = CharacterData2.名字描述();
                        Buffer.BlockCopy(array4, 0, array3, 0, array4.Length);
                        binaryWriter.Write(array3);
                        binaryWriter.Write((byte)CharacterData2.角色职业.V);
                        binaryWriter.Write((byte)CharacterData2.角色性别.V);
                        binaryWriter.Write((CharacterData2.ActiveConnection != null) ? 0 : 3);
                        binaryWriter.Write(0U);
                        binaryWriter.Write((byte)21);
                        binaryWriter.Write((byte)0);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] 社交屏蔽描述()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((byte)this.黑名单表.Count);
                    foreach (CharacterData CharacterData in this.黑名单表)
                    {
                        binaryWriter.Write(CharacterData.数据索引.V);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public CharacterData CharacterData;


        public InscriptionSkill InscriptionSkill;


        public PlayerDeals 当前交易;


        public PlayerBoth 当前摊位;


        public byte 雕色部位;


        public byte 重铸部位;


        public int 对话页面;


        public GuardInstance 对话守卫;


        public DateTime 对话超时;


        public int 打开商店;


        public string 打开界面;


        public int 回血次数;


        public int 回魔次数;


        public int 回血基数;


        public int 回魔基数;


        public DateTime 邮件时间;


        public DateTime 药品回血;


        public DateTime 药品回魔;


        public DateTime 称号时间;


        public DateTime 特权时间;


        public DateTime 拾取时间;


        public DateTime 队伍时间;


        public DateTime 战具计时;


        public DateTime 经验计时;


        public List<ItemData> 回购清单;


        public List<PetObject> 宠物列表;


        public Dictionary<object, int> CombatBonus;


        public readonly Dictionary<ushort, SkillData> PassiveSkill;
    }
}
