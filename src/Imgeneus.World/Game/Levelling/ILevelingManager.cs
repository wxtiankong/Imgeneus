﻿using Imgeneus.World.Game.Session;
using System;

namespace Imgeneus.World.Game.Levelling
{
    public interface ILevelingManager : ISessionedService
    {
        /// <summary>
        /// Inits leveling manager.
        /// </summary>
        /// <param name="owner">character id</param>
        /// <param name="exp">exp stored in databse</param>
        void Init(int owner, uint exp);

        /// <summary>
        /// Current experience amount.
        /// </summary>
        uint Exp { get; }

        /// <summary>
        /// Minimum experience needed for current player's level
        /// </summary>
        uint MinLevelExp { get; }

        /// <summary>
        /// Experience needed to level up to next level
        /// </summary>
        uint NextLevelExp { get; }

        /// <summary>
        /// Event, that is fired, when exp changes.
        /// </summary>
        event Action<uint> OnExpChanged;

        /// <summary>
        /// Event that's fired when a player level's up
        /// </summary>
        event Action<int, ushort, ushort, ushort, uint, uint> OnLevelUp;

        /// <summary>
        /// Increases a character's main stat by a certain amount
        /// </summary>
        /// <param name="amount">Decrease amount</param>
        void IncreasePrimaryStat(ushort amount = 1);

        /// <summary>
        /// Decreases a character's main stat by a certain amount
        /// </summary>
        /// <param name="amount">Decrease amount</param>
        void DecreasePrimaryStat(ushort amount = 1);

        /// <summary>
        /// Attempts to set a new level for a character and handles the levelling logic (exp, stat points, skill points, etc)
        /// </summary>
        /// <param name="newLevel">New player level</param>
        /// <param name="changedByAdmin">Indicates whether the level change was issued by an admin or not.</param>
        /// <returns>Success status indicating whether it's possible to set the new level or not.</returns>
        bool TryChangeLevel(ushort newLevel, bool changedByAdmin = false);

        /// <summary>
        /// Attempts to set the experience of a player and updates the player's level if necessary.
        /// </summary>
        /// <param name="exp">New player experience</param>
        /// <param name="changedByAdmin">Indicates whether the level change was issued by an admin or not.</param>
        /// <returns>Success status indicating whether it's possible to set the new level or not.</returns>
        bool TryChangeExperience(uint exp, bool changedByAdmin = false);

        /// <summary>
        /// Gives a player the experience gained by killing a mob
        /// </summary>
        /// <param name="mobLevel">Killed mob's level</param>
        /// <param name="mobExp">Killed mob's experience</param>
        void AddMobExperience(ushort mobLevel, ushort mobExp);
    }
}