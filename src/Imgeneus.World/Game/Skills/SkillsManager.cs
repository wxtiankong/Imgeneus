﻿using Imgeneus.Core.Extensions;
using Imgeneus.Database;
using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.Database.Preload;
using Imgeneus.World.Game.Attack;
using Imgeneus.World.Game.Buffs;
using Imgeneus.World.Game.Elements;
using Imgeneus.World.Game.Health;
using Imgeneus.World.Game.Player;
using Imgeneus.World.Game.Stats;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace Imgeneus.World.Game.Skills
{
    public class SkillsManager : ISkillsManager
    {
        private readonly ILogger<SkillsManager> _logger;
        private readonly IDatabasePreloader _databasePreloader;
        private readonly IDatabase _database;
        private readonly IHealthManager _healthManager;
        private readonly IAttackManager _attackManager;
        private readonly IBuffsManager _buffsManager;
        private readonly IStatsManager _statsManager;
        private readonly IElementProvider _elementProvider;

        private int _ownerId;

        public SkillsManager(ILogger<SkillsManager> logger, IDatabasePreloader databasePreloader, IDatabase database, IHealthManager healthManager, IAttackManager attackManager, IBuffsManager buffsManager, IStatsManager statsManager, IElementProvider elementProvider)
        {
            _logger = logger;
            _databasePreloader = databasePreloader;
            _database = database;
            _healthManager = healthManager;
            _attackManager = attackManager;
            _buffsManager = buffsManager;
            _statsManager = statsManager;
            _elementProvider = elementProvider;

            _castTimer.Elapsed += CastTimer_Elapsed;

#if DEBUG
            _logger.LogDebug("SkillsManager {hashcode} created", GetHashCode());
#endif
        }

#if DEBUG
        ~SkillsManager()
        {
            _logger.LogDebug("SkillsManager {hashcode} collected by GC", GetHashCode());
        }
#endif

        #region Init & Clear

        public void Init(int ownerId, IEnumerable<Skill> skills, ushort skillPoint = 0)
        {
            _ownerId = ownerId;
            SkillPoints = skillPoint;

            foreach (var skill in skills)
                Skills.TryAdd(skill.Number, skill);
        }

        public async Task Clear()
        {
            Skills.Clear();
        }

        public void Dispose()
        {
            _castTimer.Elapsed -= CastTimer_Elapsed;
        }

        #endregion

        #region Events

        public event Action<int, IKillable, Skill, AttackResult> OnUsedSkill;

        public event Action<int, IKillable, Skill, AttackResult> OnUsedRangeSkill;

        #endregion

        #region Skill points

        public ushort SkillPoints { get; private set; }

        public async Task<bool> TrySetSkillPoints(ushort value)
        {
            if (SkillPoints == value)
                return true;

            var character = await _database.Characters.FindAsync(_ownerId);
            if (character is null)
                return false;

            character.SkillPoint = value;

            var ok = (await _database.SaveChangesAsync()) > 0;
            if (ok)
                SkillPoints = value;

            return ok;
        }

        #endregion

        #region Skills

        public ConcurrentDictionary<byte, Skill> Skills { get; private set; } = new ConcurrentDictionary<byte, Skill>();

        public async Task<(bool Ok, Skill Skill)> TryLearnNewSkill(ushort skillId, byte skillLevel)
        {
            if (Skills.Values.Any(s => s.SkillId == skillId && s.SkillLevel == skillLevel))
            {
                _logger.LogWarning("Character {characterId} has already learned skill {skillId} with level {skillLevel}", _ownerId, skillId, skillLevel);
                return (false, null);
            }

            // Find learned skill.
            var dbSkill = _databasePreloader.Skills[(skillId, skillLevel)];
            if (SkillPoints < dbSkill.SkillPoint)
            {
                _logger.LogWarning("Character {characterId} has not enough skill points  for skill {skillId} with level {skillLevel}", _ownerId, skillId, skillLevel);
                return (false, null);
            }

            byte skillNumber = 0;

            // Find out if the character has already learned the same skill, but lower level.
            var isSkillLearned = Skills.Values.FirstOrDefault(s => s.SkillId == skillId);
            // If there is skill of lower level => delete it.
            if (isSkillLearned != null)
            {
                var learnedSkill = _databasePreloader.Skills[(isSkillLearned.SkillId, isSkillLearned.SkillLevel)];
                if (learnedSkill is null)
                {
                    _logger.LogWarning("Learned skill {skillId} {skillLevel} is not found in db for character {characterId}", isSkillLearned.SkillId, isSkillLearned.SkillLevel, _ownerId);
                    skillNumber = Skills.Values.Select(s => s.Number).Max();
                    skillNumber++;
                }
                else
                {
                    var skillToRemove = _database.CharacterSkills.FirstOrDefault(s => s.CharacterId == _ownerId && s.SkillId == learnedSkill.Id);
                    if (skillToRemove is null)
                    {
                        _logger.LogError("Could not remove old skill {skillId} with level {skillLevel} from db for character {characterId}", learnedSkill.SkillId, learnedSkill.SkillLevel, _ownerId);
                    }
                    else
                    {
                        _database.CharacterSkills.Remove(skillToRemove);
                    }

                    skillNumber = isSkillLearned.Number;
                }
            }
            // No such skill. Generate new number.
            else
            {
                if (Skills.Any())
                {
                    // Find the next skill number.
                    skillNumber = Skills.Values.Select(s => s.Number).Max();
                    skillNumber++;
                }
                else
                {
                    // No learned skills at all.
                }
            }

            // Save char and learned skill.
            var skillToAdd = new DbCharacterSkill()
            {
                CharacterId = _ownerId,
                SkillId = dbSkill.Id,
                Number = skillNumber
            };

            _database.CharacterSkills.Add(skillToAdd);

            var ok = (await _database.SaveChangesAsync()) > 0;
            if (!ok)
            {
                _logger.LogError("Could not save skill {skillId} with level {skillLevel} for character {characterId}", skillId, skillLevel, _ownerId);
                return (false, null);
            }

            // Remove previously learned skill.
            if (isSkillLearned != null)
                Skills.TryRemove(skillNumber, out var removed);

            await TrySetSkillPoints((ushort)(SkillPoints - dbSkill.SkillPoint));

            var skill = new Skill(dbSkill, skillNumber, 0);
            Skills.TryAdd(skillNumber, skill);

            _logger.LogDebug("Character {characterId} learned skill {skillId} of level {skillLevel}", _ownerId, skillId, skillLevel);

            // Activate passive skill as soon as it's learned.
            //if (skill.IsPassive)
            //UseSkill(skill);

            return (true, skill);
        }

        #endregion

        #region Casting

        /// <summary>
        /// The timer, that is starting skill after cast time.
        /// </summary>
        private Timer _castTimer = new Timer();

        /// <summary>
        /// Skill, that player tries to cast.
        /// </summary>
        private Skill _skillInCast;

        /// <summary>
        /// Target for which we are casting spell.
        /// </summary>
        private IKillable _targetInCast;

        /// <summary>
        /// Event, that is fired, when user starts casting.
        /// </summary>
        public event Action<int, IKillable, Skill> OnSkillCastStarted;

        public void StartCasting(Skill skill, IKillable target)
        {
            if (!CanUseSkill(skill, target, out var success))
                return;

            _skillInCast = skill;
            _targetInCast = target;
            _castTimer.Interval = skill.CastTime;
            _castTimer.Start();
            OnSkillCastStarted?.Invoke(_ownerId, _targetInCast, skill);
        }

        /// <summary>
        /// When time for casting has elapsed.
        /// </summary>
        private void CastTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _castTimer.Stop();
            CanUseSkill(_skillInCast, _targetInCast, out var success);

            _skillInCast = null;
            _targetInCast = null;
        }

        #endregion

        #region Use skill

        public bool CanUseSkill(Skill skill, IKillable target, out AttackSuccess success)
        {
            if ((skill.TargetType == TargetType.SelectedEnemy ||
                skill.TargetType == TargetType.AnyEnemy ||
                skill.TargetType == TargetType.EnemiesNearTarget)
                &&
                (target is null || target.IsDead /*|| (target is Mob && (target as Mob).Country == Country)*/))
            {
                success = AttackSuccess.WrongTarget;
                return false;
            }

            /*if (InventoryManager.Weapon is null || (!skill.RequiredWeapons.Contains(InventoryManager.Weapon.Type) && skill.RequiredWeapons.Count != 0))
            {
                SendSkillWrongEquipment(target, skill);
                return false;
            }*/

            /*if (skill.Number != ITEM_SKILL_NUMBER && skill.NeedShield && InventoryManager.Shield is null)
            {
                SendSkillWrongEquipment(target, skill);
                return false;
            }*/

            if (_healthManager.CurrentMP < skill.NeedMP || _healthManager.CurrentSP < skill.NeedSP)
            {
                success = AttackSuccess.NotEnoughMPSP;
                return false;
            }

            if ((skill.TypeAttack == TypeAttack.PhysicalAttack || skill.TypeAttack == TypeAttack.ShootingAttack) &&
                _buffsManager.ActiveBuffs.Any(b => b.StateType == StateType.Sleep || b.StateType == StateType.Stun || b.StateType == StateType.Silence))
            {
                success = AttackSuccess.CanNotAttack;
                return false;
            }

            if (skill.TypeAttack == TypeAttack.MagicAttack &&
                _buffsManager.ActiveBuffs.Any(b => b.StateType == StateType.Sleep || b.StateType == StateType.Stun || b.StateType == StateType.Darkness))
            {
                success = AttackSuccess.CanNotAttack;
                return false;
            }

            success = AttackSuccess.Normal;
            return true;
        }

        public void UseSkill(Skill skill, IKiller skillOwner, IKillable target = null)
        {
            if (!skill.IsPassive)
                _attackManager.StartAttack();
            //SendAttackStart();

            //if (!skill.IsPassive && !CanAttack(skill.Number, target))
            //  return;

            //if (skill.Number == ITEM_SKILL_NUMBER && !CanUseSkill(skill, target))
            // return;

            if (skill.NeedMP > 0 || skill.NeedSP > 0)
            {
                _healthManager.CurrentMP -= skill.NeedMP;
                _healthManager.CurrentSP -= skill.NeedSP;
                //SendUseSMMP(skill.NeedMP, skill.NeedSP);
            }

            int n = 0;
            do
            {
                var targets = new List<IKillable>();
                switch (skill.TargetType)
                {
                    case TargetType.None:
                    case TargetType.Caster:
                        targets.Add(skillOwner as IKillable);
                        break;

                    case TargetType.SelectedEnemy:
                        if (target != null)
                            targets.Add(target);
                        else
                            targets.Add(skillOwner as IKillable);
                        break;

                    case TargetType.PartyMembers:
                        var t = skillOwner as Character;
                        if (t.Party != null)
                            foreach (var member in t.Party.Members.Where(m => m.Map == t.Map && MathExtensions.Distance(t.PosX, m.PosX, t.PosZ, m.PosZ) < skill.ApplyRange).ToList())
                                targets.Add(member);
                        else
                            targets.Add(skillOwner as IKillable);
                        break;

                    case TargetType.EnemiesNearTarget:
                        /*var enemies = Map.Cells[CellId].GetEnemies(this, target, skill.ApplyRange);
                        foreach (var e in enemies)
                            targets.Add(e);*/
                        break;

                    default:
                        throw new NotImplementedException("Not implemented skill target.");
                }

                foreach (var t in targets)
                {
                    // While implementing multiple attack I commented this out. Maybe it's not needed.
                    //if (t.IsDead)
                    //continue;

                    if (skill.TypeAttack != TypeAttack.Passive && !_attackManager.AttackSuccessRate(t, skill.TypeAttack, skill))
                    {
                        if (target == t)
                            OnUsedSkill?.Invoke(_ownerId, t, skill, new AttackResult(AttackSuccess.Miss, new Damage(0, 0, 0)));
                        else
                            OnUsedRangeSkill?.Invoke(_ownerId, t, skill, new AttackResult(AttackSuccess.Miss, new Damage(0, 0, 0)));

                        continue;
                    }

                    var attackResult = _attackManager.CalculateAttackResult(skill, t, _elementProvider.AttackElement, _statsManager.MinAttack, _statsManager.MaxAttack, _statsManager.MinMagicAttack, _statsManager.MaxMagicAttack);

                    try
                    {
                        // First apply skill.
                        PerformSkill(skill, target, t, skillOwner, attackResult, n);

                        // Second decrease hp.
                        if (attackResult.Damage.HP > 0)
                            t.HealthManager.DecreaseHP(attackResult.Damage.HP, skillOwner);
                        if (attackResult.Damage.SP > 0)
                            t.HealthManager.CurrentSP -= attackResult.Damage.SP;
                        if (attackResult.Damage.MP > 0)
                            t.HealthManager.CurrentMP -= attackResult.Damage.MP;
                    }
                    catch (NotImplementedException)
                    {
                        _logger.LogError($"Not implemented skill type {skill.Type}");
                    }
                }

                n++;
            }
            while (n < skill.MultiAttack);
        }

        public void PerformSkill(Skill skill, IKillable initialTarget, IKillable target, IKiller skillOwner, AttackResult attackResult, int n = 0)
        {
            switch (skill.Type)
            {
                case TypeDetail.Buff:
                case TypeDetail.SubtractingDebuff:
                case TypeDetail.PeriodicalHeal:
                case TypeDetail.PeriodicalDebuff:
                case TypeDetail.PreventAttack:
                case TypeDetail.Immobilize:
                case TypeDetail.RemoveAttribute:
                case TypeDetail.ElementalAttack:
                case TypeDetail.ElementalProtection:
                case TypeDetail.Untouchable:
                    target.BuffsManager.AddActiveBuff(skill, skillOwner);
                    break;

                case TypeDetail.Healing:
                    attackResult = UsedHealingSkill(skill, target);
                    break;

                case TypeDetail.Dispel:
                    attackResult = UsedDispelSkill(skill, target);
                    break;

                case TypeDetail.Stealth:
                    attackResult = UsedStealthSkill(skill, target);
                    break;

                case TypeDetail.UniqueHitAttack:
                case TypeDetail.MultipleHitsAttack:
                    break;

                case TypeDetail.PassiveDefence:
                case TypeDetail.WeaponMastery:
                    //target.BuffsManager.AddActiveBuff(skill, this);
                    break;

                default:
                    throw new NotImplementedException("Not implemented skill type.");
            }

            if ((initialTarget == target || skillOwner == target) && n == 0)
                OnUsedSkill?.Invoke(_ownerId, initialTarget, skill, attackResult);
            else
                OnUsedRangeSkill?.Invoke(_ownerId, target, skill, attackResult);
        }

        /// <summary>
        /// Calculates healing result.
        /// </summary>
        public AttackResult UsedHealingSkill(Skill skill, IKillable target)
        {
            var healHP = _statsManager.TotalWis * 4 + skill.HealHP;
            var healSP = skill.HealSP;
            var healMP = skill.HealMP;
            AttackResult result = new AttackResult(AttackSuccess.Normal, new Damage((ushort)healHP, healSP, healMP));

            target.HealthManager.IncreaseHP(healHP);
            target.HealthManager.CurrentMP += healMP;
            target.HealthManager.CurrentSP += healSP;

            return result;
        }

        /// <summary>
        /// Makes target invisible.
        /// </summary>
        public AttackResult UsedStealthSkill(Skill skill, IKillable target)
        {
            //target.BuffsManager.AddActiveBuff(skill, this);
            return new AttackResult(AttackSuccess.Normal, new Damage());
        }

        /// <summary>
        /// Clears debuffs.
        /// </summary>
        public AttackResult UsedDispelSkill(Skill skill, IKillable target)
        {
            var debuffs = target.BuffsManager.ActiveBuffs.Where(b => b.IsDebuff).ToList();
            foreach (var debuff in debuffs)
            {
                debuff.CancelBuff();
            }

            return new AttackResult(AttackSuccess.Normal, new Damage());
        }

        #endregion
    }
}
