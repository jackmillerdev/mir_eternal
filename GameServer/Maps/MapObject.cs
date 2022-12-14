using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using GameServer.Data;
using GameServer.Templates;
using GameServer.Networking;
using GamePackets.Server;

namespace GameServer.Maps
{

    public abstract class MapObject
    {

        public override string ToString()
        {
            return this.ObjectName;
        }

        public DateTime RecoveryTime { get; set; }
        public DateTime HealTime { get; set; }
        public DateTime TimeoutTime { get; set; }
        public DateTime CurrentTime { get; set; }
        public DateTime ProcessTime { get; set; }
        public virtual int ProcessInterval { get; }
        public int TreatmentCount { get; set; }
        public int TreatmentBase { get; set; }
        public byte ActionId { get; set; }
        public bool FightingStance { get; set; }
        public abstract GameObjectType ObjectType { get; }
        public abstract ObjectSize ObjectSize { get; }
        public ushort WalkSpeed => (ushort)this[GameObjectStats.WalkSpeed];
        public ushort RunSpeed => (ushort)this[GameObjectStats.RunSpeed];
        public virtual int WalkInterval => WalkSpeed * 60;
        public virtual int RunInterval => RunSpeed * 60;
        public virtual int ObjectId { get; set; }
        public virtual int CurrentHP { get; set; }
        public virtual int CurrentMP { get; set; }
        public virtual byte CurrentLevel { get; set; }
        public virtual bool Died { get; set; }
        public virtual bool Blocking { get; set; }
        public virtual bool CanBeHit => !this.Died;
        public virtual string ObjectName { get; set; }
        public virtual GameDirection CurrentDirection { get; set; }
        public virtual MapInstance CurrentMap { get; set; }
        public virtual Point CurrentPosition { get; set; }
        public virtual ushort CurrentAltitude => CurrentMap.GetTerrainHeight(CurrentPosition);
        public virtual DateTime BusyTime { get; set; }
        public virtual DateTime HardTime { get; set; }
        public virtual DateTime WalkTime { get; set; }
        public virtual DateTime RunTime { get; set; }
        public virtual Dictionary<GameObjectStats, int> Stats { get; }
        public virtual MonitorDictionary<int, DateTime> Coolings { get; }
        public virtual MonitorDictionary<ushort, BuffData> Buffs { get; }

        public bool SecondaryObject;
        public bool ActiveObject;
        public HashSet<MapObject> NeighborsImportant;
        public HashSet<MapObject> NeighborsSneak;
        public HashSet<MapObject> Neighbors;
        public HashSet<SkillInstance> SkillTasks;
        public HashSet<TrapObject> Traps;
        public Dictionary<object, Dictionary<GameObjectStats, int>> StatsBonus;

        public virtual int this[GameObjectStats Stat]
        {
            get
            {
                if (!Stats.ContainsKey(Stat))
                {
                    return 0;
                }
                return Stats[Stat];
            }
            set
            {
                Stats[Stat] = value;
                if (Stat == GameObjectStats.MaxHP)
                {
                    CurrentHP = Math.Min(CurrentHP, value);
                    return;
                }
                if (Stat == GameObjectStats.MaxMP)
                {
                    CurrentMP = Math.Min(CurrentMP, value);
                }
            }
        }

        public virtual void RefreshStats()
        {
            int num = 0;
            int num2 = 0;
            int num3 = 0;
            int num4 = 0;
            foreach (object obj in Enum.GetValues(typeof(GameObjectStats)))
            {
                int num5 = 0;
                GameObjectStats stat = (GameObjectStats)obj;
                foreach (KeyValuePair<object, Dictionary<GameObjectStats, int>> keyValuePair in StatsBonus)
                {
                    int num6;
                    if (keyValuePair.Value != null && keyValuePair.Value.TryGetValue(stat, out num6) && num6 != 0)
                    {
                        if (keyValuePair.Key is BuffData)
                        {
                            if (stat == GameObjectStats.WalkSpeed)
                            {
                                num2 = Math.Max(num2, num6);
                                num = Math.Min(num, num6);
                            }
                            else if (stat == GameObjectStats.RunSpeed)
                            {
                                num4 = Math.Max(num4, num6);
                                num3 = Math.Min(num3, num6);
                            }
                            else
                            {
                                num5 += num6;
                            }
                        }
                        else
                        {
                            num5 += num6;
                        }
                    }
                }
                if (stat == GameObjectStats.WalkSpeed)
                {
                    this[stat] = Math.Max(1, num5 + num + num2);
                }
                else if (stat == GameObjectStats.RunSpeed)
                {
                    this[stat] = Math.Max(1, num5 + num3 + num4);
                }
                else if (stat == GameObjectStats.Luck)
                {
                    this[stat] = num5;
                }
                else
                {
                    this[stat] = Math.Max(0, num5);
                }
            }

            if (this is PlayerObject playerObject)
            {
                foreach (PetObject petObject in playerObject.Pets)
                {
                    if (petObject.Template.InheritsStats != null)
                    {
                        var dictionary = new Dictionary<GameObjectStats, int>();
                        foreach (var InheritStat in petObject.Template.InheritsStats)
                        {
                            dictionary[InheritStat.ConvertStat] = (int)(this[InheritStat.InheritsStats] * InheritStat.Ratio);
                        }
                        petObject.StatsBonus[playerObject.CharacterData] = dictionary;
                        petObject.RefreshStats();
                    }
                }
            }
        }

        public virtual void Process()
        {
            CurrentTime = MainProcess.CurrentTime;
            ProcessTime = MainProcess.CurrentTime.AddMilliseconds(ProcessInterval);
        }

        public virtual void Dies(MapObject obj, bool skillKill)
        {
            SendPacket(new ObjectDiesPacket
            {
                ObjectId = ObjectId
            });

            SkillTasks.Clear();
            Died = true;
            Blocking = false;

            foreach (var neightborObject in Neighbors)
                neightborObject.NotifyObjectDies(this);
        }

        public MapObject()
        {
            CurrentTime = MainProcess.CurrentTime;
            SkillTasks = new HashSet<SkillInstance>();
            Traps = new HashSet<TrapObject>();
            NeighborsImportant = new HashSet<MapObject>();
            NeighborsSneak = new HashSet<MapObject>();
            Neighbors = new HashSet<MapObject>();
            Stats = new Dictionary<GameObjectStats, int>();
            Coolings = new MonitorDictionary<int, DateTime>(null);
            Buffs = new MonitorDictionary<ushort, BuffData>(null);
            StatsBonus = new Dictionary<object, Dictionary<GameObjectStats, int>>();
            ProcessTime = MainProcess.CurrentTime.AddMilliseconds(MainProcess.RandomNumber.Next(ProcessInterval));
        }

        public void UnbindGrid()
        {
            foreach (Point location in ComputingClass.GetLocationRange(CurrentPosition, CurrentDirection, ObjectSize))
                CurrentMap[location].Remove(this);
        }

        public void BindGrid()
        {
            foreach (Point location in ComputingClass.GetLocationRange(CurrentPosition, CurrentDirection, ObjectSize))
                CurrentMap[location].Add(this);
        }

        public void Delete()
        {
            NotifyNeightborClear();
            UnbindGrid();
            SecondaryObject = false;
            MapGatewayProcess.RemoveObject(this);
            ActiveObject = false;
            MapGatewayProcess.DeactivateObject(this);
        }

        public int GetDistance(Point location) => ComputingClass.GridDistance(this.CurrentPosition, location);

        public int GetDistance(MapObject obj) => ComputingClass.GridDistance(this.CurrentPosition, obj.CurrentPosition);

        public void SendPacket(GamePacket packet)
        {
            if (packet.PacketInfo.Broadcast)
                BroadcastPacket(packet);

            if (this is PlayerObject playerObj)
                playerObj.ActiveConnection?.SendPacket(packet);
        }

        private void BroadcastPacket(GamePacket packet)
        {
            foreach (MapObject obj in this.Neighbors)
            {
                PlayerObject PlayerObject = obj as PlayerObject;
                if (PlayerObject != null && !PlayerObject.NeighborsSneak.Contains(this) && PlayerObject != null)
                {
                    PlayerObject.ActiveConnection.SendPacket(packet);
                }
            }
        }

        public bool CanBeSeenBy(MapObject obj)
        {
            return Math.Abs(CurrentPosition.X - obj.CurrentPosition.X) <= 20
                && Math.Abs(CurrentPosition.Y - obj.CurrentPosition.Y) <= 20;
        }


        public bool CanAttack(MapObject obj)
        {
            if (obj.Died)
                return false;

            if (this is MonsterObject monsterObject)
            {
                return monsterObject.ActiveAttackTarget && (
                    obj is PlayerObject
                    || obj is PetObject
                    || (obj is GuardObject guardObject && guardObject.CanBeInjured)
                    );
            }
            else if (this is GuardObject guardObject)
            {
                return guardObject.ActiveAttackTarget
                    && (
                        (obj is MonsterObject neightborMonsterObject && neightborMonsterObject.ActiveAttackTarget)
                        || (obj is PlayerObject playerObject && playerObject.????????????)
                        || (obj is PetObject && guardObject.MobId == 6734)
                    );
            }
            else if (this is PetObject)
            {
                return obj is MonsterObject neightborMonsterObject
                    && neightborMonsterObject.ActiveAttackTarget;
            }

            return false;
        }

        public bool IsNeightbor(MapObject obj)
        {
            switch (ObjectType)
            {
                case GameObjectType.Player:
                    return true;
                case GameObjectType.Pet:
                case GameObjectType.Monster:
                    return obj.ObjectType == GameObjectType.Monster
                        || obj.ObjectType == GameObjectType.Player
                        || obj.ObjectType == GameObjectType.Pet
                        || obj.ObjectType == GameObjectType.NPC
                        || obj.ObjectType == GameObjectType.Trap;
                case GameObjectType.NPC:
                    return obj.ObjectType == GameObjectType.Monster
                        || obj.ObjectType == GameObjectType.Player
                        || obj.ObjectType == GameObjectType.Pet
                        || obj.ObjectType == GameObjectType.Trap;
                case GameObjectType.Item:
                    return obj.ObjectType == GameObjectType.Player;
                case GameObjectType.Trap:
                    return obj.ObjectType == GameObjectType.Player
                        || obj.ObjectType == GameObjectType.Pet;
            }

            return false;
        }

        public GameObjectRelationship GetRelationship(MapObject obj)
        {
            if (this == obj)
                return GameObjectRelationship.ItSelf;

            if (obj is TrapObject neightborTrapObject)
                obj = neightborTrapObject.TrapSource;

            if (this is GuardObject)
            {
                if (obj is MonsterObject || obj is PetObject || obj is PlayerObject)
                    return GameObjectRelationship.Hostility;
            }
            else if (this is PlayerObject playerObject)
            {
                if (obj is MonsterObject)
                    return GameObjectRelationship.Hostility;
                else if (obj is GuardObject)
                    return playerObject.AttackMode == AttackMode.?????? && CurrentMap.MapId != 80
                        ? GameObjectRelationship.Hostility
                        : GameObjectRelationship.Friendly;
                else if (obj is PlayerObject neightborPlayerObject)
                    return playerObject.AttackMode == AttackMode.??????
                        || (
                            playerObject.AttackMode == AttackMode.??????
                            && playerObject.Guild != null
                            && neightborPlayerObject.Guild != null
                            && (
                                playerObject.Guild == neightborPlayerObject.Guild
                                || playerObject.Guild.????????????.ContainsKey(neightborPlayerObject.Guild)
                            )
                        )
                        || (
                            playerObject.AttackMode == AttackMode.?????? && (
                                playerObject.Team != null && neightborPlayerObject.Team != null
                                && playerObject.Team == neightborPlayerObject.Team
                            )
                        )
                        || (
                            playerObject.AttackMode == AttackMode.?????? && (
                                !playerObject.???????????? && !neightborPlayerObject.????????????
                            )
                        )
                        || (
                            playerObject.AttackMode == AttackMode.Hostility && (
                                playerObject.Guild == null
                                || neightborPlayerObject == null
                                || !playerObject.Guild.Hostility??????.ContainsKey(neightborPlayerObject.Guild)
                            )
                        )
                        ? GameObjectRelationship.Friendly
                        : GameObjectRelationship.Hostility;
                else if (obj is PetObject petObject)
                    return (petObject.PlayerOwner == this && playerObject.AttackMode != AttackMode.??????)
                        || (playerObject.AttackMode == AttackMode.??????)
                        || (playerObject.AttackMode == AttackMode.?????? && playerObject.Guild != null && petObject.PlayerOwner.Guild != null && (playerObject.Guild == petObject.PlayerOwner.Guild || playerObject.Guild.????????????.ContainsKey(petObject.PlayerOwner.Guild)))
                        || (playerObject.AttackMode == AttackMode.?????? && playerObject.Team != null && petObject.PlayerOwner.Team != null && playerObject.Team == petObject.PlayerOwner.Team)
                        || (playerObject.AttackMode == AttackMode.?????? && !petObject.PlayerOwner.???????????? && !petObject.PlayerOwner.????????????)
                        || (playerObject.AttackMode != AttackMode.Hostility && (
                            playerObject.Guild == null
                            || petObject.PlayerOwner.Guild == null
                            || !playerObject.Guild.Hostility??????.ContainsKey(petObject.PlayerOwner.Guild)
                        ))
                        ? GameObjectRelationship.Friendly
                        : petObject.PlayerOwner == this
                            ? GameObjectRelationship.Friendly | GameObjectRelationship.Hostility
                            : GameObjectRelationship.Hostility;

            }
            else if (this is PetObject petObject)
                return petObject.PlayerOwner != obj
                    ? petObject.PlayerOwner.GetRelationship(obj)
                    : GameObjectRelationship.Friendly;
            else if (this is TrapObject trapObject)
                return trapObject.TrapSource.GetRelationship(obj);
            else if (obj is not MonsterObject)
                return GameObjectRelationship.Hostility;

            return GameObjectRelationship.Friendly;
        }

