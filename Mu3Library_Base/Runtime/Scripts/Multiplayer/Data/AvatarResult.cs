using UnityEngine;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>The answer to <see cref="IMultiplayerManager.LoadAvatar"/>.</summary>
    public readonly struct AvatarResult
    {
        public MultiplayerReason Reason { get; }
        public bool IsSuccess => Reason == MultiplayerReason.None;
        public PlayerId Player { get; }
        public AvatarSize Size { get; }

        /// <summary>
        /// The avatar, null on failure. The manager owns it: never destroy or change it, and ask
        /// again once it is gone (see <see cref="IMultiplayerManager.LoadAvatar"/>).
        /// </summary>
        public Texture2D Texture { get; }

        public AvatarResult(MultiplayerReason reason, PlayerId player, AvatarSize size, Texture2D texture)
        {
            Reason = reason;
            Player = player;
            Size = size;
            Texture = texture;
        }
    }
}
