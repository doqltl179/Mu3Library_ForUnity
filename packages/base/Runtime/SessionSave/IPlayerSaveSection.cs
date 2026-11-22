using System;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// A part of a session save that every player has their own of, such as an inventory or a
    /// character. <see cref="SessionSaveManager"/> saves it once for each player of the session
    /// under that player's <see cref="PlayerId"/>, so the data follows the account when another
    /// player hosts the session later. The host's game holds every player's data and hands it out:
    /// the manager calls these members on the host only. Derive from
    /// <see cref="PlayerSaveSection{TData}"/> rather than implementing this interface directly.
    /// </summary>
    public interface IPlayerSaveSection
    {
        /// <summary>
        /// The section's name: 1 to <see cref="SessionSaveKeys.MaxPlayerSectionKeyLength"/>
        /// characters of letters, digits, <c>_</c>, <c>-</c>, and <c>.</c>. It must not change
        /// between game versions.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The version of the data this section writes, 1 or more; see
        /// <see cref="Mu3Library.Save.ISaveSection.Version"/>.
        /// </summary>
        public int Version { get; }

        /// <summary>The type <see cref="Capture"/> returns and <see cref="Restore"/> takes.</summary>
        public Type DataType { get; }

        /// <summary>Returns <paramref name="player"/>'s data to save, of <see cref="DataType"/>.</summary>
        public object Capture(PlayerId player);

        /// <summary>
        /// Brings data written with <paramref name="savedVersion"/> up to <see cref="Version"/>;
        /// see <see cref="Mu3Library.Save.ISaveSection.Migrate"/>.
        /// </summary>
        public object Migrate(object data, int savedVersion);

        /// <summary>Takes back <paramref name="player"/>'s saved data, already migrated.</summary>
        public void Restore(PlayerId player, object data);

        /// <summary>
        /// Called when a slot that loads holds nothing for <paramref name="player"/>, such as a
        /// player who joins a saved session for the first time, so they start from the game's defaults.
        /// </summary>
        public void RestoreDefault(PlayerId player);
    }
}
