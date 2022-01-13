﻿using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.DatabaseBackgroundService.Handlers;
using Imgeneus.Network.Packets.Game;
using Imgeneus.World.Game.Stats;
using System;
using System.Linq;

namespace Imgeneus.World.Game.Player
{
    public partial class Character
    {
        #region Character info

        public string Name { get; set; } = "";
        public Fraction Country { get; set; }
        public ushort MapId { get; private set; }
        public Race Race { get; set; }
        public CharacterProfession Class { get; set; }
        public byte Hair { get; set; }
        public byte Face { get; set; }
        public byte Height { get; set; }
        public Gender Gender { get; set; }
        public ushort SkillPoint { get; private set; }
        public uint Exp { get; private set; }
        public ushort Kills { get; private set; }
        public ushort Deaths { get; private set; }
        public ushort Victories { get; private set; }
        public ushort Defeats { get; private set; }
        public bool IsAdmin { get; set; }
        public bool IsRename { get; set; }

        /// <summary>
        /// Account points, used for item mall or online shop purchases.
        /// </summary>
        public uint Points { get; private set; }

        private byte[] _nameAsByteArray;
        public byte[] NameAsByteArray
        {
            get
            {
                if (_nameAsByteArray is null)
                {
                    _nameAsByteArray = new byte[21];

                    var chars = Name.ToCharArray(0, Name.Length);
                    for (var i = 0; i < chars.Length; i++)
                    {
                        _nameAsByteArray[i] = (byte)chars[i];
                    }
                }
                return _nameAsByteArray;
            }
        }

        #endregion

        #region Max HP & SP & MP

