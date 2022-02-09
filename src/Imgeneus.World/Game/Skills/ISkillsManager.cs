﻿using Imgeneus.World.Game.Attack;
using Imgeneus.World.Game.Session;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Imgeneus.World.Game.Skills
{
    public interface ISkillsManager : ISessionedService, IDisposable
    {
        /// <summary>
        /// Number for skill, generated by using item from inventory.
        /// </summary>
        public const byte ITEM_SKILL_NUMBER = 254;

        /// <summary>
        /// Inits skill manager.
        /// </summary>
        /// <param name="ownerId">who is using skills</param>
        /// <param name="skills">initial skills</param>
        /// <param name="skillPoint">free skill points</param>
        void Init(int ownerId, IEnumerable<Skill> skills, ushort skillPoint = 0);

        /// <summary>
        /// Event, that is fired, when single target skill is used.
        /// </summary>
        event Action<int, IKillable, Skill, AttackResult> OnUsedSkill;

        /// <summary>
        /// Event, that is fired, when range skill is used.
        /// </summary>
        event Action<int, IKillable, Skill, AttackResult> OnUsedRangeSkill;

        /// <summary>
        /// Event, that is fired, when user starts casting.
        /// </summary>
        event Action<int, IKillable, Skill> OnSkillCastStarted;

        /// <summary>
        /// Free skill points.
        /// </summary>
        ushort SkillPoints { get; }

        /// <summary>
        /// Tries to set new value for skill points nad save it to db
        /// </summary>
        /// <param name="skillPoint">value of skill points</param>
        /// <returns>true if success</returns>
        bool TrySetSkillPoints(ushort skillPoint);

        /// <summary>
        /// Collection of available skills.
        /// </summary>
        ConcurrentDictionary<byte, Skill> Skills { get; }

        /// <summary>
        /// Player learns new skill.
        /// </summary>
        /// <param name="skillId">skill id</param>
        /// <param name="skillLevel">skill level</param>
        /// <returns>successful or not</returns>
        Task<(bool Ok, Skill Skill)> TryLearnNewSkill(ushort skillId, byte skillLevel);

        /// <summary>
        /// Checks if it's enough sp and mp in order to use a skill.
        /// </summary>
        /// <param name="skill">skill, that character wants to use</param>
        bool CanUseSkill(Skill skill, IKillable target, out AttackSuccess success);

        /// <summary>
        /// Use skill on <see cref="IKillable"/>
        /// </summary>
        void UseSkill(Skill skill, IKiller skillOwner, IKillable target = null);

        /// <summary>
        /// Performs side effect of skill.
        /// </summary>
        /// <param name="skill">skill</param>
        /// <param name="initialTarget">target, that was initially selected</param>
        /// <param name="target">current target, usually is the same as initialTarget, but if it's AoE (area of effect) skill, then can be different from initial target</param>
        /// <param name="skillOwner">who performes skill</param>
        /// <param name="attackResult">result after performing skill</param>
        /// <param name="n">How many times this skill was called, used in multi skills.</param>
        void PerformSkill(Skill skill, IKillable initialTarget, IKillable target, IKiller skillOwner, AttackResult attackResult, int n = 0);

        /// <summary>
        /// Starts casting.
        /// </summary>
        /// <param name="skill">skill, that we are casting</param>
        /// <param name="target">target for which, that we are casting</param>
        void StartCasting(Skill skill, IKillable target);

        /// <summary>
        /// Clears skills and adds skill points.
        /// </summary>
        Task<bool> TryResetSkills();

        /// <summary>
        /// Triggers send reset skills for player.
        /// </summary>
        event Action OnResetSkills;
    }
}
