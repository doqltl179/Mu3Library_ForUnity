namespace Mu3Library.Multiplayer.Local.Machine
{
    internal enum MachineLinkRole
    {
        /// <summary>Accepted, before its first frame says what it is for.</summary>
        Unknown,
        /// <summary>A session connection, either way.</summary>
        Session,
        /// <summary>To a lobby's owner, waiting for its answer to a join.</summary>
        Joining,
        /// <summary>To the owner of a lobby this player is a member of.</summary>
        MemberToOwner,
        /// <summary>From a member of a lobby this player owns.</summary>
        OwnerToMember,
    }
}
