using System;

namespace Mu3Library.Save
{
    /// <summary>
    /// One system's part of a save: a key, the version of its data, and the data it hands to
    /// <see cref="ISaveManager"/> and takes back. A game splits what it saves into sections, one per
    /// system or stage, and saves and loads them at points it chooses, such as a checkpoint, rather
    /// than every detail of the moment. Derive from <see cref="SaveSection{TData}"/> rather than
    /// implementing this interface directly.
    /// </summary>
    public interface ISaveSection
    {
        /// <summary>
        /// The section's name within a slot: 1 to <see cref="SaveLimits.MaxKeyLength"/> characters of
        /// letters, digits, <c>_</c>, <c>-</c>, and <c>.</c>. It must not change between game versions.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The version of the data this section writes, 1 or more. Raise it when the data's shape
        /// changes; a slot written with an older version passes through <see cref="Migrate"/> when it
        /// loads, and one written with a newer version is refused with
        /// <see cref="SaveFailureReason.VersionTooNew"/>.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// The type <see cref="Capture"/> returns and <see cref="Restore"/> takes, which the serializer
        /// writes and reads.
        /// </summary>
        public Type DataType { get; }

        /// <summary>
        /// Returns the data to save, of <see cref="DataType"/>.
        /// </summary>
        public object Capture();

        /// <summary>
        /// Brings data a slot held, written with <paramref name="savedVersion"/>, up to
        /// <see cref="Version"/>, without touching the game. The manager calls it while it reads the
        /// slot, before any section restores, so a migration that throws restores nothing. It is
        /// called for every section the slot holds, also when <paramref name="savedVersion"/> equals
        /// <see cref="Version"/>, when it returns the data as it is; <paramref name="savedVersion"/> is
        /// never above <see cref="Version"/>.
        /// </summary>
        public object Migrate(object data, int savedVersion);

        /// <summary>
        /// Takes back data a slot held, already migrated to <see cref="Version"/>.
        /// </summary>
        public void Restore(object data);

        /// <summary>
        /// Called when a whole slot loads and holds nothing for this section, such as a slot saved
        /// before the game added it, so the section starts from its defaults.
        /// </summary>
        public void RestoreDefault();
    }
}
