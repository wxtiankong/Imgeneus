﻿using Imgeneus.Database.Entities;
using Imgeneus.Network.Data;
using Imgeneus.Network.PacketProcessor;
using Imgeneus.Network.Packets;
using Imgeneus.World.Game.Blessing;
using Imgeneus.World.Game.Country;
using System;

namespace Imgeneus.World.Game.Player
{
    public partial class Character
    {
        private void OnDarkBlessChanged(BlessArgs args)
        {
            if (CountryProvider.Country == CountryType.Dark)
                AddBlessBonuses(args);

            if (Client != null)
                SendBlessUpdate(1, args.NewValue);
        }

        private void OnLightBlessChanged(BlessArgs args)
        {
            if (CountryProvider.Country == CountryType.Light)
                AddBlessBonuses(args);

            if (Client != null)
                SendBlessUpdate(0, args.NewValue);
        }

        /// <summary>
        /// Sends update of bonuses, based on bless amount change.
        /// </summary>
        /// <param name="args">bless args</param>
        private void AddBlessBonuses(BlessArgs args)
        {
            if (args.OldValue >= Bless.MAX_HP_SP_MP && args.NewValue < Bless.MAX_HP_SP_MP)
            {
                HealthManager.ExtraHP -= HealthManager.ConstHP / 5;
                HealthManager.ExtraMP -= HealthManager.ConstMP / 5;
                HealthManager.ExtraSP -= HealthManager.ConstSP / 5;
            }
            if (args.OldValue < Bless.MAX_HP_SP_MP && args.NewValue >= Bless.MAX_HP_SP_MP)
            {
                HealthManager.ExtraHP += HealthManager.ConstHP / 5;
                HealthManager.ExtraMP += HealthManager.ConstMP / 5;
                HealthManager.ExtraSP += HealthManager.ConstSP / 5;
            }
        }

        /// <summary>
        /// Sends new bless amount.
        /// </summary>
        private void SendBlessUpdate(byte fraction, int amount)
        {
            using var packet = new ImgeneusPacket(PacketType.BLESS_UPDATE);
            packet.Write(fraction);
            packet.Write(amount);
            Client.Send(packet);
        }

        /// <summary>
        /// Sends initial bless amount.
        /// </summary>
        private void SendBlessAmount()
        {
            using var packet = new ImgeneusPacket(PacketType.BLESS_INIT);
            packet.Write((byte)CountryProvider.Country);

            var blessAmount = CountryProvider.Country == CountryType.Light ? Bless.Instance.LightAmount : Bless.Instance.DarkAmount;
            packet.Write(blessAmount);
            packet.Write(Bless.Instance.RemainingTime);

            Client.Send(packet);
        }
    }
}
