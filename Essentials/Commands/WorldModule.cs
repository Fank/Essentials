﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using SpaceEngineers.Game.GUI;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers;
using Torch.Utils;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.API.Session;
using VRage.Game.ModAPI;
using VRage.Network;

namespace Essentials.Commands
{
    public class WorldModule : CommandModule
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        [Command("identity clean", "Remove identities that have not logged on in X days.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void CleanIdentities(int days)
        {
            var count = 0;
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    count++;
                }
            }
            
            RemoveEmptyFactions();
            Context.Respond($"Removed {count} old identities");
        }

        [Command("identity purge", "Remove identities AND the grids they own if they have not logged on in X days.")]
        [Permission(MyPromoteLevel.SpaceMaster)]
        public void PurgeIdentities(int days)
        {
            var count = 0;
            var count2 = 0;
            var grids = MyEntities.GetEntities().OfType<MyCubeGrid>().ToList();
            var idents = MySession.Static.Players.GetAllIdentities().ToList();
            var cutoff = DateTime.Now - TimeSpan.FromDays(days);
            foreach (var identity in idents)
            {
                if (identity.LastLoginTime < cutoff)
                {
                    MySession.Static.Factions.KickPlayerFromFaction(identity.IdentityId);
                    MySession.Static.Players.RemoveIdentity(identity.IdentityId);
                    count++;
                    foreach (var grid in grids)
                    {
                        if (grid.BigOwners.Contains(identity.IdentityId))
                        {
                            grid.Close();
                            count2++;
                        }
                    }
                }
            }
            
            RemoveEmptyFactions();
            Context.Respond($"Removed {count} old identities and {count2} grids owned by them.");
        }

        [Command("faction clean", "Removes factions with fewer than the given number of players.")]
        public void CleanFactions(int memberCount = 1)
        {
            int count = 0;
            foreach(var faction in MySession.Static.Factions.ToList())
            {
                if (faction.Value.Members.Count < memberCount)
                {
                    count++;
                    RemoveFaction(faction.Value);
                }
            }

            Context.Respond($"Removed {count} factions with fewer than {memberCount} members.");
        }

        private void RemoveEmptyFactions()
        {
            foreach (var faction in MySession.Static.Factions.ToList())
            {
                if (faction.Value.Members.Count == 0)
                {
                    RemoveFaction(faction.Value);
                }
            }
        }

        //Equinox told me you can use delegates for ReflectedMethods.
        //He lied.
        //private delegate void FactionStateDelegate(MyFactionCollection instance, MyFactionStateChange action, long fromFactionId, long toFactionId, long playerId, long senderId);

        [ReflectedMethod(Name = "ApplyFactionStateChange", Type = typeof(MyFactionCollection))]
        private static Action<MyFactionCollection, MyFactionStateChange, long, long, long, long> _applyFactionState;

        private static MethodInfo _factionChangeSuccessInfo = typeof(MyFactionCollection).GetMethod("FactionStateChangeSuccess", BindingFlags.NonPublic|BindingFlags.Static);

        //TODO: This should probably be moved into Torch base, but I honestly cannot be bothered
        /// <summary>
        /// Removes a faction from the server and all clients because Keen fucked up their own system.
        /// </summary>
        /// <param name="faction"></param>
        private static void RemoveFaction(MyFaction faction)
        {
            //bypass the check that says the server doesn't have permission to delete factions
            _applyFactionState(MySession.Static.Factions, MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, 0, 0);
            var n = EssentialsPlugin.Instance.Torch.CurrentSession.Managers.GetManager<NetworkManager>();
            //send remove message to clients
            n.RaiseStaticEvent(_factionChangeSuccessInfo, MyFactionStateChange.RemoveFaction, faction.FactionId, faction.FactionId, 0L, 0L);
        }
    }
}