        /// <summary>
        /// Gets the character's primary stat
        /// </summary>
        public CharacterStatEnum GetPrimaryStat()
        {
            var defaultStat = _characterConfig.DefaultStats.First(s => s.Job == Class);

            switch (defaultStat.MainStat)
            {
                case 0:
                    return CharacterStatEnum.Strength;

                case 1:
                    return CharacterStatEnum.Dexterity;

                case 2:
                    return CharacterStatEnum.Reaction;

                case 3:
                    return CharacterStatEnum.Intelligence;

                case 4:
                    return CharacterStatEnum.Wisdom;

                case 5:
                    return CharacterStatEnum.Luck;

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Gets the character's primary stat
        /// </summary>
        public CharacterAttributeEnum GetAttributeByStat(CharacterStatEnum stat)
        {
            switch (stat)
            {
                case CharacterStatEnum.Strength:
                    return CharacterAttributeEnum.Strength;

                case CharacterStatEnum.Dexterity:
                    return CharacterAttributeEnum.Dexterity;

                case CharacterStatEnum.Reaction:
                    return CharacterAttributeEnum.Reaction;

                case CharacterStatEnum.Intelligence:
                    return CharacterAttributeEnum.Intelligence;

                case CharacterStatEnum.Wisdom:
                    return CharacterAttributeEnum.Wisdom;

                case CharacterStatEnum.Luck:
                    return CharacterAttributeEnum.Luck;

                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Increases a character's main stat by a certain amount
        /// </summary>
        /// <param name="amount">Decrease amount</param>
        public void IncreasePrimaryStat(ushort amount = 1)
        {
            var primaryAttribute = GetPrimaryStat();

            switch (primaryAttribute)
            {
                case CharacterStatEnum.Strength:
                    StatsManager.TrySetStats(str: (ushort)(StatsManager.Strength + amount));
                    break;

                case CharacterStatEnum.Dexterity:
                    StatsManager.TrySetStats(dex: (ushort)(StatsManager.Dexterity + amount));
                    break;

                case CharacterStatEnum.Reaction:
                    StatsManager.TrySetStats(rec: (ushort)(StatsManager.Reaction + amount));
                    break;

                case CharacterStatEnum.Intelligence:
                    StatsManager.TrySetStats(intl: (ushort)(StatsManager.Intelligence + amount));
                    break;

                case CharacterStatEnum.Wisdom:
                    StatsManager.TrySetStats(wis: (ushort)(StatsManager.Wisdom + amount));
                    break;

                case CharacterStatEnum.Luck:
                    StatsManager.TrySetStats(luc: (ushort)(StatsManager.Luck + amount));
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Decreases a character's main stat by a certain amount
        /// </summary>
        /// <param name="amount">Decrease amount</param>
        public void DecreasePrimaryStat(ushort amount = 1)
        {
            var primaryAttribute = GetPrimaryStat();

            switch (primaryAttribute)
            {
                case CharacterStatEnum.Strength:
                    StatsManager.TrySetStats(str: (ushort)(StatsManager.Strength - amount));
                    break;

                case CharacterStatEnum.Dexterity:
                    StatsManager.TrySetStats(dex: (ushort)(StatsManager.Dexterity - amount));
                    break;

                case CharacterStatEnum.Reaction:
                    StatsManager.TrySetStats(rec: (ushort)(StatsManager.Reaction - amount));
                    break;

                case CharacterStatEnum.Intelligence:
                    StatsManager.TrySetStats(intl: (ushort)(StatsManager.Intelligence - amount));
                    break;

                case CharacterStatEnum.Wisdom:
                    StatsManager.TrySetStats(wis: (ushort)(StatsManager.Wisdom - amount));
                    break;

                case CharacterStatEnum.Luck:
                    StatsManager.TrySetStats(luc: (ushort)(StatsManager.Luck - amount));
                    break;

                default:
                    break;
            }
        }
 
        #endregion

        #region Defense & Resistance

        /// <summary>
        /// Physical defense.
        /// </summary>
        public override int Defense
        {
            get
            {
                return StatsManager.TotalRec + StatsManager.ExtraDefense;
            }
        }

        /// <summary>
        /// Magic resistance.
        /// </summary>
        public override int Resistance
        {
            get
            {
                return StatsManager.TotalWis + StatsManager.ExtraResistance;
            }
        }

        #endregion

        #region Attack & Move speed

        /// <summary>
        /// Pure weapon speed without any gems or buffs.
        /// </summary>
        private byte _weaponSpeed;

        /// <summary>
        /// Sets weapon speed.
        /// </summary>
        private void SetWeaponSpeed(byte speed)
        {
            _weaponSpeed = speed;
            InvokeAttackOrMoveChanged();
        }

        private int NextAttackTime
        {
            get
            {
                switch (AttackSpeed)
                {
                    case AttackSpeed.ExteremelySlow:
                        return 4000;

                    case AttackSpeed.VerySlow:
                        return 3750;

                    case AttackSpeed.Slow:
                        return 3500;

                    case AttackSpeed.ABitSlow:
                        return 3250;

                    case AttackSpeed.Normal:
                        return 3000;

                    case AttackSpeed.ABitFast:
                        return 2750;

                    case AttackSpeed.Fast:
                        return 2500;

                    case AttackSpeed.VeryFast:
                        return 2250;

                    case AttackSpeed.ExteremelyFast:
                        return 2000;

                    default:
                        return 2000;
                }
            }
        }

        /// <summary>
        /// How fast character can make new hit.
        /// </summary>
        public override AttackSpeed AttackSpeed
        {
            get
            {
                if (_weaponSpeed == 0)
                    return AttackSpeed.None;

                var weaponType = InventoryManager.Weapon.ToPassiveSkillType();
                _weaponSpeedPassiveSkillModificator.TryGetValue(weaponType, out var passiveSkillModifier);

                var finalSpeed = _weaponSpeed + _attackSpeedModifier + passiveSkillModifier;

                if (finalSpeed < 0)
                    return AttackSpeed.ExteremelySlow;

                if (finalSpeed > 9)
                    return AttackSpeed.ExteremelyFast;

                return (AttackSpeed)finalSpeed;
            }
        }

        private int _moveSpeed = 2; // 2 == normal by default.
        /// <summary>
        /// How fast character moves.
        /// </summary>
        public override int MoveSpeed
        {
            protected set
            {
                if (_moveSpeed == value)
                    return;

                if (value < 0)
                    value = 0;

                _moveSpeed = value;
                InvokeAttackOrMoveChanged();
            }
            get
            {
                if (ActiveBuffs.Any(b => b.StateType == StateType.Sleep || b.StateType == StateType.Stun || b.StateType == StateType.Immobilize))
                    return (int)MoveSpeedEnum.CanNotMove;

                if (StealthManager.IsStealth)
                    return (int)MoveSpeedEnum.Normal;

                if (IsOnVehicle)
                    return (int)MoveSpeedEnum.VeryFast;

                return _moveSpeed;
            }
        }

        #endregion

        #region Min/Max Attack & Magic attack

        /// <summary>
        /// Calculates character attack, based on character profession.
        /// </summary>
        private int GetCharacterAttack()
        {
            int characterAttack;
            switch (Class)
            {
                case CharacterProfession.Fighter:
                case CharacterProfession.Defender:
                case CharacterProfession.Ranger:
                    characterAttack = (int)(Math.Floor(1.3 * StatsManager.TotalStr) + Math.Floor(0.25 * StatsManager.TotalDex));
                    break;

                case CharacterProfession.Mage:
                case CharacterProfession.Priest:
                    characterAttack = (int)(Math.Floor(1.3 * StatsManager.TotalInt) + Math.Floor(0.2 * StatsManager.TotalWis));
                    break;

                case CharacterProfession.Archer:
                    characterAttack = (int)(StatsManager.TotalStr + Math.Floor(0.3 * StatsManager.TotalLuc) + Math.Floor(0.2 * StatsManager.TotalDex));
                    break;

                default:
                    throw new NotImplementedException("Not implemented job.");
            }

            return characterAttack;
        }

        /// <summary>
        /// Min physical attack.
        /// </summary>
        public int MinAttack
        {
            get
            {
                var weaponAttack = InventoryManager.Weapon != null ? InventoryManager.Weapon.MinAttack : 0;
                int characterAttack = 0;

                if (Class == CharacterProfession.Fighter ||
                    Class == CharacterProfession.Defender ||
                    Class == CharacterProfession.Ranger ||
                    Class == CharacterProfession.Archer)
                {
                    characterAttack = GetCharacterAttack();
                }

                return weaponAttack + characterAttack + _skillPhysicalAttackPower;
            }
        }

        /// <summary>
        /// Max physical attack.
        /// </summary>
        public int MaxAttack
        {
            get
            {
                var weaponAttack = InventoryManager.Weapon != null ? InventoryManager.Weapon.MaxAttack : 0;
                int characterAttack = 0;

                if (Class == CharacterProfession.Fighter ||
                    Class == CharacterProfession.Defender ||
                    Class == CharacterProfession.Ranger ||
                    Class == CharacterProfession.Archer)
                {
                    characterAttack = GetCharacterAttack();
                }

                return weaponAttack + characterAttack + _skillPhysicalAttackPower;
            }
        }

        /// <summary>
        /// Min magic attack.
        /// </summary>
        public int MinMagicAttack
        {
            get
            {
                var weaponAttack = InventoryManager.Weapon != null ? InventoryManager.Weapon.MinAttack : 0;
                int characterAttack = 0;

                if (Class == CharacterProfession.Mage ||
                    Class == CharacterProfession.Priest)
                {
                    characterAttack = GetCharacterAttack();
                }

                return weaponAttack + characterAttack + _skillMagicAttackPower;
            }
        }

        /// <summary>
        /// Max magic attack.
        /// </summary>
        public int MaxMagicAttack
        {
            get
            {
                var weaponAttack = InventoryManager.Weapon != null ? InventoryManager.Weapon.MaxAttack : 0;
                int characterAttack = 0;

                if (Class == CharacterProfession.Mage ||
                    Class == CharacterProfession.Priest)
                {
                    characterAttack = GetCharacterAttack();
                }

                return weaponAttack + characterAttack + _skillMagicAttackPower;
            }
        }

        #endregion

        #region Elements

        /// <inheritdoc />
        public override Element DefenceElement
        {
            get
            {
                if (RemoveElement)
                    return Element.None;

                if (DefenceSkillElement != Element.None)
                    return DefenceSkillElement;

                if (InventoryManager.Armor is null)
                    return Element.None;

                return InventoryManager.Armor.Element;
            }
        }

        /// <inheritdoc />
        public override Element AttackElement
        {
            get
            {
                if (AttackSkillElement != Element.None)
                    return AttackSkillElement;

                if (InventoryManager.Weapon is null)
                    return Element.None;

                return InventoryManager.Weapon.Element;
            }
        }

        #endregion

        #region Reset stats

        public void ResetStats()
        {
            var defaultStat = _characterConfig.DefaultStats.First(s => s.Job == Class);
            var statPerLevel = _characterConfig.GetLevelStatSkillPoints(LevelingManager.Grow).StatPoint;

            StatsManager.TrySetStats(defaultStat.Str,
                                     defaultStat.Dex,
                                     defaultStat.Rec,
                                     defaultStat.Int,
                                     defaultStat.Luc,
                                     (ushort)((LevelProvider.Level - 1) * statPerLevel)); // Level - 1, because we are starting with 1 level.

            IncreasePrimaryStat((ushort)(LevelProvider.Level - 1));

            _taskQueue.Enqueue(ActionType.UPDATE_STATS, Id, StatsManager.Strength, StatsManager.Dexterity, StatsManager.Reaction, StatsManager.Intelligence, StatsManager.Wisdom, StatsManager.Luck, StatsManager.StatPoint);
            _packetsHelper.SendResetStats(Client, this);
            SendAdditionalStats();
        }

        #endregion

        #region Attributes

        /// <summary>
        /// Gets a character's attribute.
        /// </summary>
        public uint GetAttributeValue(CharacterAttributeEnum attribute)
        {
            switch (attribute)
            {
                case CharacterAttributeEnum.Grow:
                    return (uint)LevelingManager.Grow;

                case CharacterAttributeEnum.Level:
                    return LevelProvider.Level;

                case CharacterAttributeEnum.Money:
                    return Gold;

                case CharacterAttributeEnum.StatPoint:
                    return StatsManager.StatPoint;

                case CharacterAttributeEnum.SkillPoint:
                    return SkillPoint;

                case CharacterAttributeEnum.Strength:
                    return StatsManager.Strength;

                case CharacterAttributeEnum.Dexterity:
                    return StatsManager.Dexterity;

                case CharacterAttributeEnum.Reaction:
                    return StatsManager.Reaction;

                case CharacterAttributeEnum.Intelligence:
                    return StatsManager.Intelligence;

                case CharacterAttributeEnum.Luck:
                    return StatsManager.Luck;

                case CharacterAttributeEnum.Wisdom:
                    return StatsManager.Wisdom;

                // TODO: Investigate what these attributes represent
                case CharacterAttributeEnum.Hg:
                case CharacterAttributeEnum.Vg:
                case CharacterAttributeEnum.Cg:
                case CharacterAttributeEnum.Og:
                case CharacterAttributeEnum.Ig:
                    return 0;

                case CharacterAttributeEnum.Exp:
                    return Exp;

                case CharacterAttributeEnum.Kills:
                    return Kills;

                case CharacterAttributeEnum.Deaths:
                    return Deaths;

                default:
                    return 0;
            }
        }

        #endregion

        #region Stat and Skill Points

        /// <summary>
        /// Increases the player's stat points by a certain amount
        /// </summary>
        /// <param name="amount"></param>
        //public void IncreaseStatPoint(ushort amount) => StatsManager.TrySetStatPoint((ushort)(StatsManager.StatPoint + amount));

        /// <summary>
        /// Set the skill points amount
        /// </summary>
        public void SetSkillPoint(ushort skillPoint)
        {
            SkillPoint = skillPoint;

            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_SKILLPOINT, Id, SkillPoint);
        }

        /// <summary>
        /// Increases the player's skill points by a certain amount
        /// </summary>
        /// <param name="amount"></param>
        public void IncreaseSkillPoint(ushort amount) => SetSkillPoint(SkillPoint += amount);

        #endregion

        #region Kills and Deaths

        /// <summary>
        /// Sets the kill count
        /// </summary>
        public void SetKills(ushort kills)
        {
            Kills = kills;

            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_KILLS, Id, Kills);
        }

        /// <summary>
        /// Sets the death count
        /// </summary>
        public void SetDeaths(ushort deaths)
        {
            Deaths = deaths;

            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_DEATHS, Id, Deaths);
        }

        #endregion

        #region Wins & Loses

        /// <summary>
        /// Sets the number of duel victories.
        /// </summary>
        public void SetVictories(ushort victories)
        {
            Victories = victories;

            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_VICTORIES, Id, Victories);
        }

        /// <summary>
        /// Sets the number of duel defeats.
        /// </summary>
        public void SetDefeats(ushort defeats)
        {
            Defeats = defeats;

            _taskQueue.Enqueue(ActionType.SAVE_CHARACTER_DEFEATS, Id, Defeats);
        }

        #endregion

        #region Account Points

        /// <summary>
        /// Attempts to set the player's account points.
        /// </summary>
        /// <param name="points">Points to set.</param>
        public void SetPoints(uint points)
        {
            Points = points;

            _taskQueue.Enqueue(ActionType.SAVE_ACCOUNT_POINTS, Client.UserId, Points);
            SendAccountPoints();
        }

        #endregion
    }
}
