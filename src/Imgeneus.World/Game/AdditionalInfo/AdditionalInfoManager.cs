﻿using Imgeneus.Database;
using Imgeneus.Database.Entities;
using Imgeneus.World.Game.Player.Config;
using Imgeneus.World.Game.Stats;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Imgeneus.World.Game.AdditionalInfo
{
    public class AdditionalInfoManager : IAdditionalInfoManager
    {
        private readonly ILogger<AdditionalInfoManager> _logger;
        private readonly ICharacterConfiguration _characterConfig;
        private readonly IDatabase _database;
        private int _ownerId;

        public AdditionalInfoManager(ILogger<AdditionalInfoManager> logger, ICharacterConfiguration characterConfiguration, IDatabase database)
        {
            _logger = logger;
            _characterConfig = characterConfiguration;
            _database = database;

#if DEBUG
            _logger.LogDebug("AdditionalInfoManager {hashcode} created", GetHashCode());
#endif
        }

#if DEBUG
        ~AdditionalInfoManager()
        {
            _logger.LogDebug("AdditionalInfoManager {hashcode} collected by GC", GetHashCode());
        }
#endif

        #region Init & Clear

        public void Init(int ownerId, Race race, CharacterProfession profession, byte hair, byte face, byte height, Gender gender, Mode grow)
        {
            _ownerId = ownerId;

            Race = race;
            Class = profession;
            Hair = hair;
            Face = face;
            Height = height;
            Gender = gender;
            Grow = grow;
        }

        #endregion

        public Race Race { get; set; }
        public CharacterProfession Class { get; set; }
        public byte Hair { get; set; }
        public byte Face { get; set; }
        public byte Height { get; set; }
        public Gender Gender { get; set; }

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

        #region Appearance

        public event Action<int, byte, byte, byte, byte> OnAppearanceChanged;
        public async Task ChangeAppearance(byte hair, byte face, byte size, byte sex)
        {
            Hair = hair;
            Face = face;
            Height = size;
            Gender = (Gender)sex;

            var character = await _database.Characters.FindAsync(_ownerId);
            if (character is null)
            {
                _logger.LogError("Character {id} is not found", _ownerId);
                return;
            }

            character.Face = Face;
            character.Hair = Hair;
            character.Height = Height;
            character.Gender = Gender;

            await _database.SaveChangesAsync();

            OnAppearanceChanged?.Invoke(_ownerId, Hair, Face, Height, (byte)Gender);
        }

        #endregion

        #region Grow

        public Mode Grow { get; private set; }

        public async Task<bool> TrySetGrow(Mode grow)
        {
            if (Grow == grow)
                return true;

            if (grow > Mode.Ultimate)
                return false;

            var character = await _database.Characters.FindAsync(_ownerId);
            if (character is null)
                return false;

            character.Mode = grow;

            var ok = (await _database.SaveChangesAsync()) > 0;
            if (ok)
                Grow = grow;

            return ok;
        }

        #endregion
    }
}
