﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using GameServer.Maps;
using GameServer.Data;

namespace GameServer.Networking
{
	
	public sealed class 客户网络
	{
		
		public 客户网络(TcpClient 客户端)
		{
			
			this.剩余数据 = new byte[0];
			this.接收列表 = new ConcurrentQueue<GamePacket>();
			this.发送列表 = new ConcurrentQueue<GamePacket>();
			
			this.当前连接 = 客户端;
			this.当前连接.NoDelay = true;
			this.接入时间 = MainProcess.CurrentTime;
			this.断开时间 = MainProcess.CurrentTime.AddMinutes((double)CustomClass.掉线判定时间);
			this.断网事件 = (EventHandler<Exception>)Delegate.Combine(this.断网事件, new EventHandler<Exception>(NetworkServiceGateway.断网回调));
			this.网络地址 = this.当前连接.Client.RemoteEndPoint.ToString().Split(new char[]
			{
				':'
			})[0];
			this.开始异步接收();
		}

		
		public void 处理数据()
		{
			try
			{
				if (!this.正在断开 && !NetworkServiceGateway.网络服务停止)
				{
					if (MainProcess.CurrentTime > this.断开时间)
					{
						this.尝试断开连接(new Exception("No response for a long time, disconnect."));
					}
					else
					{
						this.处理已收封包();
						this.发送全部封包();
					}
				}
				else if (!this.正在发送 && this.接收列表.Count == 0 && this.发送列表.Count == 0)
				{
					PlayerObject PlayerObject = this.绑定角色;
					if (PlayerObject != null)
					{
						PlayerObject.玩家角色下线();
					}
					AccountData AccountData = this.绑定账号;
					if (AccountData != null)
					{
						AccountData.账号下线();
					}
					NetworkServiceGateway.移除网络(this);
					this.当前连接.Client.Shutdown(SocketShutdown.Both);
					this.当前连接.Close();
					this.接收列表 = null;
					this.发送列表 = null;
					this.当前阶段 = GameStage.正在登录;
				}
				else
				{
					this.处理已收封包();
					this.发送全部封包();
				}
			}
			catch (Exception ex)
			{
				if (this.绑定角色 != null)
				{
					string[] array = new string[10];
					array[0] = "处理网络数据时出现异常, 已断开对应连接\r\n账号:[";
					int num = 1;
					AccountData AccountData2 = this.绑定账号;
					string text;
					if (AccountData2 != null)
					{
						if ((text = AccountData2.账号名字.V) != null)
						{
							goto IL_113;
						}
					}
					text = "None";
					IL_113:
					array[num] = text;
					array[2] = "]\r\n角色:[";
					int num2 = 3;
					PlayerObject PlayerObject2 = this.绑定角色;
					string text2;
					if (PlayerObject2 != null)
					{
						if ((text2 = PlayerObject2.对象名字) != null)
						{
							goto IL_139;
						}
					}
					text2 = "None";
					IL_139:
					array[num2] = text2;
					array[4] = "]\r\n网络地址:[";
					array[5] = this.网络地址;
					array[6] = "]\r\n物理地址:[";
					array[7] = this.物理地址;
					array[8] = "]\r\n错误提示:";
					array[9] = ex.Message;
					MainProcess.AddSystemLog(string.Concat(array));
				}
				PlayerObject PlayerObject3 = this.绑定角色;
				if (PlayerObject3 != null)
				{
					PlayerObject3.玩家角色下线();
				}
				AccountData AccountData3 = this.绑定账号;
				if (AccountData3 != null)
				{
					AccountData3.账号下线();
				}
				NetworkServiceGateway.移除网络(this);
				Socket client = this.当前连接.Client;
				if (client != null)
				{
					client.Shutdown(SocketShutdown.Both);
				}
				TcpClient tcpClient = this.当前连接;
				if (tcpClient != null)
				{
					tcpClient.Close();
				}
				this.接收列表 = null;
				this.发送列表 = null;
				this.当前阶段 = GameStage.正在登录;
			}
		}

		
		public void 发送封包(GamePacket 封包)
		{
			if (!this.正在断开 && !NetworkServiceGateway.网络服务停止 && 封包 != null)
			{
				this.发送列表.Enqueue(封包);
			}
		}

		
		public void 尝试断开连接(Exception e)
		{
			if (!this.正在断开)
			{
				this.正在断开 = true;
				EventHandler<Exception> eventHandler = this.断网事件;
				if (eventHandler == null)
				{
					return;
				}
				eventHandler(this, e);
			}
		}

		
		private void 处理已收封包()
		{
			while (!this.接收列表.IsEmpty)
			{
				if (this.接收列表.Count > (int)CustomClass.封包限定数量)
				{
					this.接收列表 = new ConcurrentQueue<GamePacket>();
					NetworkServiceGateway.屏蔽网络(this.网络地址);
					this.尝试断开连接(new Exception("Too many packets, disconnect and restrict login."));
					return;
				}
                if (this.接收列表.TryDequeue(out GamePacket GamePacket))
                {
                    if (!GamePacket.封包处理方法表.TryGetValue(GamePacket.封包类型, out MethodInfo methodInfo))
                    {
                        this.尝试断开连接(new Exception("No packet handling found, disconnect. Packet type: " + GamePacket.封包类型.FullName));
                        return;
                    }
                    methodInfo.Invoke(this, new object[]
                    {
                        GamePacket
                    });
                }
            }
		}

		
		private void 发送全部封包()
		{
			List<byte> list = new();
			while (!this.发送列表.IsEmpty)
			{
                if (this.发送列表.TryDequeue(out GamePacket GamePacket))
                {
                    list.AddRange(GamePacket.取字节());
                }
            }
			if (list.Count != 0)
			{
				this.开始异步发送(list);
			}
		}

		
		private void 延迟掉线时间()
		{
			this.断开时间 = MainProcess.CurrentTime.AddMinutes((double)CustomClass.掉线判定时间);
		}

		
		private void 开始异步接收()
		{
			try
			{
				if (!this.正在断开 && !NetworkServiceGateway.网络服务停止)
				{
					byte[] array = new byte[8192];
					this.当前连接.Client.BeginReceive(array, 0, array.Length, SocketFlags.None, new AsyncCallback(this.接收完成回调), array);
				}
			}
			catch (Exception ex)
			{
				this.尝试断开连接(new Exception("Asynchronous Receiving Error: " + ex.Message));
			}
		}

		
		private void 接收完成回调(IAsyncResult 异步参数)
		{
			try
			{
				if (!this.正在断开 && !NetworkServiceGateway.网络服务停止 && this.当前连接.Client != null)
				{
					Socket client = this.当前连接.Client;
					int num = (client != null) ? client.EndReceive(异步参数) : 0;
					if (num > 0)
					{
						this.接收总数 += num;
						NetworkServiceGateway.ReceivedBytes += (long)num;
						Array src = 异步参数.AsyncState as byte[];
						byte[] dst = new byte[this.剩余数据.Length + num];
						Buffer.BlockCopy(this.剩余数据, 0, dst, 0, this.剩余数据.Length);
						Buffer.BlockCopy(src, 0, dst, this.剩余数据.Length, num);
						this.剩余数据 = dst;
						for (;;)
						{
							GamePacket GamePacket = GamePacket.取封包(this, this.剩余数据, out this.剩余数据);
							if (GamePacket == null)
							{
								break;
							}
							this.接收列表.Enqueue(GamePacket);
						}
						this.延迟掉线时间();
						this.开始异步接收();
					}
					else
					{
						this.尝试断开连接(new Exception("Client disconnected."));
					}
				}
			}
			catch (Exception ex)
			{
				this.尝试断开连接(new Exception("Packet construction error, message: " + ex.Message));
			}
		}

		
		private void 开始异步发送(List<byte> 数据)
		{
			try
			{
				this.正在发送 = true;
				this.当前连接.Client.BeginSend(数据.ToArray(), 0, 数据.Count, SocketFlags.None, new AsyncCallback(this.发送完成回调), null);
			}
			catch (Exception ex)
			{
				this.正在发送 = false;
				this.发送列表 = new ConcurrentQueue<GamePacket>();
				this.尝试断开连接(new Exception("Asynchronous sending error: " + ex.Message));
			}
		}

		
		private void 发送完成回调(IAsyncResult 异步参数)
		{
			try
			{
				int num = this.当前连接.Client.EndSend(异步参数);
				this.发送总数 += num;
				NetworkServiceGateway.SendedBytes += (long)num;
				if (num == 0)
				{
					this.发送列表 = new ConcurrentQueue<GamePacket>();
					this.尝试断开连接(new Exception("Error sending callback!"));
				}
				this.正在发送 = false;
			}
			catch (Exception ex)
			{
				this.正在发送 = false;
				this.发送列表 = new ConcurrentQueue<GamePacket>();
				this.尝试断开连接(new Exception("Sending callback errors: " + ex.Message));
			}
		}

		
		public void 处理封包(ReservedPacketZeroOnePacket P)
		{
		}

		
		public void 处理封包(ReservedPacketZeroTwoPacket P)
		{
		}

		
		public void 处理封包(ReservedPacketZeroThreePacket P)
		{
		}

		
		public void 处理封包(上传游戏设置 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家更改设置(P.字节描述);
		}

		
		public void 处理封包(客户碰触法阵 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(客户进入法阵 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家进入法阵(P.TeleportGateNumber);
		}

		
		public void 处理封包(ClickNpcDialogPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(RequestObjectDataPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.请求对象外观(P.对象编号, P.状态编号);
		}

		
		public void 处理封包(客户网速测试 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.发送封包(new InternetSpeedTestPacket
			{
				当前时间 = P.客户时间
			});
		}

		
		public void 处理封包(测试网关网速 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.发送封包(new LoginQueryResponsePacket
			{
				当前时间 = P.客户时间
			});
		}

		
		public void 处理封包(客户请求复活 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家请求复活();
		}

		
		public void 处理封包(ToggleAttackMode P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
            if (Enum.IsDefined(typeof(AttackMode), (int)P.AttackMode) && Enum.TryParse<AttackMode>(P.AttackMode.ToString(), out AttackMode 模式))
            {
                this.绑定角色.更改AttackMode(模式);
                return;
            }
            this.尝试断开连接(new Exception("更改AttackMode时提供错误的枚举参数.即将断开连接."));
		}

		
		public void 处理封包(更改PetMode P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
            if (Enum.IsDefined(typeof(PetMode), (int)P.PetMode) && Enum.TryParse<PetMode>(P.PetMode.ToString(), out PetMode 模式))
            {
                this.绑定角色.更改PetMode(模式);
                return;
            }
            this.尝试断开连接(new Exception(string.Format("更改PetMode时提供错误的枚举参数.即将断开连接. 参数 - {0}", P.PetMode)));
		}

		
		public void 处理封包(上传角色位置 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家同步位置();
		}

		
		public void 处理封包(客户角色转动 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
            if (Enum.IsDefined(typeof(GameDirection), (int)P.转动方向) && Enum.TryParse<GameDirection>(P.转动方向.ToString(), out GameDirection 转动方向))
            {
                this.绑定角色.玩家角色转动(转动方向);
                return;
            }
            this.尝试断开连接(new Exception("玩家角色转动时提供错误的枚举参数.即将断开连接."));
		}

		
		public void 处理封包(客户角色走动 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家角色走动(P.坐标);
		}

		
		public void 处理封包(客户角色跑动 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家角色跑动(P.坐标);
		}

		
		public void 处理封包(CharacterSwitchSkillsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家开关技能(P.SkillId);
		}

		
		public void 处理封包(CharacterEquipmentSkillsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			if (P.技能栏位 < 32)
			{
				this.绑定角色.玩家拖动技能(P.技能栏位, P.SkillId);
				return;
			}
			this.尝试断开连接(new Exception("玩家装配技能时提供错误的封包参数.即将断开连接."));
		}

		
		public void 处理封包(CharacterReleaseSkillsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家释放技能(P.SkillId, P.动作编号, P.目标编号, P.锚点坐标);
		}

		
		public void 处理封包(BattleStanceSwitchPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家切换姿态();
		}

		
		public void 处理封包(客户更换角色 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.更换角色(this);
			this.当前阶段 = GameStage.选择角色;
		}

		
		public void 处理封包(场景加载完成 P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家进入场景();
			this.当前阶段 = GameStage.正在游戏;
		}

		
		public void 处理封包(ExitCurrentCopyPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家退出副本();
		}

		
		public void 处理封包(玩家退出登录 P)
		{
			if (this.当前阶段 == GameStage.正在登录)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.返回登录(this);
		}

		
		public void 处理封包(打开角色背包 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(CharacterPickupItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(CharacterDropsItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家丢弃物品(P.背包类型, P.物品位置, P.丢弃数量);
		}

		
		public void 处理封包(CharacterTransferItemPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家转移物品(P.当前背包, P.原有位置, P.目标背包, P.目标位置);
		}

		
		public void 处理封包(CharacterUseItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家使用物品(P.背包类型, P.物品位置);
		}

		
		public void 处理封包(玩家喝修复油 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家喝修复油(P.背包类型, P.物品位置);
		}

		
		public void 处理封包(玩家扩展背包 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家扩展背包(P.背包类型, P.扩展大小);
		}

		
		public void 处理封包(RequestStoreDataPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.RequestStoreDataPacket(P.版本编号);
		}

		
		public void 处理封包(CharacterPurchageItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家购买物品(P.StoreId, P.物品位置, P.购入数量);
		}

		
		public void 处理封包(CharacterSellItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家出售物品(P.背包类型, P.物品位置, P.卖出数量);
		}

		
		public void 处理封包(查询回购列表 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.请求回购清单();
		}

		
		public void 处理封包(CharacterRepurchageItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			if (P.物品位置 < 100)
			{
				this.绑定角色.玩家回购物品(P.物品位置);
				return;
			}
			this.尝试断开连接(new Exception("玩家回购物品时提供错误的位置参数.即将断开连接."));
		}

		
		public void 处理封包(商店修理单件 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.商店修理单件(P.背包类型, P.物品位置);
		}

		
		public void 处理封包(商店修理全部 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.商店修理全部();
		}

		
		public void 处理封包(商店特修单件 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.商店特修单件(P.物品容器, P.物品位置);
		}

		
		public void 处理封包(随身修理单件 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.随身修理单件(P.物品容器, P.物品位置, P.Id);
		}

		
		public void 处理封包(随身特修全部 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.随身修理全部();
		}

		
		public void 处理封包(CharacterOrganizerBackpackPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家整理背包(P.背包类型);
		}

		
		public void 处理封包(CharacterSplitItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家拆分物品(P.当前背包, P.物品位置, P.拆分数量, P.目标背包, P.目标位置);
		}

		
		public void 处理封包(CharacterBreakdownItemsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
            if (Enum.TryParse<ItemBackPack>(P.背包类型.ToString(), out ItemBackPack ItemBackPack) && Enum.IsDefined(typeof(ItemBackPack), ItemBackPack))
            {
                this.绑定角色.玩家分解物品(P.背包类型, P.物品位置, P.分解数量);
                return;
            }
            this.尝试断开连接(new Exception("玩家分解物品时提供错误的枚举参数.即将断开连接."));
		}

		
		public void 处理封包(CharacterSynthesisItemPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家合成物品();
		}

		
		public void 处理封包(玩家镶嵌灵石 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家镶嵌灵石(P.装备类型, P.装备位置, P.装备孔位, P.灵石类型, P.灵石位置);
		}

		
		public void 处理封包(玩家拆除灵石 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家拆除灵石(P.装备类型, P.装备位置, P.装备孔位);
		}

		
		public void 处理封包(OrdinaryInscriptionRefinementPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.OrdinaryInscriptionRefinementPacket(P.装备类型, P.装备位置, P.Id);
		}

		
		public void 处理封包(高级铭文洗练 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.高级铭文洗练(P.装备类型, P.装备位置, P.Id);
		}

		
		public void 处理封包(ReplaceInscriptionRefinementPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.ReplaceInscriptionRefinementPacket(P.装备类型, P.装备位置, P.Id);
		}

		
		public void 处理封包(ReplaceAdvancedInscriptionPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.高级洗练确认(P.装备类型, P.装备位置);
		}

		
		public void 处理封包(ReplaceLowLevelInscriptionsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.替换洗练确认(P.装备类型, P.装备位置);
		}

		
		public void 处理封包(AbandonInscriptionReplacementPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.放弃替换铭文();
		}

		
		public void 处理封包(UnlockDoubleInscriptionSlotPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.UnlockDoubleInscriptionSlotPacket(P.装备类型, P.装备位置, P.操作参数);
		}

		
		public void 处理封包(ToggleDoubleInscriptionBitPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.ToggleDoubleInscriptionBitPacket(P.装备类型, P.装备位置, P.操作参数);
		}

		
		public void 处理封包(传承武器铭文 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.传承武器铭文(P.来源类型, P.来源位置, P.目标类型, P.目标位置);
		}

		
		public void 处理封包(升级武器普通 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.升级武器普通(P.首饰组, P.材料组);
		}

		
		public void 处理封包(CharacterSelectionTargetPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家选中对象(P.对象编号);
		}

		
		public void 处理封包(开始Npcc对话 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.开始Npcc对话(P.对象编号);
		}

		
		public void 处理封包(继续Npcc对话 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.继续Npcc对话(P.Id);
		}

		
		public void 处理封包(查看玩家装备 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查看对象装备(P.对象编号);
		}

		
		public void 处理封包(RequestDragonguardDataPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(RequestSoulStoneDataPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(查询奖励找回 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(同步角色战力 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询玩家战力(P.对象编号);
		}

		
		public void 处理封包(查询问卷调查 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(玩家申请交易 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家申请交易(P.对象编号);
		}

		
		public void 处理封包(玩家同意交易 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家同意交易(P.对象编号);
		}

		
		public void 处理封包(玩家结束交易 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家结束交易();
		}

		
		public void 处理封包(玩家放入金币 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家放入金币(P.NumberGoldCoins);
		}

		
		public void 处理封包(玩家放入物品 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家放入物品(P.放入位置, P.放入物品, P.物品容器, P.物品位置);
		}

		
		public void 处理封包(玩家锁定交易 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家锁定交易();
		}

		
		public void 处理封包(玩家解锁交易 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家解锁交易();
		}

		
		public void 处理封包(玩家确认交易 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家确认交易();
		}

		
		public void 处理封包(玩家准备摆摊 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家准备摆摊();
		}

		
		public void 处理封包(玩家重整摊位 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家重整摊位();
		}

		
		public void 处理封包(玩家开始摆摊 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家开始摆摊();
		}

		
		public void 处理封包(玩家收起摊位 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家收起摊位();
		}

		
		public void 处理封包(PutItemsInBoothPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.PutItemsInBoothPacket(P.放入位置, P.物品容器, P.物品位置, P.物品数量, P.物品价格);
		}

		
		public void 处理封包(取回摊位物品 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.取回摊位物品(P.取回位置);
		}

		
		public void 处理封包(更改摊位名字 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.更改摊位名字(P.摊位名字);
		}

		
		public void 处理封包(更改摊位外观 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.升级摊位外观(P.外观编号);
		}

		
		public void 处理封包(打开角色摊位 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家打开摊位(P.对象编号);
		}

		
		public void 处理封包(购买摊位物品 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.购买摊位物品(P.对象编号, P.物品位置, P.购买数量);
		}

		
		public void 处理封包(AddFriendsToFollowPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家添加关注(P.对象编号, P.对象名字);
		}

		
		public void 处理封包(取消好友关注 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家取消关注(P.对象编号);
		}

		
		public void 处理封包(CreateNewFriendGroupPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(MobileFriendsGroupPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(SendFriendChatPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			if (P.字节数据.Length < 7)
			{
				this.尝试断开连接(new Exception(string.Format("数据太短,断开连接.  处理封包: {0},  数据长度:{1}", P.GetType(), P.字节数据.Length)));
				return;
			}
			if (P.字节数据.Last<byte>() != 0)
			{
				this.尝试断开连接(new Exception(string.Format("数据错误,断开连接.  处理封包: {0},  无结束符.", P.GetType())));
				return;
			}
			this.绑定角色.玩家好友聊天(P.字节数据);
		}

		
		public void 处理封包(玩家添加仇人 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家添加仇人(P.对象编号);
		}

		
		public void 处理封包(玩家删除仇人 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家删除仇人(P.对象编号);
		}

		
		public void 处理封包(玩家屏蔽对象 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家屏蔽目标(P.对象编号);
		}

		
		public void 处理封包(玩家解除屏蔽 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家解除屏蔽(P.对象编号);
		}

		
		public void 处理封包(玩家比较成就 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(SendChatMessagePacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			if (P.字节数据.Length < 7)
			{
				this.尝试断开连接(new Exception(string.Format("数据太短,断开连接.  处理封包: {0},  数据长度:{1}", P.GetType(), P.字节数据.Length)));
				return;
			}
			if (P.字节数据.Last<byte>() != 0)
			{
				this.尝试断开连接(new Exception(string.Format("数据错误,断开连接.  处理封包: {0},  无结束符.", P.GetType())));
				return;
			}
			this.绑定角色.玩家发送广播(P.字节数据);
		}

		
		public void 处理封包(SendSocialMessagePacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			if (P.字节数据.Length < 6)
			{
				this.尝试断开连接(new Exception(string.Format("数据太短,断开连接.  处理封包: {0},  数据长度:{1}", P.GetType(), P.字节数据.Length)));
				return;
			}
			if (P.字节数据.Last<byte>() != 0)
			{
				this.尝试断开连接(new Exception(string.Format("数据错误,断开连接.  处理封包: {0},  无结束符.", P.GetType())));
				return;
			}
			this.绑定角色.玩家发送消息(P.字节数据);
		}

		
		public void 处理封包(RequestCharacterDataPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.请求角色资料(P.角色编号);
		}

		
		public void 处理封包(上传社交信息 P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(查询附近队伍 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询附近队伍();
		}

		
		public void 处理封包(查询队伍信息 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询队伍信息(P.对象编号);
		}

		
		public void 处理封包(申请创建队伍 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请创建队伍(P.对象编号, P.分配方式);
		}

		
		public void 处理封包(SendTeamRequestPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.SendTeamRequestPacket(P.对象编号);
		}

		
		public void 处理封包(申请离开队伍 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请队员离队(P.对象编号);
		}

		
		public void 处理封包(申请更改队伍 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请移交队长(P.队长编号);
		}

		
		public void 处理封包(回应组队请求 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.回应组队请求(P.对象编号, P.组队方式, P.回应方式);
		}

		
		public void 处理封包(玩家装配称号 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家使用称号(P.Id);
		}

		
		public void 处理封包(玩家卸下称号 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家卸下称号();
		}

		
		public void 处理封包(申请发送邮件 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请发送邮件(P.字节数据);
		}

		
		public void 处理封包(QueryMailboxContentPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.QueryMailboxContentPacket();
		}

		
		public void 处理封包(查看邮件内容 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查看邮件内容(P.邮件编号);
		}

		
		public void 处理封包(删除指定邮件 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.删除指定邮件(P.邮件编号);
		}

		
		public void 处理封包(提取邮件附件 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.提取邮件附件(P.邮件编号);
		}

		
		public void 处理封包(查询行会名字 P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询行会信息(P.行会编号);
		}

		
		public void 处理封包(更多行会信息 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.更多行会信息();
		}

		
		public void 处理封包(查看行会列表 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查看行会列表(P.行会编号, P.查看方式);
		}

		
		public void 处理封包(FindCorrespondingGuildPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.FindCorrespondingGuildPacket(P.行会编号, P.行会名字);
		}

		
		public void 处理封包(申请加入行会 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请加入行会(P.行会编号, P.行会名字);
		}

		
		public void 处理封包(查看申请列表 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查看申请列表();
		}

		
		public void 处理封包(处理入会申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.处理入会申请(P.对象编号, P.处理类型);
		}

		
		public void 处理封包(处理入会邀请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.处理入会邀请(P.对象编号, P.处理类型);
		}

		
		public void 处理封包(InviteToJoinGuildPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.InviteToJoinGuildPacket(P.对象名字);
		}

		
		public void 处理封包(申请创建行会 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请创建行会(P.字节数据);
		}

		
		public void 处理封包(申请解散行会 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请解散行会();
		}

		
		public void 处理封包(DonateGuildFundsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.DonateGuildFundsPacket(P.NumberGoldCoins);
		}

		
		public void 处理封包(DistributeGuildBenefitsPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.DistributeGuildBenefitsPacket();
		}

		
		public void 处理封包(申请离开行会 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请离开行会();
		}

		
		public void 处理封包(更改行会公告 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.更改行会公告(P.行会公告);
		}

		
		public void 处理封包(更改行会宣言 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.更改行会宣言(P.行会宣言);
		}

		
		public void 处理封包(设置行会禁言 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.设置行会禁言(P.对象编号, P.禁言状态);
		}

		
		public void 处理封包(变更会员职位 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.变更会员职位(P.对象编号, P.对象职位);
		}

		
		public void 处理封包(ExpelMembersPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.ExpelMembersPacket(P.对象编号);
		}

		
		public void 处理封包(TransferPresidentPositionPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.TransferPresidentPositionPacket(P.对象编号);
		}

		
		public void 处理封包(申请行会外交 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请行会外交(P.外交类型, P.外交时间, P.行会名字);
		}

		
		public void 处理封包(申请行会敌对 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请行会敌对(P.敌对时间, P.行会名字);
		}

		
		public void 处理封包(处理结盟申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.处理结盟申请(P.处理类型, P.行会编号);
		}

		
		public void 处理封包(申请解除结盟 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请解除结盟(P.行会编号);
		}

		
		public void 处理封包(申请解除敌对 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.申请解除敌对(P.行会编号);
		}

		
		public void 处理封包(处理解敌申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.处理解除申请(P.行会编号, P.回应类型);
		}

		
		public void 处理封包(更改存储权限 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(查看结盟申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查看结盟申请();
		}

		
		public void 处理封包(更多GuildEvents P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.更多GuildEvents();
		}

		
		public void 处理封包(QueryGuildAchievementsPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(开启行会活动 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(PublishWantedListPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(SyncedWantedListPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(StartGuildWarPacket P)
		{
			if (this.当前阶段 != GameStage.场景加载 && this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(查询地图路线 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询地图路线();
		}

		
		public void 处理封包(ToggleMapRoutePacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.ToggleMapRoutePacket();
		}

		
		public void 处理封包(跳过剧情动画 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(更改收徒推送 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.更改收徒推送(P.收徒推送);
		}

		
		public void 处理封包(查询师门成员 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询师门成员();
		}

		
		public void 处理封包(查询师门奖励 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询师门奖励();
		}

		
		public void 处理封包(查询拜师名册 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询拜师名册();
		}

		
		public void 处理封包(查询收徒名册 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询收徒名册();
		}

		
		public void 处理封包(CongratsToApprenticeForUpgradePacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(玩家申请拜师 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家申请拜师(P.对象编号);
		}

		
		public void 处理封包(同意拜师申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.同意拜师申请(P.对象编号);
		}

		
		public void 处理封包(RefusedApplyApprenticeshipPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.RefusedApplyApprenticeshipPacket(P.对象编号);
		}

		
		public void 处理封包(玩家申请收徒 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.玩家申请收徒(P.对象编号);
		}

		
		public void 处理封包(同意收徒申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.同意收徒申请(P.对象编号);
		}

		
		public void 处理封包(RejectionApprenticeshipAppPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.RejectionApprenticeshipAppPacket(P.对象编号);
		}

		
		public void 处理封包(AppForExpulsionPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.AppForExpulsionPacket(P.对象编号);
		}

		
		public void 处理封包(离开师门申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.离开师门申请();
		}

		
		public void 处理封包(提交出师申请 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.提交出师申请();
		}

		
		public void 处理封包(查询排名榜单 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询排名榜单(P.榜单类型, P.起始位置);
		}

		
		public void 处理封包(查看演武排名 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(刷新演武挑战 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(开始战场演武 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(EnterMartialArtsBatllefieldPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(跨服武道排名 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(LoginConsignmentPlatformPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.发送封包(new 社交错误提示
			{
				错误编号 = 12804
			});
		}

		
		public void 处理封包(查询平台商品 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.发送封包(new 社交错误提示
			{
				错误编号 = 12804
			});
		}

		
		public void 处理封包(InquireAboutSpecifiedProductPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.发送封包(new 社交错误提示
			{
				错误编号 = 12804
			});
		}

		
		public void 处理封包(上架平台商品 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.发送封包(new 社交错误提示
			{
				错误编号 = 12804
			});
		}

		
		public void 处理封包(RequestTreasureDataPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询珍宝商店(P.数据版本);
		}

		
		public void 处理封包(查询出售信息 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.查询出售信息();
		}

		
		public void 处理封包(购买珍宝商品 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.购买珍宝商品(P.Id, P.购买数量);
		}

		
		public void 处理封包(购买每周特惠 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.购买每周特惠(P.礼包编号);
		}

		
		public void 处理封包(购买玛法特权 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.购买玛法特权(P.特权类型, P.购买数量);
		}

		
		public void 处理封包(BookMarfaPrivilegesPacket P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.BookMarfaPrivilegesPacket(P.特权类型);
		}

		
		public void 处理封包(领取特权礼包 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定角色.领取特权礼包(P.特权类型, P.礼包位置);
		}

		
		public void 处理封包(玩家每日签到 P)
		{
			if (this.当前阶段 != GameStage.正在游戏)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
			}
		}

		
		public void 处理封包(客户账号登录 P)
		{
            if (this.当前阶段 != GameStage.正在登录)
            {
                this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
            }
            else if (SystemData.数据.网卡封禁.TryGetValue(P.物理地址, out DateTime t) && t > MainProcess.CurrentTime)
            {
                this.尝试断开连接(new Exception("网卡封禁, 限制登录"));
            }
            else if (!NetworkServiceGateway.门票DataSheet.TryGetValue(P.登录门票, out TicketInformation TicketInformation))
            {
                this.尝试断开连接(new Exception("登录的门票不存在."));
            }
            else if (MainProcess.CurrentTime > TicketInformation.EffectiveTime)
            {
                this.尝试断开连接(new Exception("登录门票已经过期."));
            }
            else
            {
                AccountData AccountData2;
                if (GameDataGateway.AccountData表.Keyword.TryGetValue(TicketInformation.登录账号, out GameData GameData))
                {
                    AccountData AccountData = GameData as AccountData;
                    if (AccountData != null)
                    {
                        AccountData2 = AccountData;
                        goto IL_EF;
                    }
                }
                AccountData2 = new AccountData(TicketInformation.登录账号);
            IL_EF:
                AccountData AccountData3 = AccountData2;
                if (AccountData3.网络连接 != null)
                {
                    AccountData3.网络连接.发送封包(new LoginErrorMessagePacket
                    {
                        错误代码 = 260U
                    });
                    AccountData3.网络连接.尝试断开连接(new Exception("账号重复登录, 被踢下线."));
                    this.尝试断开连接(new Exception("账号已经在线, 无法登录."));
                }
                else
                {
                    AccountData3.账号登录(this, P.物理地址);
                }
            }
            NetworkServiceGateway.门票DataSheet.Remove(P.登录门票);
		}

		
		public void 处理封包(客户创建角色 P)
		{
			if (this.当前阶段 != GameStage.选择角色)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.创建角色(this, P);
		}

		
		public void 处理封包(客户删除角色 P)
		{
			if (this.当前阶段 != GameStage.选择角色)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.删除角色(this, P);
		}

		
		public void 处理封包(彻底删除角色 P)
		{
			if (this.当前阶段 != GameStage.选择角色)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.永久删除(this, P);
		}

		
		public void 处理封包(客户进入游戏 P)
		{
			if (this.当前阶段 != GameStage.选择角色)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.进入游戏(this, P);
		}

		
		public void 处理封包(客户GetBackCharacterPacket P)
		{
			if (this.当前阶段 != GameStage.选择角色)
			{
				this.尝试断开连接(new Exception(string.Format("Phase exception, disconnected.  Processing packet: {0}, Current phase: {1}", P.GetType(), this.当前阶段)));
				return;
			}
			this.绑定账号.GetBackCharacter(this, P);
		}

		
		private DateTime 断开时间;

		
		private bool 正在发送;

		
		private byte[] 剩余数据;

		
		private readonly EventHandler<Exception> 断网事件;

		
		private ConcurrentQueue<GamePacket> 接收列表;

		
		private ConcurrentQueue<GamePacket> 发送列表;

		
		public bool 正在断开;

		
		public readonly DateTime 接入时间;

		
		public readonly TcpClient 当前连接;

		
		public GameStage 当前阶段;

		
		public AccountData 绑定账号;

		
		public PlayerObject 绑定角色;

		
		public string 网络地址;

		
		public string 物理地址;

		
		public int 发送总数;

		
		public int 接收总数;
	}
}
