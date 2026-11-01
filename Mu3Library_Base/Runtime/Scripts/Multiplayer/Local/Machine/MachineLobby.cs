using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// A lobby on a machine-wide network: the original when this player owns it, otherwise the
    /// copy the owner keeps up to date.
    /// </summary>
    internal sealed class MachineLobby
    {
        public LobbyId Id;
        public bool IsOwned;
        public PlayerId OwnerId;
        public int OwnerPort;
        public LobbyVisibility Visibility;
        public int Capacity;
        public bool IsJoinable = true;
        public readonly List<MachineMember> Members = new List<MachineMember>();
        // Keys are found in any case, as Steam finds lobby data keys.
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Owner: the members have not heard the latest change yet.</summary>
        public bool IsDirty;

        /// <summary>Member: the connection to the owner, null while finding the next owner.</summary>
        public MachineLink OwnerLink;
        public MachineLink RejoinLink;
        public double RejoinAt;
        public double RejoinDeadline;

        public MachineMember FindMember(PlayerId id, int port)
        {
            int index = IndexOfMember(id, port);
            return index >= 0 ? Members[index] : null;
        }

        public int IndexOfMember(PlayerId id, int port)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].Id == id && Members[i].Port == port)
                {
                    return i;
                }
            }

            return -1;
        }

        public bool HasMember(PlayerId id)
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i].Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Takes everything but the connections from the owner's latest copy.</summary>
        public void Apply(MachineLobby source)
        {
            OwnerId = source.OwnerId;
            OwnerPort = source.OwnerPort;
            Visibility = source.Visibility;
            Capacity = source.Capacity;
            IsJoinable = source.IsJoinable;

            Members.Clear();
            Members.AddRange(source.Members);

            Data.Clear();
            foreach (KeyValuePair<string, string> pair in source.Data)
            {
                Data[pair.Key] = pair.Value;
            }
        }
    }
}
