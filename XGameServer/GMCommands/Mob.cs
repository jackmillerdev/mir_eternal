﻿using GameServer.Data;
using GameServer.Maps;
using GameServer.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.GMCommands
{
    public class Mob : GMCommand
    {
        public override ExecutionWay ExecutionWay => ExecutionWay.优先后台执行;

        public override void Execute()
        {
            if (!Monsters.DataSheet.TryGetValue(MobName, out Monsters monster))
            {
                SEnvir.AddCommandLog($"<= @Mob Command execution failed, mob {MobName} does not exist");
                return;
            }

            if (!GameMap.DataSheet.TryGetValue(MapId, out GameMap map))
            {
                SEnvir.AddCommandLog($"<= @Move Command execution failed, map {MapId} does not exist");
                return;
            }

            var mapInstance = MapGatewayProcess.分配地图(map.MapId);

            new MonsterObject(monster, mapInstance, int.MaxValue, new System.Drawing.Point[] { new System.Drawing.Point(MapX, MapY) }, true, true) { 存活时间 = MainProcess.CurrentTime.AddMinutes(1.0) };
        }

        [FieldAttribute(0, Position = 0)]
        public string MobName;

        [FieldAttribute(0, Position = 1)]
        public byte MapId;

        [FieldAttribute(0, Position = 2)]
        public int MapX;

        [FieldAttribute(0, Position = 3)]
        public int MapY;
    }
}
