using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// What a session save holds, read from its <see cref="SaveSlotInfo"/> without restoring
    /// anything: who hosted it, the session name, and the players it holds sections for. A save
    /// received from another player keeps the host who saved it, so <see cref="HostId"/> tells
    /// where it came from until this player saves it as host.
    /// </summary>
    public sealed class SessionSaveInfo
    {
        public SaveSlotInfo Slot { get; }
        public string SlotId => Slot.SlotId;

        /// <summary>The host who last saved the slot.</summary>
        public PlayerId HostId { get; }

        public string HostName { get; }

        /// <summary>The session name the host last saved the slot with, or null.</summary>
        public string SessionName { get; }

        /// <summary>The players the slot holds at least one player section for, in the order they first appear.</summary>
        public IReadOnlyList<PlayerId> Players { get; }



        private SessionSaveInfo(SaveSlotInfo slot, PlayerId hostId, string hostName, string sessionName, IReadOnlyList<PlayerId> players)
        {
            Slot = slot;
            HostId = hostId;
            HostName = hostName;
            SessionName = sessionName;
            Players = players;
        }

        public bool HasPlayer(PlayerId player)
        {
            foreach (PlayerId candidate in Players)
            {
                if (candidate == player)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads a session save out of <paramref name="slot"/>; false for a slot that
        /// <see cref="SessionSaveManager.Save"/> never wrote, which names no host.
        /// </summary>
        public static bool TryCreate(SaveSlotInfo slot, out SessionSaveInfo info)
        {
            if (slot == null)
            {
                throw new ArgumentNullException(nameof(slot));
            }

            info = null;
            if (!slot.TryGetMetadata(SessionSaveKeys.HostId, out string hostText) ||
                !ulong.TryParse(hostText, NumberStyles.None, CultureInfo.InvariantCulture, out ulong hostValue) ||
                hostValue == 0)
            {
                return false;
            }

            slot.TryGetMetadata(SessionSaveKeys.HostName, out string hostName);
            slot.TryGetMetadata(SessionSaveKeys.SessionName, out string sessionName);

            var players = new List<PlayerId>();
            foreach (SaveSectionInfo section in slot.Sections)
            {
                if (SessionSaveKeys.TryParsePlayerSectionKey(section.Key, out PlayerId player, out _) && !players.Contains(player))
                {
                    players.Add(player);
                }
            }

            info = new SessionSaveInfo(slot, new PlayerId(hostValue), hostName ?? string.Empty, sessionName, players);
            return true;
        }
    }
}
