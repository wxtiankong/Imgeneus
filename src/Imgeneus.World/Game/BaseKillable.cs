﻿using Imgeneus.Database.Constants;
using Imgeneus.Database.Preload;
using Imgeneus.World.Game.Buffs;
using Imgeneus.World.Game.Country;
using Imgeneus.World.Game.Elements;
using Imgeneus.World.Game.Health;
using Imgeneus.World.Game.Inventory;
using Imgeneus.World.Game.Levelling;
using Imgeneus.World.Game.Monster;
using Imgeneus.World.Game.Movement;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Speed;
using Imgeneus.World.Game.Stats;
using Imgeneus.World.Game.Zone;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Imgeneus.World.Game
{
    /// <summary>
    /// Abstract entity, that can be killed. Implements common features for killable object.
    /// </summary>
    public abstract class BaseKillable : IKillable, IMapMember
    {
        protected readonly IDatabasePreloader _databasePreloader;
        public ICountryProvider CountryProvider { get; private set; }
        public IStatsManager StatsManager { get; private set; }
        public IHealthManager HealthManager { get; private set; }
        public ILevelProvider LevelProvider { get; private set; }
        public IBuffsManager BuffsManager { get; private set; }
        public IElementProvider ElementProvider { get; private set; }
        public IMovementManager MovementManager { get; private set; }

        public BaseKillable(IDatabasePreloader databasePreloader, ICountryProvider countryProvider, IStatsManager statsManager, IHealthManager healthManager, ILevelProvider levelProvider, IBuffsManager buffsManager, IElementProvider elementProvider, IMovementManager movementManager)
        {
            _databasePreloader = databasePreloader;
            CountryProvider = countryProvider;
            StatsManager = statsManager;
            HealthManager = healthManager;
            LevelProvider = levelProvider;
            BuffsManager = buffsManager;
            ElementProvider = elementProvider;
            MovementManager = movementManager;
        }

        private int _id;

        /// <inheritdoc />
        public int Id
        {
            get => _id;
            set
            {
                if (_id == 0)
                {
                    _id = value;
                }
                else
                {
                    throw new ArgumentException("Id can not be set twice.");
                }
            }
        }

        #region Map

        private Map _map;
        public Map Map
        {
            get => _map;

            set
            {
                _map = value;

                if (_map != null) // Map is set to null, when character is disposed.
                    OnMapSet();
            }
        }

        /// <summary>
        /// Call it as soon as map set.
        /// </summary>
        protected virtual void OnMapSet()
        {
        }

        public int CellId { get; set; } = -1;

        public int OldCellId { get; set; } = -1;

        #endregion

        #region Death

        /// <inheritdoc />
        public event Action<IKillable, IKiller> OnDead;

        /// <summary>
        /// Collection of entities, that made damage to this killable.
        /// </summary>
        public ConcurrentDictionary<IKiller, int> DamageMakers { get; private set; } = new ConcurrentDictionary<IKiller, int>();

        /// <summary>
        /// IKiller, that made max damage.
        /// </summary>
        protected IKiller MaxDamageMaker
        {
            get
            {
                IKiller maxDamageMaker = DamageMakers.First().Key;
                int damage = DamageMakers.First().Value;
                foreach (var dmg in DamageMakers)
                {
                    if (dmg.Value > damage)
                    {
                        damage = dmg.Value;
                        maxDamageMaker = dmg.Key;
                    }
                }

                return maxDamageMaker;
            }
        }

        protected bool _isDead;

        /// <inheritdoc />
        public bool IsDead
        {
            get => _isDead;
            protected set
            {
                _isDead = value;

                if (_isDead)
                {
                    var killer = MaxDamageMaker;
                    OnDead?.Invoke(this, killer);
                    DamageMakers.Clear();

                    // Generate drop.
                    var dropItems = GenerateDrop(killer);
                    if (dropItems.Count > 0 && killer is Character)
                    {
                        var dropOwner = killer as Character;
                        if (dropOwner.Party is null)
                        {
                            AddItemsDropOnMap(dropItems, dropOwner);
                        }
                        else
                        {
                            var notDistributedItems = dropOwner.Party.DistributeDrop(dropItems, dropOwner);
                            AddItemsDropOnMap(notDistributedItems, dropOwner);

                        }
                    }

                    // Update quest.
                    if (this is Mob && killer is Character)
                    {
                        var character = killer as Character;
                        var mob = this as Mob;
                        if (character.Party is null)
                        {
                            character.UpdateQuestMobCount(mob.MobId);
                        }
                        else
                        {
                            foreach (var m in character.Party.Members)
                            {
                                if (m.Map == character.Map)
                                    m.UpdateQuestMobCount(mob.MobId);
                            }
                        }
                    }

                    // Clear buffs.
                    var buffs = BuffsManager.ActiveBuffs.Where(b => b.ShouldClearAfterDeath).ToList();
                    foreach (var b in buffs)
                        b.CancelBuff();
                }
            }
        }

        /// <summary>
        /// Add items on map.
        /// </summary>
        private void AddItemsDropOnMap(IList<Item> dropItems, Character owner)
        {
            byte i = 0;
            foreach (var itm in dropItems)
            {
                Map.AddItem(new MapItem(itm, owner, PosX + i, PosY, PosZ));
                i++;
            }
        }

        /// <summary>
        /// Generates drop for killer.
        /// </summary>
        protected abstract IList<Item> GenerateDrop(IKiller killer);

        #endregion

        #region Position

        /// <inheritdoc />
        public float PosX { get => MovementManager.PosX; }

        /// <inheritdoc />
        public float PosY { get => MovementManager.PosY; }

        /// <inheritdoc />
        public float PosZ { get => MovementManager.PosZ; }

        /// <inheritdoc />
        public ushort Angle { get => MovementManager.Angle; }

        #endregion

        #region Untouchable

        ///  <inheritdoc/>
        public virtual bool IsUntouchable { get; private set; }

        #endregion

        #region Absorption

        public ushort Absorption { get => StatsManager.Absorption; }

        #endregion

        #region Resurrect

        /// <inheritdoc />
        public event Action<IKillable> OnRebirthed;

        /// <inheritdoc />
        public void Rebirth(ushort mapId, float x, float y, float z)
        {
            HealthManager.IncreaseHP(HealthManager.MaxHP);
            HealthManager.CurrentMP = HealthManager.MaxMP;
            HealthManager.CurrentSP = HealthManager.MaxSP;
            IsDead = false;

            MovementManager.PosX = x;
            MovementManager.PosY = y;
            MovementManager.PosZ = z;

            OnRebirthed?.Invoke(this);

            if (mapId != Map.Id)
            {
                (this as Character).Teleport(mapId, x, y, z);
            }
        }

        #endregion
    }
}
