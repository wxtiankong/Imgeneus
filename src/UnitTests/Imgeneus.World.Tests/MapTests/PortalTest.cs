﻿using Imgeneus.World.Game.Country;
using Imgeneus.World.Game.Zone.Portals;
using Moq;
using System.ComponentModel;
using Xunit;
using SPortal = Parsec.Shaiya.Svmap.Portal;

namespace Imgeneus.World.Tests.MapTests
{
    public class PortalTest : BaseTest
    {
        [Fact]
        [Description("Character must have right level to enter portal.")]
        public void Portal_RightLevel()
        {
            var portalConfig = new SPortal()
            {
                MinLevel = 20,
                MaxLevel = 30,
            };
            var portal = new Portal(portalConfig);

            Assert.False(portal.IsRightLevel(9));
            Assert.False(portal.IsRightLevel(31));

            Assert.True(portal.IsRightLevel(20));
            Assert.True(portal.IsRightLevel(25));
            Assert.True(portal.IsRightLevel(30));
        }

        [Fact]
        [Description("Character must have right faction to enter portal.")]
        public void Portal_RightFaction()
        {
            //Portal portal;

            //var portalConfig = new Mock<SPortal>();
            //portalConfig.SetupGet(x => x.Faction)
            //            .Returns(0);
            //portal = new Portal(portalConfig.Object);
            //Assert.True(portal.IsSameFaction(CountryType.Light));
            //Assert.True(portal.IsSameFaction(CountryType.Dark));

            //portalConfig = new Mock<SPortal>();
            //portalConfig.SetupGet(x => x.Faction)
            //            .Returns(1);
            //portal = new Portal(portalConfig.Object);
            //Assert.True(portal.IsSameFaction(CountryType.Light));
            //Assert.False(portal.IsSameFaction(CountryType.Dark));

            //portalConfig = new Mock<SPortal>();
            //portalConfig.SetupGet(x => x.Faction)
            //            .Returns(2);
            //portal = new Portal(portalConfig.Object);
            //Assert.False(portal.IsSameFaction(CountryType.Light));
            //Assert.True(portal.IsSameFaction(CountryType.Dark));
        }
    }
}
