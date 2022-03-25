﻿using Imgeneus.Database;
using Imgeneus.Login.Packets;
using Imgeneus.Network.Packets;
using Imgeneus.Network.Packets.Login;
using Sylver.HandlerInvoker.Attributes;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Imgeneus.Login.Handlers
{
    [Handler]
    public class AuthenticationHandler
    {
        private readonly ILoginServer _server;
        private readonly ILoginPacketFactory _loginPacketFactory;
        private readonly IDatabase _database;

        public AuthenticationHandler(ILoginServer server, ILoginPacketFactory loginPacketFactory, IDatabase database)
        {
            _server = server;
            _loginPacketFactory = loginPacketFactory;
            _database = database;
        }

        /// <summary>
        /// Handles the Authentication packet.
        /// </summary>
        [HandlerAction(PacketType.LOGIN_REQUEST)]
        public async Task Handle(LoginClient sender, AuthenticationPacket packet)
        {
            await HandleAuthentication(sender, packet.Username, packet.Password);
        }

        /// <summary>
        /// Handles the Oauth packet.
        /// </summary>
        [HandlerAction(PacketType.OAUTH_LOGIN_REQUEST)]
        public async Task HandleOauth(LoginClient sender, OAuthAuthenticationPacket packet)
        {
            // TODO(OAuth): Should we actually implement OAuth? Perhaps a custom updater distribution is desired.
            // For now, let's just parse the key as a username/password combination.
            var parts = packet.key.Split(":");
            var username = parts.FirstOrDefault();
            var password = parts.LastOrDefault();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                /*
                 * The client doesn't handle unsuccessful login responses after attempting to login with an OAuth key,
                 * as it expects the key to always be valid (having been authenticated in a prior step via the updater).
                 */
                _server.DisconnectUser(sender.Id);
                return;
            }

            await HandleAuthentication(sender, username, password);
        }

        private async Task HandleAuthentication(LoginClient sender, string username, string password)
        {
            var result = Authentication(username, password);
            if (result != AuthenticationResult.SUCCESS)
            {
                _loginPacketFactory.AuthenticationFailed(sender, result);
                return;
            }

            var dbUser = _database.Users.First(x => x.Username == username);

            if (_server.IsClientConnected(dbUser.Id))
            {
                _server.DisconnectUser(sender.Id);
                return;
            }

            dbUser.LastConnectionTime = DateTime.UtcNow;
            _database.Users.Update(dbUser);
            await _database.SaveChangesAsync();

            sender.SetClientUserID(dbUser.Id);

            _loginPacketFactory.AuthenticationSuccess(sender, result, dbUser);
        }

        private AuthenticationResult Authentication(string username, string password)
        {
            var dbUser = _database.Users.FirstOrDefault(x => x.Username == username);

            if (dbUser == null)
            {
                return AuthenticationResult.ACCOUNT_DONT_EXIST;
            }

            if (dbUser.IsDeleted)
            {
                return AuthenticationResult.ACCOUNT_IN_DELETE_PROCESS_1;
            }

            if (!dbUser.Password.Equals(password))
            {
                return AuthenticationResult.INVALID_PASSWORD;
            }

            return (AuthenticationResult)dbUser.Status;
        }
    }
}