namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// Which of a player's avatar images <see cref="IMultiplayerManager.LoadAvatar"/> loads. Steam
    /// keeps each one at a fixed size.
    /// </summary>
    public enum AvatarSize
    {
        /// <summary>32 by 32 pixels on Steam.</summary>
        Small = 0,
        /// <summary>64 by 64 pixels on Steam.</summary>
        Medium = 1,
        /// <summary>184 by 184 pixels on Steam.</summary>
        Large = 2,
    }
}
