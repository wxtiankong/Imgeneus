﻿using Imgeneus.Database.Constants;
using Imgeneus.Database.Entities;
using Imgeneus.World.Game;
using Imgeneus.World.Game.Skills;
using System.ComponentModel;
using Xunit;

namespace Imgeneus.World.Tests.CharacterTests
{
    public class CharacterSkillsTest : BaseTest
    {
        [Fact]
        [Description("Dispel should clear debuffs.")]
        public void DispelTest()
        {
            var character = CreateCharacter();

            character.BuffsManager.AddBuff(new Skill(Panic_Lvl1, 0, 0), null);
            Assert.Single(character.BuffsManager.ActiveBuffs);

            character.SkillsManager.UsedDispelSkill(new Skill(Dispel, 0, 0), character);
            Assert.Empty(character.BuffsManager.ActiveBuffs);
        }

        [Fact]
        [Description("With untouchable all attacks should miss.")]
        public void UntouchableTest()
        {
            var character = CreateCharacter();

            var character2 = CreateCharacter();
            var attackSuccess = (character2 as IKiller).AttackManager.AttackSuccessRate(character, TypeAttack.ShootingAttack, new Skill(BullsEye, 0, 0));
            Assert.True(attackSuccess); // Bull eye has 100% success rate.

            // Use untouchable.
            character.BuffsManager.AddBuff(new Skill(Untouchable, 0, 0), null);
            Assert.Single(character.BuffsManager.ActiveBuffs);

            attackSuccess = (character2 as IKiller).AttackManager.AttackSuccessRate(character, TypeAttack.ShootingAttack, new Skill(BullsEye, 0, 0));
            Assert.False(attackSuccess); // When target is untouchable, bull eye is going to fail.
        }

        [Fact]
        [Description("Archer should miss if fighter used 'FleetFoot' skill.")]
        public void FleetFootTest()
        {
            var fighter = CreateCharacter();
            var archer = CreateCharacter(profession: CharacterProfession.Archer);

            fighter.BuffsManager.AddBuff(new Skill(FleetFoot, 0, 0), null);
            Assert.Single(fighter.BuffsManager.ActiveBuffs);

            var attackSuccess = (archer as IKiller).AttackManager.AttackSuccessRate(fighter, TypeAttack.ShootingAttack);
            Assert.False(attackSuccess);
        }
    }
}