        public bool IsSpecificType(MapObject obj, SpecifyTargetType targetType)
        {
            if (obj is TrapObject trapObject)
                obj = trapObject.TrapSource;

            var targetDirection = ComputingClass.GetDirection(obj.CurrentPosition, CurrentPosition);

            if (this is MonsterObject monsterObject)
            {
                return targetType == SpecifyTargetType.None
                    || (targetType & SpecifyTargetType.LowLevelTarget) == SpecifyTargetType.LowLevelTarget && CurrentLevel < obj.CurrentLevel
                    || (targetType & SpecifyTargetType.AllMonsters) == SpecifyTargetType.AllMonsters
                    || (targetType & SpecifyTargetType.LowLevelMonster) == SpecifyTargetType.LowLevelMonster && CurrentLevel < obj.CurrentLevel
                    || ((targetType & SpecifyTargetType.LowBloodMonster) == SpecifyTargetType.LowBloodMonster && (float)this.CurrentHP / (float)this[GameObjectStats.MaxHP] < 0.4f)
                    || ((targetType & SpecifyTargetType.Normal) == SpecifyTargetType.Normal && monsterObject.Category == MonsterLevelType.Normal)
                    || ((targetType & SpecifyTargetType.Undead) == SpecifyTargetType.Undead && monsterObject.???????????? == MonsterRaceType.Undead)
                    || ((targetType & SpecifyTargetType.ZergCreature) == SpecifyTargetType.ZergCreature && monsterObject.???????????? == MonsterRaceType.ZergCreature)
                    || ((targetType & SpecifyTargetType.WomaMonster) == SpecifyTargetType.WomaMonster && monsterObject.???????????? == MonsterRaceType.WomaMonster)
                    || ((targetType & SpecifyTargetType.PigMonster) == SpecifyTargetType.PigMonster && monsterObject.???????????? == MonsterRaceType.PigMonster)
                    || ((targetType & SpecifyTargetType.ZumaMonster) == SpecifyTargetType.ZumaMonster && monsterObject.???????????? == MonsterRaceType.ZumaMonster)
                    || ((targetType & SpecifyTargetType.DragonMonster) == SpecifyTargetType.DragonMonster && monsterObject.???????????? == MonsterRaceType.DragonMonster)
                    || ((targetType & SpecifyTargetType.EliteMonsters) == SpecifyTargetType.EliteMonsters && (monsterObject.Category == MonsterLevelType.Elite || monsterObject.Category == MonsterLevelType.Boss))
                    || (((targetType & SpecifyTargetType.Backstab) == SpecifyTargetType.Backstab) && (
                            (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                        )
                    );
            }
            else if (this is GuardObject)
            {
                return targetType == SpecifyTargetType.None
                    || ((targetType & SpecifyTargetType.LowLevelTarget) == SpecifyTargetType.LowLevelTarget && CurrentLevel < obj.CurrentLevel)
                    || (((targetType & SpecifyTargetType.Backstab) == SpecifyTargetType.Backstab) && (
                       (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                    ));
            }
            else if (this is PetObject petObject)
            {
                return targetType == SpecifyTargetType.None
                    || ((targetType & SpecifyTargetType.LowLevelTarget) == SpecifyTargetType.LowLevelTarget && this.CurrentLevel < obj.CurrentLevel)
                    || ((targetType & SpecifyTargetType.Undead) == SpecifyTargetType.Undead && petObject.???????????? == MonsterRaceType.Undead)
                    || ((targetType & SpecifyTargetType.ZergCreature) == SpecifyTargetType.ZergCreature && petObject.???????????? == MonsterRaceType.ZergCreature)
                    || ((targetType & SpecifyTargetType.AllPets) == SpecifyTargetType.AllPets)
                    || (((targetType & SpecifyTargetType.Backstab) == SpecifyTargetType.Backstab) && (
                     (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                    ));
            }
            else if (this is PlayerObject playerObject)
            {
                return targetType == SpecifyTargetType.None
                || ((targetType & SpecifyTargetType.LowLevelTarget) == SpecifyTargetType.LowLevelTarget && this.CurrentLevel < obj.CurrentLevel)
                    || ((targetType & SpecifyTargetType.ShieldMage) == SpecifyTargetType.ShieldMage && playerObject.CharRole == GameObjectRace.?????? && playerObject.Buffs.ContainsKey(25350))
                    || (((targetType & SpecifyTargetType.Backstab) == SpecifyTargetType.Backstab) && (
                     (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                            || (CurrentDirection == GameDirection.?????? && (targetDirection == GameDirection.?????? || targetDirection == GameDirection.?????? || targetDirection == GameDirection.??????))
                    ));
            }

            return false;
        }


        public virtual bool CanMove()
        {
            return !Died && !(MainProcess.CurrentTime < BusyTime) && !(MainProcess.CurrentTime < WalkTime) && !CheckStatus(GameObjectState.BusyGreen | GameObjectState.Inmobilized | GameObjectState.Paralyzed | GameObjectState.Absence);
        }

        public virtual bool CanRun()
        {
            return !Died && !(MainProcess.CurrentTime < BusyTime) && !(MainProcess.CurrentTime < this.RunTime) && !CheckStatus(GameObjectState.BusyGreen | GameObjectState.Disabled | GameObjectState.Inmobilized | GameObjectState.Paralyzed | GameObjectState.Absence);
        }

        public virtual bool CanBeTurned()
        {
            return !Died && !(MainProcess.CurrentTime < BusyTime) && !(MainProcess.CurrentTime < WalkTime) && !CheckStatus(GameObjectState.BusyGreen | GameObjectState.Paralyzed | GameObjectState.Absence);
        }

        public virtual bool CanBePushed(MapObject obj)
        {
            return obj == this
                || (
                    this is MonsterObject monsterObject
                    && monsterObject.CanBeDrivenBySkills
                    && obj.GetRelationship(this) == GameObjectRelationship.Hostility
                );
        }


        public virtual bool CanBeDisplaced(MapObject obj, Point location, int distance, int qty, bool throughtWall, out Point displacedLocation, out MapObject[] targets)
        {
            displacedLocation = CurrentPosition;
            targets = null;

            if (!(CurrentPosition == location) && CanBePushed(obj))
            {
                List<MapObject> list = new List<MapObject>();
                for (int i = 1; i <= distance; i++)
                {
                    if (throughtWall)
                    {
                        Point point = ComputingClass.GetFrontPosition(CurrentPosition, location, i);
                        if (CurrentMap.CanPass(point))
                        {
                            displacedLocation = point;
                        }
                        continue;
                    }
                    GameDirection ?????? = ComputingClass.GetDirection(CurrentPosition, location);
                    Point point2 = ComputingClass.GetFrontPosition(CurrentPosition, location, i);
                    if (CurrentMap.IsBlocked(point2))
                    {
                        break;
                    }
                    bool flag = false;
                    if (CurrentMap.CellBlocked(point2))
                    {
                        foreach (MapObject item in CurrentMap[point2].Where((MapObject O) => O.Blocking))
                        {
                            if (list.Count >= qty)
                            {
                                flag = true;
                                break;
                            }
                            if (!item.CanBeDisplaced(obj, ComputingClass.????????????(item.CurrentPosition, ??????, 1), 1, qty - list.Count - 1, throughtWall: false, out var _, out var targets2))
                            {
                                flag = true;
                                break;
                            }
                            list.Add(item);
                            list.AddRange(targets2);
                        }
                    }
                    if (flag)
                    {
                        break;
                    }
                    displacedLocation = point2;
                }
                targets = list.ToArray();
                return displacedLocation != CurrentPosition;
            }
            return false;
        }

        public virtual bool CheckStatus(GameObjectState state)
        {
            foreach (BuffData BuffData in this.Buffs.Values)
            {
                if ((BuffData.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign && (BuffData.Template.PlayerState & state) != GameObjectState.Normal)
                {
                    return true;
                }
            }
            return false;
        }


        public void OnAddBuff(ushort buffId, MapObject obj)
        {
            if (this is ItemObject || this is TrapObject || (this is GuardObject guardObject && !guardObject.CanBeInjured))
                return;

            if (obj is TrapObject trapObject)
                obj = trapObject.TrapSource;


            if (!GameBuffs.DataSheet.TryGetValue(buffId, out var ??????Buff))
                return;

            if ((??????Buff.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign)
            {
                if (((??????Buff.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal || (??????Buff.PlayerState & GameObjectState.StealthStatus) != GameObjectState.Normal) && this.CheckStatus(GameObjectState.Exposed))
                    return;

                if ((??????Buff.PlayerState & GameObjectState.Exposed) != GameObjectState.Normal)
                {
                    foreach (BuffData BuffData in this.Buffs.Values.ToList<BuffData>())
                    {
                        if ((BuffData.Template.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal || (BuffData.Template.PlayerState & GameObjectState.StealthStatus) != GameObjectState.Normal)
                        {
                            ??????Buff?????????(BuffData.Id.V);
                        }
                    }
                }
            }

            if ((??????Buff.Effect & BuffEffectType.CausesSomeDamages) != BuffEffectType.SkillSign && ??????Buff.DamageType == SkillDamageType.Burn && this.Buffs.ContainsKey(25352))
                return;

            ushort GroupId = (??????Buff.GroupId != 0) ? ??????Buff.GroupId : ??????Buff.Id;
            BuffData BuffData2 = null;
            switch (??????Buff.OverlayType)
            {
                case BuffOverlayType.SuperpositionDisabled:
                    if (this.Buffs.Values.FirstOrDefault((BuffData O) => O.Buff?????? == GroupId) == null)
                    {
                        BuffData2 = (this.Buffs[??????Buff.Id] = new BuffData(obj, this, ??????Buff.Id));
                    }
                    break;
                case BuffOverlayType.SimilarReplacement:
                    {
                        foreach (var BuffData3 in Buffs.Values.Where(O => O.Buff?????? == GroupId).ToList())
                        {
                            ??????Buff?????????(BuffData3.Id.V);
                        }
                        BuffData2 = (this.Buffs[??????Buff.Id] = new BuffData(obj, this, ??????Buff.Id));
                        break;
                    }
                case BuffOverlayType.HomogeneousStacking:
                    {
                        if (!Buffs.TryGetValue(buffId, out var BuffData4))
                        {
                            BuffData2 = (this.Buffs[??????Buff.Id] = new BuffData(obj, this, ??????Buff.Id));
                            break;
                        }

                        BuffData4.????????????.V = (byte)Math.Min(BuffData4.????????????.V + 1, BuffData4.????????????);
                        if (??????Buff.AllowsSynthesis && BuffData4.????????????.V >= ??????Buff.BuffSynthesisLayer && GameBuffs.DataSheet.TryGetValue(??????Buff.BuffSynthesisId, out var ??????Buff2))
                        {
                            ??????Buff?????????(BuffData4.Id.V);
                            OnAddBuff(??????Buff.BuffSynthesisId, obj);
                            break;
                        }

                        BuffData4.????????????.V = BuffData4.????????????.V;
                        if (BuffData4.Buff??????)
                        {
                            SendPacket(new ObjectStateChangePacket
                            {
                                ???????????? = this.ObjectId,
                                Id = BuffData4.Id.V,
                                Buff?????? = (int)BuffData4.Id.V,
                                ???????????? = BuffData4.????????????.V,
                                ???????????? = (int)BuffData4.????????????.V.TotalMilliseconds,
                                ???????????? = (int)BuffData4.????????????.V.TotalMilliseconds
                            });
                        }
                        break;
                    }
                case BuffOverlayType.SimilarDelay:
                    {
                        if (Buffs.TryGetValue(buffId, out var BuffData5))
                        {
                            BuffData5.????????????.V += BuffData5.????????????.V;
                            if (BuffData5.Buff??????)
                            {
                                SendPacket(new ObjectStateChangePacket
                                {
                                    ???????????? = this.ObjectId,
                                    Id = BuffData5.Id.V,
                                    Buff?????? = (int)BuffData5.Id.V,
                                    ???????????? = BuffData5.????????????.V,
                                    ???????????? = (int)BuffData5.????????????.V.TotalMilliseconds,
                                    ???????????? = (int)BuffData5.????????????.V.TotalMilliseconds
                                });
                            }
                        }
                        else
                        {
                            BuffData2 = (this.Buffs[??????Buff.Id] = new BuffData(obj, this, ??????Buff.Id));
                        }
                        break;
                    }
            }

            if (BuffData2 == null)
                return;


            if (BuffData2.Buff??????)
            {
                SendPacket(new ObjectAddStatePacket
                {
                    SourceObjectId = this.ObjectId,
                    TargetObjectId = obj.ObjectId,
                    BuffId = BuffData2.Id.V,
                    BuffIndex = (int)BuffData2.Id.V,
                    Duration = (int)BuffData2.????????????.V.TotalMilliseconds,
                    BuffLayers = BuffData2.????????????.V,
                });
            }

            if ((??????Buff.Effect & BuffEffectType.StatsIncOrDec) != BuffEffectType.SkillSign)
            {
                StatsBonus.Add(BuffData2, BuffData2.Stat??????);
                RefreshStats();
            }

            if ((??????Buff.Effect & BuffEffectType.Riding) != BuffEffectType.SkillSign && this is PlayerObject playerObject)
            {
                if (GameMounts.DataSheet.TryGetValue(playerObject.CharacterData.CurrentMount.V, out GameMounts mount))
                {
                    playerObject.Riding = true;
                    StatsBonus.Add(BuffData2, mount.Stats);
                    RefreshStats();
                    if (mount.SoulAuraID > 0)
                        playerObject.OnAddBuff(mount.SoulAuraID, this);
                }

                SendPacket(new SyncObjectMountPacket
                {
                    ObjectId = ObjectId,
                    MountId = (byte)playerObject.CharacterData.CurrentMount.V
                });
            }

            if ((??????Buff.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign)
            {
                if ((??????Buff.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal)
                {
                    foreach (MapObject MapObject in this.Neighbors.ToList<MapObject>())
                    {
                        MapObject.?????????????????????(this);
                    }
                }
                if ((??????Buff.PlayerState & GameObjectState.StealthStatus) != GameObjectState.Normal)
                {
                    foreach (MapObject MapObject2 in this.Neighbors.ToList<MapObject>())
                    {
                        MapObject2.?????????????????????(this);
                    }
                }
            }
            if (??????Buff.AssociatedId != 0)
            {
                OnAddBuff(??????Buff.AssociatedId, obj);
            }
        }


        public void ??????Buff?????????(ushort ??????)
        {
            BuffData BuffData;
            if (this.Buffs.TryGetValue(??????, out BuffData))
            {
                MapObject MapObject;
                if (BuffData.Template.FollowedById != 0 && BuffData.Buff?????? != null && MapGatewayProcess.Objects.TryGetValue(BuffData.Buff??????.ObjectId, out MapObject) && MapObject == BuffData.Buff??????)
                {
                    this.OnAddBuff(BuffData.Template.FollowedById, BuffData.Buff??????);
                }
                if (BuffData.???????????? != null)
                {
                    foreach (ushort ??????2 in BuffData.????????????)
                    {
                        this.??????Buff?????????(??????2);
                    }
                }
                if (BuffData.???????????? && BuffData.???????????? != 0 && BuffData.Cooldown != 0)
                {
                    PlayerObject PlayerObject = this as PlayerObject;
                    if (PlayerObject != null && PlayerObject.MainSkills???.ContainsKey(BuffData.????????????))
                    {
                        DateTime dateTime = MainProcess.CurrentTime.AddMilliseconds((double)BuffData.Cooldown);
                        DateTime t = this.Coolings.ContainsKey((int)BuffData.???????????? | 16777216) ? this.Coolings[(int)BuffData.???????????? | 16777216] : default(DateTime);
                        if (dateTime > t)
                        {
                            this.Coolings[(int)BuffData.???????????? | 16777216] = dateTime;
                            this.SendPacket(new AddedSkillCooldownPacket
                            {
                                CoolingId = ((int)BuffData.???????????? | 16777216),
                                Cooldown = (int)BuffData.Cooldown
                            });
                        }
                    }
                }
                this.Buffs.Remove(??????);
                BuffData.Delete();
                if (BuffData.Buff??????)
                {
                    this.SendPacket(new ObjectRemovalStatusPacket
                    {
                        ???????????? = this.ObjectId,
                        Buff?????? = (int)??????
                    });
                }
                if ((BuffData.Effect & BuffEffectType.StatsIncOrDec) != BuffEffectType.SkillSign)
                {
                    this.StatsBonus.Remove(BuffData);
                    this.RefreshStats();
                }

                if ((BuffData.Effect & BuffEffectType.Riding) != BuffEffectType.SkillSign && this is PlayerObject playerObject)
                {
                    playerObject.Riding = false;
                    this.StatsBonus.Remove(BuffData);
                    this.RefreshStats();
                    if (GameMounts.DataSheet.TryGetValue(playerObject.CharacterData.CurrentMount.V, out GameMounts mount))
                        if (mount.SoulAuraID > 0) playerObject.??????Buff?????????(mount.SoulAuraID);
                }

                if ((BuffData.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign)
                {
                    if ((BuffData.Template.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal)
                    {
                        foreach (MapObject MapObject2 in this.Neighbors.ToList<MapObject>())
                        {
                            MapObject2.?????????????????????(this);
                        }
                    }
                    if ((BuffData.Template.PlayerState & GameObjectState.StealthStatus) != GameObjectState.Normal)
                    {
                        foreach (MapObject MapObject3 in this.Neighbors.ToList<MapObject>())
                        {
                            MapObject3.?????????????????????(this);
                        }
                    }
                }
            }
        }


        public void ??????Buff?????????(ushort ??????)
        {
            BuffData BuffData;
            if (this.Buffs.TryGetValue(??????, out BuffData))
            {
                if (BuffData.???????????? != null)
                {
                    foreach (ushort ??????2 in BuffData.????????????)
                    {
                        this.??????Buff?????????(??????2);
                    }
                }
                this.Buffs.Remove(??????);
                BuffData.Delete();
                if (BuffData.Buff??????)
                {
                    this.SendPacket(new ObjectRemovalStatusPacket
                    {
                        ???????????? = this.ObjectId,
                        Buff?????? = (int)??????
                    });
                }
                if ((BuffData.Effect & BuffEffectType.StatsIncOrDec) != BuffEffectType.SkillSign)
                {
                    this.StatsBonus.Remove(BuffData);
                    this.RefreshStats();
                }
                if ((BuffData.Effect & BuffEffectType.Riding) != BuffEffectType.SkillSign && this is PlayerObject playerObject)
                {
                    playerObject.Riding = false;
                    this.StatsBonus.Remove(BuffData);
                    this.RefreshStats();
                    if (GameMounts.DataSheet.TryGetValue(playerObject.CharacterData.CurrentMount.V, out GameMounts mount))
                        if (mount.SoulAuraID > 0) playerObject.??????Buff?????????(mount.SoulAuraID);
                }
                if ((BuffData.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign)
                {
                    if ((BuffData.Template.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal)
                    {
                        foreach (MapObject MapObject in this.Neighbors.ToList<MapObject>())
                        {
                            MapObject.?????????????????????(this);
                        }
                    }
                    if ((BuffData.Template.PlayerState & GameObjectState.StealthStatus) != GameObjectState.Normal)
                    {
                        foreach (MapObject MapObject2 in this.Neighbors.ToList<MapObject>())
                        {
                            MapObject2.?????????????????????(this);
                        }
                    }
                }
            }
        }


        public void ??????Buff?????????(BuffData ??????)
        {
            if (??????.???????????? && (??????.????????????.V -= MainProcess.CurrentTime - this.CurrentTime) < TimeSpan.Zero)
            {
                this.??????Buff?????????(??????.Id.V);
                return;
            }
            if ((??????.????????????.V -= MainProcess.CurrentTime - this.CurrentTime) < TimeSpan.Zero)
            {
                ??????.????????????.V += TimeSpan.FromMilliseconds((double)??????.????????????);
                if ((??????.Effect & BuffEffectType.CausesSomeDamages) != BuffEffectType.SkillSign)
                {
                    this.?????????????????????(??????);
                }
                if ((??????.Effect & BuffEffectType.LifeRecovery) != BuffEffectType.SkillSign)
                {
                    this.?????????????????????(??????);
                }
            }
        }


        public void ProcessSkillHit(SkillInstance skill, C_01_CalculateHitTarget info)
        {
            MapObject obj = skill.CasterObject is TrapObject trap
                ? trap.TrapSource
                : skill.CasterObject;

            if (skill.Hits.ContainsKey(ObjectId) || !CanBeHit)
                return;

            if (this != obj && !Neighbors.Contains(obj))
                return;

            if (skill.Hits.Count >= info.HitsLimit)
                return;

            if ((info.LimitedTargetRelationship & obj.GetRelationship(this)) == 0)
                return;

            if ((info.LimitedTargetType & ObjectType) == 0)
                return;

            if (!IsSpecificType(skill.CasterObject, info.QualifySpecificType))
                return;

            if ((info.LimitedTargetRelationship & GameObjectRelationship.Hostility) != 0)
            {
                if (CheckStatus(GameObjectState.Invencible))
                    return;

                if ((this is PlayerObject || this is PetObject) && (obj is PlayerObject || obj is PetObject) && (CurrentMap.IsSafeZone(CurrentPosition) || obj.CurrentMap.IsSafeZone(obj.CurrentPosition)))
                    return;

                if (obj is MonsterObject && CurrentMap.IsSafeZone(CurrentPosition))
                    return;
            }

            // TODO: Sabak Gates (move some flag to database to remove hardcoded MonsterId)
            if (this is MonsterObject monsterObject && (monsterObject.MonsterId == 8618 || monsterObject.MonsterId == 8621))
            {
                if (obj is PlayerObject playerObject && playerObject.Guild != null && playerObject.Guild == SystemData.Data.OccupyGuild.V)
                    return;

                if (obj is PetObject petObject && petObject.PlayerOwner != null && petObject.PlayerOwner.Guild != null && petObject.PlayerOwner.Guild == SystemData.Data.OccupyGuild.V)
                    return;
            }

            int num = 0;
            float num2 = 0f;
            int num3 = 0;
            float num4 = 0f;

            switch (info.SkillEvasion)
            {
                case SkillEvasionType.SkillCannotBeEvaded:
                    num = 1;
                    break;
                case SkillEvasionType.CanBePhsyicallyEvaded:
                    num3 = this[GameObjectStats.PhysicalAgility];
                    num = obj[GameObjectStats.PhysicallyAccurate];
                    if (this is MonsterObject)
                    {
                        num2 += obj[GameObjectStats.????????????] / 10000f;
                        num4 += this[GameObjectStats.????????????] / 10000f;
                    }
                    break;
                case SkillEvasionType.CanBeMagicEvaded:
                    num4 = this[GameObjectStats.MagicDodge] / 10000f;
                    if (this is MonsterObject)
                    {
                        num2 += obj[GameObjectStats.????????????] / 10000f;
                        num4 += this[GameObjectStats.????????????] / 10000f;
                    }
                    break;
                case SkillEvasionType.CanBePoisonEvaded:
                    num4 = this[GameObjectStats.????????????] / 10000f;
                    break;
                case SkillEvasionType.NonMonstersCanEvaded:
                    if (this is MonsterObject)
                        num = 1;
                    else
                    {
                        num3 = this[GameObjectStats.PhysicalAgility];
                        num = obj[GameObjectStats.PhysicallyAccurate];
                    }
                    break;
            }

            var value = new HitDetail(this)
            {
                Feedback = ComputingClass.IsHit(num, num3, num2, num4) ? info.SkillHitFeedback : SkillHitFeedback.Miss
            };

            skill.Hits.Add(this.ObjectId, value);
        }


        public void ?????????????????????(SkillInstance ??????, C_02_CalculateTargetDamage ??????, HitDetail ??????, float ????????????)
        {
            TrapObject TrapObject = ??????.CasterObject as TrapObject;
            MapObject MapObject = (TrapObject != null) ? TrapObject.TrapSource : ??????.CasterObject;
            if (this.Died)
            {
                ??????.Feedback = SkillHitFeedback.??????;
            }
            else if (!this.Neighbors.Contains(MapObject))
            {
                ??????.Feedback = SkillHitFeedback.??????;
            }
            else if ((MapObject.GetRelationship(this) & GameObjectRelationship.Hostility) == (GameObjectRelationship)0)
            {
                ??????.Feedback = SkillHitFeedback.??????;
            }
            else
            {
                MonsterObject MonsterObject = this as MonsterObject;
                if (MonsterObject != null && (MonsterObject.MonsterId == 8618 || MonsterObject.MonsterId == 8621) && this.GetDistance(MapObject) >= 4)
                {
                    ??????.Feedback = SkillHitFeedback.??????;
                }
            }
            if ((??????.Feedback & SkillHitFeedback.??????) == SkillHitFeedback.?????? && (??????.Feedback & SkillHitFeedback.??????) == SkillHitFeedback.??????)
            {
                if ((??????.Feedback & SkillHitFeedback.Miss) == SkillHitFeedback.??????)
                {
                    if (??????.?????????????????? != SpecifyTargetType.None && ComputingClass.CheckProbability(??????.??????????????????) && this.IsSpecificType(MapObject, ??????.??????????????????))
                    {
                        ??????.Damage = this.CurrentHP;
                    }
                    else
                    {
                        int[] ?????????????????? = ??????.??????????????????;
                        int? num = (?????????????????? != null) ? new int?(??????????????????.Length) : null;
                        int num2 = (int)??????.SkillLevel;
                        int num3 = (num.GetValueOrDefault() > num2 & num != null) ? ??????.??????????????????[(int)??????.SkillLevel] : 0;
                        float[] ?????????????????? = ??????.??????????????????;
                        num = ((?????????????????? != null) ? new int?(??????????????????.Length) : null);
                        num2 = (int)??????.SkillLevel;
                        float num4 = (num.GetValueOrDefault() > num2 & num != null) ? ??????.??????????????????[(int)??????.SkillLevel] : 0f;
                        if (this is MonsterObject)
                        {
                            num3 += MapObject[GameObjectStats.????????????];
                        }
                        int num5 = 0;
                        float num6 = 0f;
                        if (??????.?????????????????? != SpecifyTargetType.None && this.IsSpecificType(MapObject, ??????.??????????????????))
                        {
                            num5 = ??????.??????????????????;
                            num6 = ??????.??????????????????;
                        }
                        int num7 = 0;
                        float num8 = 0f;
                        if (??????.?????????????????? > 0f && ComputingClass.CheckProbability(??????.??????????????????))
                        {
                            num7 = ??????.??????????????????;
                            num8 = ??????.??????????????????;
                        }
                        int num9 = 0;
                        int num10 = 0;
                        switch (??????.??????????????????)
                        {
                            case SkillDamageType.Attack:
                                num10 = ComputingClass.????????????(this[GameObjectStats.MinDef], this[GameObjectStats.MaxDef]);
                                num9 = ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinDC], MapObject[GameObjectStats.MaxDC], MapObject[GameObjectStats.Luck]);
                                break;
                            case SkillDamageType.Magic:
                                num10 = ComputingClass.????????????(this[GameObjectStats.MinMCDef], this[GameObjectStats.MaxMCDef]);
                                num9 = ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinMC], MapObject[GameObjectStats.MaxMC], MapObject[GameObjectStats.Luck]);
                                break;
                            case SkillDamageType.Taoism:
                                num10 = ComputingClass.????????????(this[GameObjectStats.MinMCDef], this[GameObjectStats.MaxMCDef]);
                                num9 = ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinSC], MapObject[GameObjectStats.MaxSC], MapObject[GameObjectStats.Luck]);
                                break;
                            case SkillDamageType.Needle:
                                num10 = ComputingClass.????????????(this[GameObjectStats.MinDef], this[GameObjectStats.MaxDef]);
                                num9 = ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinNC], MapObject[GameObjectStats.MaxNC], MapObject[GameObjectStats.Luck]);
                                break;
                            case SkillDamageType.Archery:
                                num10 = ComputingClass.????????????(this[GameObjectStats.MinDef], this[GameObjectStats.MaxDef]);
                                num9 = ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinBC], MapObject[GameObjectStats.MaxBC], MapObject[GameObjectStats.Luck]);
                                break;
                            case SkillDamageType.Toxicity:
                                num9 = MapObject[GameObjectStats.MaxSC];
                                break;
                            case SkillDamageType.Sacred:
                                num9 = ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinHC], MapObject[GameObjectStats.MaxHC], 0);
                                break;
                        }
                        if (this is MonsterObject)
                        {
                            num10 = Math.Max(0, num10 - (int)((float)(num10 * MapObject[GameObjectStats.????????????]) / 10000f));
                        }
                        int num11 = 0;
                        float num12 = 0f;
                        int num13 = int.MaxValue;
                        foreach (BuffData BuffData in MapObject.Buffs.Values.ToList<BuffData>())
                        {
                            if ((BuffData.Effect & BuffEffectType.DamageIncOrDec) != BuffEffectType.SkillSign && (BuffData.Template.HowJudgeEffect == BuffDetherminationMethod.ActiveAttackDamageBoost || BuffData.Template.HowJudgeEffect == BuffDetherminationMethod.ActiveAttackDamageReduction))
                            {
                                bool flag = false;
                                switch (??????.??????????????????)
                                {
                                    case SkillDamageType.Attack:
                                    case SkillDamageType.Needle:
                                    case SkillDamageType.Archery:
                                        {
                                            BuffJudgmentType EffectJudgeType = BuffData.Template.EffectJudgeType;
                                            if (EffectJudgeType > BuffJudgmentType.AllPhysicalDamage)
                                            {
                                                if (EffectJudgeType == BuffJudgmentType.AllSpecificInjuries)
                                                {
                                                    HashSet<ushort> SpecificSkillId = BuffData.Template.SpecificSkillId;
                                                    flag = (SpecificSkillId != null && SpecificSkillId.Contains(??????.SkillId));
                                                }
                                            }
                                            else
                                            {
                                                flag = true;
                                            }
                                            break;
                                        }
                                    case SkillDamageType.Magic:
                                    case SkillDamageType.Taoism:
                                        switch (BuffData.Template.EffectJudgeType)
                                        {
                                            case BuffJudgmentType.AllSkillDamage:
                                            case BuffJudgmentType.AllMagicDamage:
                                                flag = true;
                                                break;
                                            case BuffJudgmentType.AllSpecificInjuries:
                                                {
                                                    HashSet<ushort> SpecificSkillId2 = BuffData.Template.SpecificSkillId;
                                                    flag = (SpecificSkillId2 != null && SpecificSkillId2.Contains(??????.SkillId));
                                                    break;
                                                }
                                        }
                                        break;
                                    case SkillDamageType.Toxicity:
                                    case SkillDamageType.Sacred:
                                    case SkillDamageType.Burn:
                                    case SkillDamageType.Tear:
                                        if (BuffData.Template.EffectJudgeType == BuffJudgmentType.AllSpecificInjuries)
                                        {
                                            HashSet<ushort> SpecificSkillId3 = BuffData.Template.SpecificSkillId;
                                            flag = (SpecificSkillId3 != null && SpecificSkillId3.Contains(??????.SkillId));
                                        }
                                        break;
                                }
                                if (flag)
                                {
                                    int v = (int)BuffData.????????????.V;
                                    int[] DamageIncOrDecBase = BuffData.Template.DamageIncOrDecBase;
                                    num = ((DamageIncOrDecBase != null) ? new int?(DamageIncOrDecBase.Length) : null);
                                    num2 = (int)BuffData.Buff??????.V;
                                    int num14 = v * ((num.GetValueOrDefault() > num2 & num != null) ? BuffData.Template.DamageIncOrDecBase[(int)BuffData.Buff??????.V] : 0);
                                    float num15 = (float)BuffData.????????????.V;
                                    float[] DamageIncOrDecFactor = BuffData.Template.DamageIncOrDecFactor;
                                    num = ((DamageIncOrDecFactor != null) ? new int?(DamageIncOrDecFactor.Length) : null);
                                    num2 = (int)BuffData.Buff??????.V;
                                    float num16 = num15 * ((num.GetValueOrDefault() > num2 & num != null) ? BuffData.Template.DamageIncOrDecFactor[(int)BuffData.Buff??????.V] : 0f);
                                    num11 += ((BuffData.Template.HowJudgeEffect == BuffDetherminationMethod.ActiveAttackDamageBoost) ? num14 : (-num14));
                                    num12 += ((BuffData.Template.HowJudgeEffect == BuffDetherminationMethod.ActiveAttackDamageBoost) ? num16 : (-num16));
                                    MapObject MapObject2;
                                    if (BuffData.Template.EffectiveFollowedById != 0 && BuffData.Buff?????? != null && MapGatewayProcess.Objects.TryGetValue(BuffData.Buff??????.ObjectId, out MapObject2) && MapObject2 == BuffData.Buff??????)
                                    {
                                        if (BuffData.Template.FollowUpSkillSource)
                                        {
                                            MapObject.OnAddBuff(BuffData.Template.EffectiveFollowedById, BuffData.Buff??????);
                                        }
                                        else
                                        {
                                            this.OnAddBuff(BuffData.Template.EffectiveFollowedById, BuffData.Buff??????);
                                        }
                                    }
                                    if (BuffData.Template.EffectRemoved)
                                    {
                                        MapObject.??????Buff?????????(BuffData.Id.V);
                                    }
                                }
                            }
                        }
                        foreach (BuffData BuffData2 in this.Buffs.Values.ToList<BuffData>())
                        {
                            if ((BuffData2.Effect & BuffEffectType.DamageIncOrDec) != BuffEffectType.SkillSign && (BuffData2.Template.HowJudgeEffect == BuffDetherminationMethod.PassiveInjuryIncrease || BuffData2.Template.HowJudgeEffect == BuffDetherminationMethod.PassiveInjuryReduction))
                            {
                                bool flag2 = false;
                                switch (??????.??????????????????)
                                {
                                    case SkillDamageType.Attack:
                                    case SkillDamageType.Needle:
                                    case SkillDamageType.Archery:
                                        {
                                            BuffJudgmentType EffectJudgeType = BuffData2.Template.EffectJudgeType;
                                            if (EffectJudgeType <= BuffJudgmentType.AllSpecificInjuries)
                                            {
                                                if (EffectJudgeType > BuffJudgmentType.AllPhysicalDamage)
                                                {
                                                    if (EffectJudgeType == BuffJudgmentType.AllSpecificInjuries)
                                                    {
                                                        HashSet<ushort> SpecificSkillId4 = BuffData2.Template.SpecificSkillId;
                                                        flag2 = (SpecificSkillId4 != null && SpecificSkillId4.Contains(??????.SkillId));
                                                    }
                                                }
                                                else
                                                {
                                                    flag2 = true;
                                                }
                                            }
                                            else if (EffectJudgeType != BuffJudgmentType.SourceSkillDamage && EffectJudgeType != BuffJudgmentType.SourcePhysicalDamage)
                                            {
                                                if (EffectJudgeType == BuffJudgmentType.SourceSpecificDamage)
                                                {
                                                    bool flag3;
                                                    if (MapObject == BuffData2.Buff??????)
                                                    {
                                                        HashSet<ushort> SpecificSkillId5 = BuffData2.Template.SpecificSkillId;
                                                        flag3 = (SpecificSkillId5 != null && SpecificSkillId5.Contains(??????.SkillId));
                                                    }
                                                    else
                                                    {
                                                        flag3 = false;
                                                    }
                                                    flag2 = flag3;
                                                }
                                            }
                                            else
                                            {
                                                flag2 = (MapObject == BuffData2.Buff??????);
                                            }
                                            break;
                                        }
                                    case SkillDamageType.Magic:
                                    case SkillDamageType.Taoism:
                                        {
                                            BuffJudgmentType EffectJudgeType = BuffData2.Template.EffectJudgeType;
                                            if (EffectJudgeType <= BuffJudgmentType.SourceSkillDamage)
                                            {
                                                switch (EffectJudgeType)
                                                {
                                                    case BuffJudgmentType.AllSkillDamage:
                                                    case BuffJudgmentType.AllMagicDamage:
                                                        flag2 = true;
                                                        goto IL_953;
                                                    case BuffJudgmentType.AllPhysicalDamage:
                                                    case (BuffJudgmentType)3:
                                                        goto IL_953;
                                                    case BuffJudgmentType.AllSpecificInjuries:
                                                        flag2 = BuffData2.Template.SpecificSkillId.Contains(??????.SkillId);
                                                        goto IL_953;
                                                    default:
                                                        if (EffectJudgeType != BuffJudgmentType.SourceSkillDamage)
                                                        {
                                                            goto IL_953;
                                                        }
                                                        break;
                                                }
                                            }
                                            else if (EffectJudgeType != BuffJudgmentType.SourceMagicDamage)
                                            {
                                                if (EffectJudgeType != BuffJudgmentType.SourceSpecificDamage)
                                                {
                                                    break;
                                                }
                                                bool flag4;
                                                if (MapObject == BuffData2.Buff??????)
                                                {
                                                    HashSet<ushort> SpecificSkillId6 = BuffData2.Template.SpecificSkillId;
                                                    flag4 = (SpecificSkillId6 != null && SpecificSkillId6.Contains(??????.SkillId));
                                                }
                                                else
                                                {
                                                    flag4 = false;
                                                }
                                                flag2 = flag4;
                                                break;
                                            }
                                            flag2 = (MapObject == BuffData2.Buff??????);
                                            break;
                                        }
                                    case SkillDamageType.Toxicity:
                                    case SkillDamageType.Sacred:
                                    case SkillDamageType.Burn:
                                    case SkillDamageType.Tear:
                                        {
                                            BuffJudgmentType EffectJudgeType = BuffData2.Template.EffectJudgeType;
                                            if (EffectJudgeType != BuffJudgmentType.AllSpecificInjuries)
                                            {
                                                if (EffectJudgeType == BuffJudgmentType.SourceSpecificDamage)
                                                {
                                                    bool flag5;
                                                    if (MapObject == BuffData2.Buff??????)
                                                    {
                                                        HashSet<ushort> SpecificSkillId7 = BuffData2.Template.SpecificSkillId;
                                                        flag5 = (SpecificSkillId7 != null && SpecificSkillId7.Contains(??????.SkillId));
                                                    }
                                                    else
                                                    {
                                                        flag5 = false;
                                                    }
                                                    flag2 = flag5;
                                                }
                                            }
                                            else
                                            {
                                                HashSet<ushort> SpecificSkillId8 = BuffData2.Template.SpecificSkillId;
                                                flag2 = (SpecificSkillId8 != null && SpecificSkillId8.Contains(??????.SkillId));
                                            }
                                            break;
                                        }
                                }
                            IL_953:
                                if (flag2)
                                {
                                    int v2 = (int)BuffData2.????????????.V;
                                    int[] DamageIncOrDecBase2 = BuffData2.Template.DamageIncOrDecBase;
                                    num = ((DamageIncOrDecBase2 != null) ? new int?(DamageIncOrDecBase2.Length) : null);
                                    num2 = (int)BuffData2.Buff??????.V;
                                    int num17 = v2 * ((num.GetValueOrDefault() > num2 & num != null) ? BuffData2.Template.DamageIncOrDecBase[(int)BuffData2.Buff??????.V] : 0);
                                    float num18 = (float)BuffData2.????????????.V;
                                    float[] DamageIncOrDecFactor2 = BuffData2.Template.DamageIncOrDecFactor;
                                    num = ((DamageIncOrDecFactor2 != null) ? new int?(DamageIncOrDecFactor2.Length) : null);
                                    num2 = (int)BuffData2.Buff??????.V;
                                    float num19 = num18 * ((num.GetValueOrDefault() > num2 & num != null) ? BuffData2.Template.DamageIncOrDecFactor[(int)BuffData2.Buff??????.V] : 0f);
                                    num11 += ((BuffData2.Template.HowJudgeEffect == BuffDetherminationMethod.PassiveInjuryIncrease) ? num17 : (-num17));
                                    num12 += ((BuffData2.Template.HowJudgeEffect == BuffDetherminationMethod.PassiveInjuryIncrease) ? num19 : (-num19));
                                    MapObject MapObject3;
                                    if (BuffData2.Template.EffectiveFollowedById != 0 && BuffData2.Buff?????? != null && MapGatewayProcess.Objects.TryGetValue(BuffData2.Buff??????.ObjectId, out MapObject3) && MapObject3 == BuffData2.Buff??????)
                                    {
                                        if (BuffData2.Template.FollowUpSkillSource)
                                        {
                                            MapObject.OnAddBuff(BuffData2.Template.EffectiveFollowedById, BuffData2.Buff??????);
                                        }
                                        else
                                        {
                                            this.OnAddBuff(BuffData2.Template.EffectiveFollowedById, BuffData2.Buff??????);
                                        }
                                    }
                                    if (BuffData2.Template.HowJudgeEffect == BuffDetherminationMethod.PassiveInjuryReduction && BuffData2.Template.LimitedDamage)
                                    {
                                        num13 = Math.Min(num13, BuffData2.Template.LimitedDamageValue);
                                    }
                                    if (BuffData2.Template.EffectRemoved)
                                    {
                                        this.??????Buff?????????(BuffData2.Id.V);
                                    }
                                }
                            }
                        }
                        float num20 = (num4 + num6) * (float)num9 + (float)num3 + (float)num5 + (float)num11;
                        float val = (float)(num10 - num7) - (float)num10 * num8;
                        float val2 = (num20 - Math.Max(0f, val)) * (1f + num12) * ????????????;
                        int ???????????? = (int)Math.Min((float)num13, Math.Max(0f, val2));
                        ??????.Damage = ????????????;
                    }
                }
                this.TimeoutTime = MainProcess.CurrentTime.AddSeconds(10.0);
                MapObject.TimeoutTime = MainProcess.CurrentTime.AddSeconds(10.0);
                if ((??????.Feedback & SkillHitFeedback.Miss) == SkillHitFeedback.??????)
                {
                    foreach (BuffData BuffData3 in this.Buffs.Values.ToList<BuffData>())
                    {
                        if ((BuffData3.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign && (BuffData3.Template.PlayerState & GameObjectState.Absence) != GameObjectState.Normal)
                        {
                            this.??????Buff?????????(BuffData3.Id.V);
                        }
                    }
                }
                MonsterObject MonsterObject2 = this as MonsterObject;
                if (MonsterObject2 != null)
                {
                    MonsterObject2.HardTime = MainProcess.CurrentTime.AddMilliseconds((double)??????.??????????????????);
                    if (MapObject is PlayerObject || MapObject is PetObject)
                    {
                        MonsterObject2.HateObject.????????????(MapObject, MainProcess.CurrentTime.AddMilliseconds((double)MonsterObject2.HateTime), ??????.Damage);
                    }
                }
                else
                {
                    PlayerObject PlayerObject = this as PlayerObject;
                    if (PlayerObject != null)
                    {
                        if (??????.Damage > 0)
                        {
                            PlayerObject.??????????????????(??????.Damage);
                        }
                        if (??????.Damage > 0)
                        {
                            PlayerObject.??????????????????(??????.Damage);
                        }
                        if (PlayerObject.GetRelationship(MapObject) == GameObjectRelationship.Hostility)
                        {
                            foreach (PetObject PetObject in PlayerObject.Pets.ToList<PetObject>())
                            {
                                if (PetObject.Neighbors.Contains(MapObject) && !MapObject.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                                {
                                    PetObject.HateObject.????????????(MapObject, MainProcess.CurrentTime.AddMilliseconds((double)PetObject.HateTime), 0);
                                }
                            }
                        }
                        PlayerObject PlayerObject2 = MapObject as PlayerObject;
                        if (PlayerObject2 != null && !this.CurrentMap.????????????(this.CurrentPosition) && !PlayerObject.???????????? && !PlayerObject.????????????)
                        {
                            if (PlayerObject2.????????????)
                            {
                                PlayerObject2.???PK?????? = TimeSpan.FromMinutes(1.0);
                            }
                            else
                            {
                                PlayerObject2.???????????? = TimeSpan.FromMinutes(1.0);
                            }
                        }
                        else
                        {
                            PetObject PetObject2 = MapObject as PetObject;
                            if (PetObject2 != null && !this.CurrentMap.????????????(this.CurrentPosition) && !PlayerObject.???????????? && !PlayerObject.????????????)
                            {
                                if (PetObject2.PlayerOwner.????????????)
                                {
                                    PetObject2.PlayerOwner.???PK?????? = TimeSpan.FromMinutes(1.0);
                                }
                                else
                                {
                                    PetObject2.PlayerOwner.???????????? = TimeSpan.FromMinutes(1.0);
                                }
                            }
                        }
                    }
                    else
                    {
                        PetObject PetObject3 = this as PetObject;
                        if (PetObject3 != null)
                        {
                            if (MapObject != PetObject3.PlayerOwner && PetObject3.GetRelationship(MapObject) == GameObjectRelationship.Hostility)
                            {
                                PlayerObject ???????????? = PetObject3.PlayerOwner;
                                foreach (PetObject PetObject4 in ((???????????? != null) ? ????????????.Pets.ToList<PetObject>() : null))
                                {
                                    if (PetObject4.Neighbors.Contains(MapObject) && !MapObject.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                                    {
                                        PetObject4.HateObject.????????????(MapObject, MainProcess.CurrentTime.AddMilliseconds((double)PetObject4.HateTime), 0);
                                    }
                                }
                            }
                            if (MapObject != PetObject3.PlayerOwner)
                            {
                                PlayerObject PlayerObject3 = MapObject as PlayerObject;
                                if (PlayerObject3 != null && !this.CurrentMap.????????????(this.CurrentPosition) && !PetObject3.PlayerOwner.???????????? && !PetObject3.PlayerOwner.????????????)
                                {
                                    PlayerObject3.???????????? = TimeSpan.FromMinutes(1.0);
                                }
                            }
                        }
                        else
                        {
                            GuardObject GuardInstance = this as GuardObject;
                            if (GuardInstance != null && GuardInstance.GetRelationship(MapObject) == GameObjectRelationship.Hostility)
                            {
                                GuardInstance.HateObject.????????????(MapObject, default(DateTime), 0);
                            }
                        }
                    }
                }
                PlayerObject PlayerObject4 = MapObject as PlayerObject;
                if (PlayerObject4 != null)
                {
                    if (PlayerObject4.GetRelationship(this) == GameObjectRelationship.Hostility && !this.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                    {
                        foreach (PetObject PetObject5 in PlayerObject4.Pets.ToList<PetObject>())
                        {
                            if (PetObject5.Neighbors.Contains(this))
                            {
                                PetObject5.HateObject.????????????(this, MainProcess.CurrentTime.AddMilliseconds((double)PetObject5.HateTime), ??????.?????????????????? ? ??????.Damage : 0);
                            }
                        }
                    }
                    EquipmentData EquipmentData;
                    if (MainProcess.CurrentTime > PlayerObject4.???????????? && !PlayerObject4.Died && PlayerObject4.CurrentHP < PlayerObject4[GameObjectStats.MaxHP] && PlayerObject4.Equipment.TryGetValue(15, out EquipmentData) && EquipmentData.????????????.V > 0 && (EquipmentData.Id == 99999106 || EquipmentData.Id == 99999107))
                    {
                        PlayerObject4.CurrentHP += ((this is MonsterObject) ? 20 : 10);
                        PlayerObject4.??????????????????(1);
                        PlayerObject4.???????????? = MainProcess.CurrentTime.AddMilliseconds(1000.0);
                    }
                }
                if ((this.CurrentHP = Math.Max(0, this.CurrentHP - ??????.Damage)) == 0)
                {
                    ??????.Feedback |= SkillHitFeedback.??????;
                    this.Dies(MapObject, true);
                }
                return;
            }
        }


        public void ?????????????????????(BuffData ??????)
        {
            int num = 0;
            switch (??????.????????????)
            {
                case SkillDamageType.Attack:
                case SkillDamageType.Needle:
                case SkillDamageType.Archery:
                    num = ComputingClass.????????????(this[GameObjectStats.MinDef], this[GameObjectStats.MaxDef]);
                    break;
                case SkillDamageType.Magic:
                case SkillDamageType.Taoism:
                    num = ComputingClass.????????????(this[GameObjectStats.MinMCDef], this[GameObjectStats.MaxMCDef]);
                    break;
            }
            int num2 = Math.Max(0, ??????.????????????.V * (int)??????.????????????.V - num);
            this.CurrentHP = Math.Max(0, this.CurrentHP - num2);
            ?????????????????? ?????????????????? = new ??????????????????();
            ??????????????????.Id = ??????.Id.V;
            MapObject buff?????? = ??????.Buff??????;
            ??????????????????.Buff?????? = ((buff?????? != null) ? buff??????.ObjectId : 0);
            ??????????????????.Buff?????? = this.ObjectId;
            ??????????????????.???????????? = -num2;
            this.SendPacket(??????????????????);
            if (this.CurrentHP == 0)
            {
                this.Dies(??????.Buff??????, false);
            }
        }


        public void ?????????????????????(SkillInstance ??????, C_05_CalculateTargetReply ??????)
        {
            if (!this.Died)
            {
                if (this.CurrentMap == ??????.CasterObject.CurrentMap)
                {
                    if (this != ??????.CasterObject && !this.Neighbors.Contains(??????.CasterObject))
                    {
                        return;
                    }
                    TrapObject TrapObject = ??????.CasterObject as TrapObject;
                    MapObject MapObject = (TrapObject != null) ? TrapObject.TrapSource : ??????.CasterObject;
                    int[] ?????????????????? = ??????.??????????????????;
                    int? num = (?????????????????? != null) ? new int?(??????????????????.Length) : null;
                    int SkillLevel = (int)??????.SkillLevel;
                    int num2 = (num.GetValueOrDefault() > SkillLevel & num != null) ? ??????.??????????????????[(int)??????.SkillLevel] : 0;
                    byte[] PhysicalRecoveryBase = ??????.PhysicalRecoveryBase;
                    num = ((PhysicalRecoveryBase != null) ? new int?(PhysicalRecoveryBase.Length) : null);
                    SkillLevel = (int)??????.SkillLevel;
                    int num3 = (int)((num.GetValueOrDefault() > SkillLevel & num != null) ? ??????.PhysicalRecoveryBase[(int)??????.SkillLevel] : 0);
                    float[] Taoism???????????? = ??????.Taoism????????????;
                    num = ((Taoism???????????? != null) ? new int?(Taoism????????????.Length) : null);
                    SkillLevel = (int)??????.SkillLevel;
                    float num4 = (num.GetValueOrDefault() > SkillLevel & num != null) ? ??????.Taoism????????????[(int)??????.SkillLevel] : 0f;
                    float[] Taoism???????????? = ??????.Taoism????????????;
                    num = ((Taoism???????????? != null) ? new int?(Taoism????????????.Length) : null);
                    SkillLevel = (int)??????.SkillLevel;
                    float num5 = (num.GetValueOrDefault() > SkillLevel & num != null) ? ??????.Taoism????????????[(int)??????.SkillLevel] : 0f;
                    int[] ?????????????????? = ??????.??????????????????;
                    num = ((?????????????????? != null) ? new int?(??????????????????.Length) : null);
                    SkillLevel = (int)??????.SkillLevel;
                    int num6;
                    if (num.GetValueOrDefault() > SkillLevel & num != null)
                    {
                        if (MapObject == this)
                        {
                            num6 = ??????.??????????????????[(int)??????.SkillLevel];
                            goto IL_1F1;
                        }
                    }
                    num6 = 0;
                IL_1F1:
                    int num7 = num6;
                    float[] ?????????????????? = ??????.??????????????????;
                    num = ((?????????????????? != null) ? new int?(??????????????????.Length) : null);
                    SkillLevel = (int)??????.SkillLevel;
                    float num8;
                    if (num.GetValueOrDefault() > SkillLevel & num != null)
                    {
                        if (MapObject == this)
                        {
                            num8 = ??????.??????????????????[(int)??????.SkillLevel];
                            goto IL_249;
                        }
                    }
                    num8 = 0f;
                IL_249:
                    float num9 = num8;
                    if (num4 > 0f)
                    {
                        num2 += (int)(num4 * (float)ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinSC], MapObject[GameObjectStats.MaxSC], MapObject[GameObjectStats.Luck]));
                    }
                    if (num5 > 0f)
                    {
                        num3 += (int)(num5 * (float)ComputingClass.CalculateAttack(MapObject[GameObjectStats.MinSC], MapObject[GameObjectStats.MaxSC], MapObject[GameObjectStats.Luck]));
                    }
                    if (num7 > 0)
                    {
                        this.CurrentHP += num7;
                    }
                    if (num9 > 0f)
                    {
                        this.CurrentHP += (int)((float)this[GameObjectStats.MaxHP] * num9);
                    }
                    if (num2 > this.TreatmentCount && num3 > 0)
                    {
                        this.TreatmentCount = (int)((byte)num2);
                        this.TreatmentBase = num3;
                        this.HealTime = MainProcess.CurrentTime.AddMilliseconds(500.0);
                    }
                    return;
                }
            }
        }


        public void ?????????????????????(BuffData ??????)
        {
            if (??????.Template.PhysicalRecoveryBase == null)
            {
                return;
            }
            if (??????.Template.PhysicalRecoveryBase.Length <= (int)??????.Buff??????.V)
            {
                return;
            }
            byte b = ??????.Template.PhysicalRecoveryBase[(int)??????.Buff??????.V];
            this.CurrentHP += (int)b;
            ?????????????????? ?????????????????? = new ??????????????????();
            ??????????????????.Id = ??????.Id.V;
            MapObject buff?????? = ??????.Buff??????;
            ??????????????????.Buff?????? = ((buff?????? != null) ? buff??????.ObjectId : 0);
            ??????????????????.Buff?????? = this.ObjectId;
            ??????????????????.???????????? = (int)b;
            this.SendPacket(??????????????????);
        }


        public void ItSelf???????????????(Point ??????)
        {
            PlayerObject PlayerObject = this as PlayerObject;
            if (PlayerObject != null)
            {
                PlayerDeals ???????????? = PlayerObject.????????????;
                if (???????????? != null)
                {
                    ????????????.????????????();
                }
                using (List<BuffData>.Enumerator enumerator = this.Buffs.Values.ToList<BuffData>().GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        BuffData BuffData = enumerator.Current;
                        SkillTraps ????????????;
                        if ((BuffData.Effect & BuffEffectType.CreateTrap) != BuffEffectType.SkillSign && SkillTraps.DataSheet.TryGetValue(BuffData.Template.TriggerTrapSkills, out ????????????))
                        {
                            int num = 0;

                            for (; ; )
                            {
                                Point point = ComputingClass.GetFrontPosition(this.CurrentPosition, ??????, num);
                                if (point == ??????)
                                {
                                    break;
                                }
                                foreach (Point ??????2 in ComputingClass.GetLocationRange(point, this.CurrentDirection, BuffData.Template.NumberTrapsTriggered))
                                {
                                    if (!this.CurrentMap.IsBlocked(??????2))
                                    {
                                        IEnumerable<MapObject> source = this.CurrentMap[??????2];
                                        Func<MapObject, bool> predicate = null;
                                        if (predicate == null)
                                        {
                                            predicate = delegate (MapObject O)
                                          {
                                              TrapObject TrapObject = O as TrapObject;
                                              return TrapObject != null && TrapObject.??????GroupId != 0 && TrapObject.??????GroupId == ????????????.GroupId;
                                          };
                                        }
                                        if (source.FirstOrDefault(predicate) == null)
                                        {
                                            this.Traps.Add(new TrapObject(this, ????????????, this.CurrentMap, ??????2));
                                        }
                                    }
                                }
                                num++;
                            }
                        }
                        if ((BuffData.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign && (BuffData.Template.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal)
                        {
                            this.??????Buff?????????(BuffData.Id.V);
                        }
                    }
                    goto IL_30E;
                }
            }
            if (this is PetObject)
            {
                foreach (BuffData BuffData2 in this.Buffs.Values.ToList<BuffData>())
                {
                    SkillTraps ????????????;
                    if ((BuffData2.Effect & BuffEffectType.CreateTrap) != BuffEffectType.SkillSign && SkillTraps.DataSheet.TryGetValue(BuffData2.Template.TriggerTrapSkills, out ????????????))
                    {
                        int num2 = 0;

                        for (; ; )
                        {
                            Point point2 = ComputingClass.GetFrontPosition(this.CurrentPosition, ??????, num2);
                            if (point2 == ??????)
                            {
                                break;
                            }
                            foreach (Point ??????3 in ComputingClass.GetLocationRange(point2, this.CurrentDirection, BuffData2.Template.NumberTrapsTriggered))
                            {
                                if (!this.CurrentMap.IsBlocked(??????3))
                                {
                                    IEnumerable<MapObject> source2 = this.CurrentMap[??????3];
                                    Func<MapObject, bool> predicate2 = null;
                                    if (predicate2 == null)
                                    {
                                        predicate2 = delegate (MapObject O)
                                       {
                                           TrapObject TrapObject = O as TrapObject;
                                           return TrapObject != null && TrapObject.??????GroupId != 0 && TrapObject.??????GroupId == ????????????.GroupId;
                                       };
                                    }
                                    if (source2.FirstOrDefault(predicate2) == null)
                                    {
                                        this.Traps.Add(new TrapObject(this, ????????????, this.CurrentMap, ??????3));
                                    }
                                }
                            }
                            num2++;
                        }
                    }
                    if ((BuffData2.Effect & BuffEffectType.StatusFlag) != BuffEffectType.SkillSign && (BuffData2.Template.PlayerState & GameObjectState.Invisibility) != GameObjectState.Normal)
                    {
                        this.??????Buff?????????(BuffData2.Id.V);
                    }
                }
            }
        IL_30E:
            this.UnbindGrid();
            this.CurrentPosition = ??????;
            this.BindGrid();
            this.?????????????????????();
            foreach (MapObject MapObject in this.Neighbors.ToList<MapObject>())
            {
                MapObject.?????????????????????(this);
            }
        }


        public void NotifyNeightborClear()
        {
            foreach (MapObject MapObject in this.Neighbors.ToList<MapObject>())
            {
                MapObject.?????????????????????(this);
            }
            this.Neighbors.Clear();
            this.NeighborsImportant.Clear();
            this.NeighborsSneak.Clear();
        }


        public void ?????????????????????()
        {
            foreach (MapObject MapObject in this.Neighbors.ToList<MapObject>())
            {
                if (this.CurrentMap != MapObject.CurrentMap || !this.CanBeSeenBy(MapObject))
                {
                    MapObject.?????????????????????(this);
                    this.?????????????????????(MapObject);
                }
            }
            for (int i = -20; i <= 20; i++)
            {
                for (int j = -20; j <= 20; j++)
                {
                    this.CurrentMap[new Point(this.CurrentPosition.X + i, this.CurrentPosition.Y + j)].ToList<MapObject>();
                    try
                    {
                        foreach (MapObject MapObject2 in this.CurrentMap[new Point(this.CurrentPosition.X + i, this.CurrentPosition.Y + j)])
                        {
                            if (MapObject2 != this)
                            {
                                if (!this.Neighbors.Contains(MapObject2) && this.IsNeightbor(MapObject2))
                                {
                                    this.?????????????????????(MapObject2);
                                }
                                if (!MapObject2.Neighbors.Contains(this) && MapObject2.IsNeightbor(this))
                                {
                                    MapObject2.?????????????????????(this);
                                }
                            }
                        }
                        goto IL_15C;
                    }
                    catch
                    {
                        goto IL_15C;
                    }
                    break;
                IL_15C:;
                }
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            if (!(this is ItemObject))
            {
                PetObject PetObject = this as PetObject;
                if (PetObject != null)
                {
                    HateObject.???????????? ????????????;
                    if (PetObject.CanAttack(??????) && this.GetDistance(??????) <= PetObject.RangeHate && !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                    {
                        PetObject.HateObject.????????????(??????, default(DateTime), 0);
                    }
                    else if (this.GetDistance(??????) > PetObject.RangeHate && PetObject.HateObject.????????????.TryGetValue(??????, out ????????????) && ????????????.???????????? < MainProcess.CurrentTime)
                    {
                        PetObject.HateObject.????????????(??????);
                    }
                }
                else
                {
                    MonsterObject MonsterObject = this as MonsterObject;
                    if (MonsterObject != null)
                    {
                        HateObject.???????????? ????????????2;
                        if (this.GetDistance(??????) <= MonsterObject.RangeHate && MonsterObject.CanAttack(??????) && (MonsterObject.VisibleStealthTargets || !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus)))
                        {
                            MonsterObject.HateObject.????????????(??????, default(DateTime), 0);
                        }
                        else if (this.GetDistance(??????) > MonsterObject.RangeHate && MonsterObject.HateObject.????????????.TryGetValue(??????, out ????????????2) && ????????????2.???????????? < MainProcess.CurrentTime)
                        {
                            MonsterObject.HateObject.????????????(??????);
                        }
                    }
                    else
                    {
                        TrapObject TrapObject = this as TrapObject;
                        if (TrapObject != null)
                        {
                            if (ComputingClass.GetLocationRange(TrapObject.CurrentPosition, TrapObject.CurrentDirection, TrapObject.ObjectSize).Contains(??????.CurrentPosition))
                            {
                                TrapObject.??????????????????(??????);
                            }
                        }
                        else
                        {
                            GuardObject GuardInstance = this as GuardObject;
                            if (GuardInstance != null)
                            {
                                if (GuardInstance.CanAttack(??????) && this.GetDistance(??????) <= GuardInstance.RangeHate)
                                {
                                    GuardInstance.HateObject.????????????(??????, default(DateTime), 0);
                                }
                                else if (this.GetDistance(??????) > GuardInstance.RangeHate)
                                {
                                    GuardInstance.HateObject.????????????(??????);
                                }
                            }
                        }
                    }
                }
            }
            if (!(?????? is ItemObject))
            {
                PetObject PetObject2 = ?????? as PetObject;
                if (PetObject2 != null)
                {
                    if (PetObject2.GetDistance(this) <= PetObject2.RangeHate && PetObject2.CanAttack(this) && !this.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                    {
                        PetObject2.HateObject.????????????(this, default(DateTime), 0);
                        return;
                    }
                    HateObject.???????????? ????????????3;
                    if (PetObject2.GetDistance(this) > PetObject2.RangeHate && PetObject2.HateObject.????????????.TryGetValue(this, out ????????????3) && ????????????3.???????????? < MainProcess.CurrentTime)
                    {
                        PetObject2.HateObject.????????????(this);
                        return;
                    }
                }
                else
                {
                    MonsterObject MonsterObject2 = ?????? as MonsterObject;
                    if (MonsterObject2 != null)
                    {
                        if (MonsterObject2.GetDistance(this) <= MonsterObject2.RangeHate && MonsterObject2.CanAttack(this) && (MonsterObject2.VisibleStealthTargets || !this.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus)))
                        {
                            MonsterObject2.HateObject.????????????(this, default(DateTime), 0);
                            return;
                        }
                        HateObject.???????????? ????????????4;
                        if (MonsterObject2.GetDistance(this) > MonsterObject2.RangeHate && MonsterObject2.HateObject.????????????.TryGetValue(this, out ????????????4) && ????????????4.???????????? < MainProcess.CurrentTime)
                        {
                            MonsterObject2.HateObject.????????????(this);
                            return;
                        }
                    }
                    else
                    {
                        TrapObject TrapObject2 = ?????? as TrapObject;
                        if (TrapObject2 != null)
                        {
                            if (ComputingClass.GetLocationRange(TrapObject2.CurrentPosition, TrapObject2.CurrentDirection, TrapObject2.ObjectSize).Contains(this.CurrentPosition))
                            {
                                TrapObject2.??????????????????(this);
                                return;
                            }
                        }
                        else
                        {
                            GuardObject GuardInstance2 = ?????? as GuardObject;
                            if (GuardInstance2 != null)
                            {
                                if (GuardInstance2.CanAttack(this) && GuardInstance2.GetDistance(this) <= GuardInstance2.RangeHate)
                                {
                                    GuardInstance2.HateObject.????????????(this, default(DateTime), 0);
                                    return;
                                }
                                if (GuardInstance2.GetDistance(this) > GuardInstance2.RangeHate)
                                {
                                    GuardInstance2.HateObject.????????????(this);
                                }
                            }
                        }
                    }
                }
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            if (this.NeighborsSneak.Remove(??????))
            {
                if (this is PlayerObject PlayerObject)
                {
                    GameObjectType ???????????? = ??????.ObjectType;
                    if (???????????? <= GameObjectType.NPC)
                    {
                        switch (????????????)
                        {
                            case GameObjectType.Player:
                            case GameObjectType.Monster:
                                break;
                            case GameObjectType.Pet:
                                PlayerObject.ActiveConnection.SendPacket(new ObjectCharacterStopPacket
                                {
                                    ???????????? = ??????.ObjectId,
                                    ???????????? = ??????.CurrentPosition,
                                    ???????????? = ??????.CurrentAltitude
                                });
                                PlayerObject.ActiveConnection.SendPacket(new ObjectComesIntoViewPacket
                                {
                                    ???????????? = 1,
                                    ???????????? = ??????.ObjectId,
                                    ???????????? = ??????.CurrentPosition,
                                    ???????????? = ??????.CurrentAltitude,
                                    ???????????? = (ushort)??????.CurrentDirection,
                                    ???????????? = ((byte)(??????.Died ? 13 : 1)),
                                    ???????????? = (byte)(??????.CurrentHP * 100 / ??????[GameObjectStats.MaxHP])
                                });
                                PlayerObject.ActiveConnection.SendPacket(new SyncObjectHP
                                {
                                    ObjectId = ??????.ObjectId,
                                    CurrentHP = ??????.CurrentHP,
                                    MaxHP = ??????[GameObjectStats.MaxHP]
                                });
                                PlayerObject.ActiveConnection.SendPacket(new ObjectTransformTypePacket
                                {
                                    ???????????? = 2,
                                    ???????????? = ??????.ObjectId
                                });
                                goto IL_356;
                            case (GameObjectType)3:
                                goto IL_356;
                            default:
                                if (???????????? != GameObjectType.NPC)
                                {
                                    goto IL_356;
                                }
                                break;
                        }
                        PlayerObject.ActiveConnection.SendPacket(new ObjectCharacterStopPacket
                        {
                            ???????????? = ??????.ObjectId,
                            ???????????? = ??????.CurrentPosition,
                            ???????????? = ??????.CurrentAltitude
                        });
                        SConnection ???????????? = PlayerObject.ActiveConnection;
                        ObjectComesIntoViewPacket ObjectComesIntoViewPacket = new ObjectComesIntoViewPacket();
                        ObjectComesIntoViewPacket.???????????? = 1;
                        ObjectComesIntoViewPacket.???????????? = ??????.ObjectId;
                        ObjectComesIntoViewPacket.???????????? = ??????.CurrentPosition;
                        ObjectComesIntoViewPacket.???????????? = ??????.CurrentAltitude;
                        ObjectComesIntoViewPacket.???????????? = (ushort)??????.CurrentDirection;
                        ObjectComesIntoViewPacket.???????????? = ((byte)(??????.Died ? 13 : 1));
                        ObjectComesIntoViewPacket.???????????? = (byte)(??????.CurrentHP * 100 / ??????[GameObjectStats.MaxHP]);
                        PlayerObject PlayerObject2 = ?????? as PlayerObject;
                        if (PlayerObject2 != null)
                        {
                            ObjectComesIntoViewPacket.AdditionalParam = (byte)(!PlayerObject2.???????????? ? 0 : 2);
                            ObjectComesIntoViewPacket.ActiveMount = PlayerObject2.Riding ? (byte)PlayerObject2.CharacterData.CurrentMount.V : (byte)0;
                        }
                        ????????????.SendPacket(ObjectComesIntoViewPacket);
                        PlayerObject.ActiveConnection.SendPacket(new SyncObjectHP
                        {
                            ObjectId = ??????.ObjectId,
                            CurrentHP = ??????.CurrentHP,
                            MaxHP = ??????[GameObjectStats.MaxHP]
                        });
                    }
                    else if (???????????? != GameObjectType.Item)
                    {
                        if (???????????? == GameObjectType.Trap)
                        {
                            PlayerObject.ActiveConnection.SendPacket(new TrapComesIntoViewPacket
                            {
                                MapId = ??????.ObjectId,
                                ???????????? = ??????.CurrentPosition,
                                ???????????? = ??????.CurrentAltitude,
                                ???????????? = (?????? as TrapObject).TrapSource.ObjectId,
                                Id = (?????? as TrapObject).Id,
                                ???????????? = (?????? as TrapObject).??????????????????
                            });
                        }
                    }
                    else if (?????? is ItemObject dropObject)
                    {
                        PlayerObject.ActiveConnection.SendPacket(new ObjectDropItemsPacket
                        {
                            DropperObjectId = dropObject.DropperObjectId,
                            ItemObjectId = dropObject.ObjectId,
                            ???????????? = dropObject.CurrentPosition,
                            ???????????? = dropObject.CurrentAltitude,
                            ItemId = dropObject.Id,
                            ???????????? = dropObject.????????????,
                            OwnerPlayerId = dropObject.GetOwnerPlayerIdForDrop(PlayerObject),
                        });
                    }
                IL_356:
                    if (??????.Buffs.Count > 0)
                    {
                        PlayerObject.ActiveConnection.SendPacket(new ????????????Buff
                        {
                            ???????????? = ??????.??????Buff??????()
                        });
                        return;
                    }
                }
                else if (this is TrapObject TrapObject)
                {
                    if (ComputingClass.GetLocationRange(TrapObject.CurrentPosition, TrapObject.CurrentDirection, TrapObject.ObjectSize).Contains(??????.CurrentPosition))
                    {
                        TrapObject.??????????????????(??????);
                        return;
                    }
                }
                else if (this is PetObject PetObject)
                {
                    if (this.GetDistance(??????) <= PetObject.RangeHate && PetObject.CanAttack(??????) && !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                    {
                        PetObject.HateObject.????????????(??????, default(DateTime), 0);
                        return;
                    }
                    HateObject.???????????? ????????????;
                    if (this.GetDistance(??????) > PetObject.RangeHate && PetObject.HateObject.????????????.TryGetValue(??????, out ????????????) && ????????????.???????????? < MainProcess.CurrentTime)
                    {
                        PetObject.HateObject.????????????(??????);
                        return;
                    }
                }
                else if (this is MonsterObject MonsterObject)
                {
                    if (this.GetDistance(??????) <= MonsterObject.RangeHate && MonsterObject.CanAttack(??????) && (MonsterObject.VisibleStealthTargets || !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus)))
                    {
                        MonsterObject.HateObject.????????????(??????, default(DateTime), 0);
                        return;
                    }
                    HateObject.???????????? ????????????2;
                    if (this.GetDistance(??????) > MonsterObject.RangeHate && MonsterObject.HateObject.????????????.TryGetValue(??????, out ????????????2) && ????????????2.???????????? < MainProcess.CurrentTime)
                    {
                        MonsterObject.HateObject.????????????(??????);
                        return;
                    }
                }
            }
            else if (this.Neighbors.Add(??????))
            {
                if (?????? is PlayerObject || ?????? is PetObject)
                    this.NeighborsImportant.Add(??????);

                if (this is PlayerObject PlayerObject3)
                {
                    GameObjectType ???????????? = ??????.ObjectType;
                    if (???????????? <= GameObjectType.NPC)
                    {
                        switch (????????????)
                        {
                            case GameObjectType.Player:
                            case GameObjectType.Monster:
                                break;
                            case GameObjectType.Pet:
                                PlayerObject3.ActiveConnection.SendPacket(new ObjectCharacterStopPacket
                                {
                                    ???????????? = ??????.ObjectId,
                                    ???????????? = ??????.CurrentPosition,
                                    ???????????? = ??????.CurrentAltitude
                                });
                                PlayerObject3.ActiveConnection.SendPacket(new ObjectComesIntoViewPacket
                                {
                                    ???????????? = 1,
                                    ???????????? = ??????.ObjectId,
                                    ???????????? = ??????.CurrentPosition,
                                    ???????????? = ??????.CurrentAltitude,
                                    ???????????? = (ushort)??????.CurrentDirection,
                                    ???????????? = ((byte)(??????.Died ? 13 : 1)),
                                    ???????????? = (byte)(??????.CurrentHP * 100 / ??????[GameObjectStats.MaxHP])
                                });
                                PlayerObject3.ActiveConnection.SendPacket(new SyncObjectHP
                                {
                                    ObjectId = ??????.ObjectId,
                                    CurrentHP = ??????.CurrentHP,
                                    MaxHP = ??????[GameObjectStats.MaxHP]
                                });
                                PlayerObject3.ActiveConnection.SendPacket(new ObjectTransformTypePacket
                                {
                                    ???????????? = 2,
                                    ???????????? = ??????.ObjectId
                                });
                                goto IL_866;
                            case (GameObjectType)3:
                                goto IL_866;
                            default:
                                if (???????????? != GameObjectType.NPC)
                                {
                                    goto IL_866;
                                }
                                break;
                        }
                        PlayerObject3.ActiveConnection.SendPacket(new ObjectCharacterStopPacket
                        {
                            ???????????? = ??????.ObjectId,
                            ???????????? = ??????.CurrentPosition,
                            ???????????? = ??????.CurrentAltitude
                        });
                        SConnection ????????????2 = PlayerObject3.ActiveConnection;
                        ObjectComesIntoViewPacket ObjectComesIntoViewPacket2 = new ObjectComesIntoViewPacket
                        {
                            ???????????? = 1,
                            ???????????? = ??????.ObjectId,
                            ???????????? = ??????.CurrentPosition,
                            ???????????? = ??????.CurrentAltitude,
                            ???????????? = (ushort)??????.CurrentDirection,
                            ???????????? = ((byte)(??????.Died ? 13 : 1)),
                            ???????????? = (byte)(??????.CurrentHP * 100 / ??????[GameObjectStats.MaxHP])
                        };
                        PlayerObject PlayerObject4 = ?????? as PlayerObject;
                        if (PlayerObject4 != null)
                        {
                            ObjectComesIntoViewPacket2.AdditionalParam = (byte)(!PlayerObject4.???????????? ? 0 : 2);
                            ObjectComesIntoViewPacket2.ActiveMount = (byte)(PlayerObject4.Riding ? PlayerObject4.CharacterData.CurrentMount.V : 0);
                        }
                        ????????????2.SendPacket(ObjectComesIntoViewPacket2);

                        PlayerObject3.ActiveConnection.SendPacket(new SyncObjectHP
                        {
                            ObjectId = ??????.ObjectId,
                            CurrentHP = ??????.CurrentHP,
                            MaxHP = ??????[GameObjectStats.MaxHP]
                        });
                    }
                    else if (???????????? != GameObjectType.Item)
                    {
                        if (???????????? == GameObjectType.Trap)
                        {
                            PlayerObject3.ActiveConnection.SendPacket(new TrapComesIntoViewPacket
                            {
                                MapId = ??????.ObjectId,
                                ???????????? = ??????.CurrentPosition,
                                ???????????? = ??????.CurrentAltitude,
                                ???????????? = (?????? as TrapObject).TrapSource.ObjectId,
                                Id = (?????? as TrapObject).Id,
                                ???????????? = (?????? as TrapObject).??????????????????
                            });
                        }
                        else if (?????? is ChestObject chestObject && chestObject.IsAlredyOpened(PlayerObject3))
                        {
                            PlayerObject3.ActiveConnection.SendPacket(new ChestComesIntoViewPacket
                            {
                                ObjectId = ??????.ObjectId,
                                Direction = (ushort)??????.CurrentDirection,
                                Position = ??????.CurrentPosition,
                                Altitude = ??????.CurrentAltitude,
                                NPCTemplateId = chestObject.Template.Id,
                            });
                            chestObject.ActivateObject();
                        }
                    }
                    else if (?????? is ItemObject dropObject)
                    {
                        PlayerObject3.ActiveConnection.SendPacket(new ObjectDropItemsPacket
                        {
                            DropperObjectId = dropObject.DropperObjectId,
                            ItemObjectId = dropObject.ObjectId,
                            ???????????? = dropObject.CurrentPosition,
                            ???????????? = dropObject.CurrentAltitude,
                            ItemId = dropObject.Id,
                            ???????????? = dropObject.????????????,
                            OwnerPlayerId = dropObject.GetOwnerPlayerIdForDrop(PlayerObject3),
                        });
                    }
                IL_866:
                    if (??????.Buffs.Count > 0)
                    {
                        PlayerObject3.ActiveConnection.SendPacket(new ????????????Buff
                        {
                            ???????????? = ??????.??????Buff??????()
                        });
                        return;
                    }
                }
                else if (this is TrapObject TrapObject2)
                {
                    if (ComputingClass.GetLocationRange(TrapObject2.CurrentPosition, TrapObject2.CurrentDirection, TrapObject2.ObjectSize).Contains(??????.CurrentPosition))
                    {
                        TrapObject2.??????????????????(??????);
                        return;
                    }
                }
                else if (this is PetObject PetObject2)
                {
                    if (!this.Died)
                    {
                        if (this.GetDistance(??????) <= PetObject2.RangeHate && PetObject2.CanAttack(??????) && !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                        {
                            PetObject2.HateObject.????????????(??????, default(DateTime), 0);
                            return;
                        }
                        HateObject.???????????? ????????????3;
                        if (this.GetDistance(??????) > PetObject2.RangeHate && PetObject2.HateObject.????????????.TryGetValue(??????, out ????????????3) && ????????????3.???????????? < MainProcess.CurrentTime)
                        {
                            PetObject2.HateObject.????????????(??????);
                            return;
                        }
                    }
                }
                else if (this is MonsterObject MonsterObject2)
                {
                    if (!this.Died)
                    {
                        HateObject.???????????? ????????????4;
                        if (this.GetDistance(??????) <= MonsterObject2.RangeHate && MonsterObject2.CanAttack(??????) && (MonsterObject2.VisibleStealthTargets || !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus)))
                        {
                            MonsterObject2.HateObject.????????????(??????, default(DateTime), 0);
                        }
                        else if (this.GetDistance(??????) > MonsterObject2.RangeHate && MonsterObject2.HateObject.????????????.TryGetValue(??????, out ????????????4) && ????????????4.???????????? < MainProcess.CurrentTime)
                        {
                            MonsterObject2.HateObject.????????????(??????);
                        }
                        if (this.NeighborsImportant.Count != 0)
                        {
                            MonsterObject2.??????????????????();
                            return;
                        }
                    }
                }
                else if (this is GuardObject GuardInstance)
                {
                    if (!this.Died)
                    {
                        if (GuardInstance.CanAttack(??????) && this.GetDistance(??????) <= GuardInstance.RangeHate)
                        {
                            GuardInstance.HateObject.????????????(??????, default(DateTime), 0);
                        }
                        else if (this.GetDistance(??????) > GuardInstance.RangeHate)
                        {
                            GuardInstance.HateObject.????????????(??????);
                        }
                        if (this.NeighborsImportant.Count != 0)
                        {
                            GuardInstance.??????????????????();
                        }
                    }
                }
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            if (this.Neighbors.Remove(??????))
            {
                this.NeighborsSneak.Remove(??????);
                this.NeighborsImportant.Remove(??????);
                if (!(this is ItemObject))
                {
                    PlayerObject PlayerObject = this as PlayerObject;
                    if (PlayerObject != null)
                    {
                        PlayerObject.ActiveConnection.SendPacket(new ObjectOutOfViewPacket
                        {
                            ???????????? = ??????.ObjectId
                        });
                        return;
                    }
                    PetObject PetObject = this as PetObject;
                    if (PetObject != null)
                    {
                        PetObject.HateObject.????????????(??????);
                        return;
                    }
                    MonsterObject MonsterObject = this as MonsterObject;
                    if (MonsterObject != null)
                    {
                        if (!this.Died)
                        {
                            MonsterObject.HateObject.????????????(??????);
                            if (MonsterObject.NeighborsImportant.Count == 0)
                            {
                                MonsterObject.??????????????????();
                                return;
                            }
                        }
                    }
                    else
                    {
                        GuardObject GuardInstance = this as GuardObject;
                        if (GuardInstance != null && !this.Died)
                        {
                            GuardInstance.HateObject.????????????(??????);
                            if (GuardInstance.NeighborsImportant.Count == 0)
                            {
                                GuardInstance.??????????????????();
                            }
                        }
                    }
                }
            }
        }


        public void NotifyObjectDies(MapObject ??????)
        {
            MonsterObject MonsterObject = this as MonsterObject;
            if (MonsterObject != null)
            {
                MonsterObject.HateObject.????????????(??????);
                return;
            }
            PetObject PetObject = this as PetObject;
            if (PetObject != null)
            {
                PetObject.HateObject.????????????(??????);
                return;
            }
            GuardObject GuardInstance = this as GuardObject;
            if (GuardInstance != null)
            {
                GuardInstance.HateObject.????????????(??????);
                return;
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            PetObject PetObject = this as PetObject;
            if (PetObject != null && PetObject.HateObject.????????????.ContainsKey(??????))
            {
                PetObject.HateObject.????????????(??????);
            }
            MonsterObject MonsterObject = this as MonsterObject;
            if (MonsterObject != null && MonsterObject.HateObject.????????????.ContainsKey(??????) && !MonsterObject.VisibleStealthTargets)
            {
                MonsterObject.HateObject.????????????(??????);
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            PetObject PetObject = this as PetObject;
            if (PetObject != null)
            {
                if (PetObject.HateObject.????????????.ContainsKey(??????))
                {
                    PetObject.HateObject.????????????(??????);
                }
                this.NeighborsSneak.Add(??????);
            }
            MonsterObject MonsterObject = this as MonsterObject;
            if (MonsterObject != null && !MonsterObject.VisibleStealthTargets)
            {
                if (MonsterObject.HateObject.????????????.ContainsKey(??????))
                {
                    MonsterObject.HateObject.????????????(??????);
                }
                this.NeighborsSneak.Add(??????);
            }
            PlayerObject PlayerObject = this as PlayerObject;
            if (PlayerObject != null && (this.GetRelationship(??????) == GameObjectRelationship.Hostility || ??????.GetRelationship(this) == GameObjectRelationship.Hostility))
            {
                this.NeighborsSneak.Add(??????);
                PlayerObject.ActiveConnection.SendPacket(new ObjectOutOfViewPacket
                {
                    ???????????? = ??????.ObjectId
                });
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            PetObject PetObject = this as PetObject;
            if (PetObject != null)
            {
                HateObject.???????????? ????????????;
                if (this.GetDistance(??????) <= PetObject.RangeHate && PetObject.CanAttack(??????) && !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus))
                {
                    PetObject.HateObject.????????????(??????, default(DateTime), 0);
                }
                else if (this.GetDistance(??????) > PetObject.RangeHate && PetObject.HateObject.????????????.TryGetValue(??????, out ????????????) && ????????????.???????????? < MainProcess.CurrentTime)
                {
                    PetObject.HateObject.????????????(??????);
                }
            }
            MonsterObject MonsterObject = this as MonsterObject;
            if (MonsterObject != null)
            {
                if (this.GetDistance(??????) <= MonsterObject.RangeHate && MonsterObject.CanAttack(??????) && (MonsterObject.VisibleStealthTargets || !??????.CheckStatus(GameObjectState.Invisibility | GameObjectState.StealthStatus)))
                {
                    MonsterObject.HateObject.????????????(??????, default(DateTime), 0);
                    return;
                }
                HateObject.???????????? ????????????2;
                if (this.GetDistance(??????) > MonsterObject.RangeHate && MonsterObject.HateObject.????????????.TryGetValue(??????, out ????????????2) && ????????????2.???????????? < MainProcess.CurrentTime)
                {
                    MonsterObject.HateObject.????????????(??????);
                }
            }
        }


        public void ?????????????????????(MapObject ??????)
        {
            if (this.NeighborsSneak.Contains(??????))
            {
                this.?????????????????????(??????);
            }
        }


        public byte[] ??????Buff??????()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream(34))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write((byte)this.Buffs.Count);
                    foreach (KeyValuePair<ushort, BuffData> keyValuePair in this.Buffs)
                    {
                        binaryWriter.Write(keyValuePair.Value.Id.V);
                        binaryWriter.Write((int)keyValuePair.Value.Id.V);
                        binaryWriter.Write(keyValuePair.Value.????????????.V);
                        binaryWriter.Write((int)keyValuePair.Value.????????????.V.TotalMilliseconds);
                        binaryWriter.Write((int)keyValuePair.Value.????????????.V.TotalMilliseconds);
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }


        public byte[] ??????Buff??????()
        {
            byte[] result;
            using (MemoryStream memoryStream = new MemoryStream(34))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(ObjectId);
                    int num = 0;
                    foreach (KeyValuePair<ushort, BuffData> keyValuePair in this.Buffs)
                    {
                        binaryWriter.Write(keyValuePair.Value.Id.V);
                        binaryWriter.Write((int)keyValuePair.Value.Id.V);
                        if (++num >= 5)
                        {
                            break;
                        }
                    }
                    result = memoryStream.ToArray();
                }
            }
            return result;
        }
    }
}
