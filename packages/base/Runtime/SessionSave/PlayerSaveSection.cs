using System;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// A player section whose data is one serializable class or struct, as for
    /// <see cref="Mu3Library.Save.SaveSection{TData}"/>.
    /// </summary>
    public abstract class PlayerSaveSection<TData> : IPlayerSaveSection
    {
        public abstract string Key { get; }
        public virtual int Version => 1;

        Type IPlayerSaveSection.DataType => typeof(TData);



        object IPlayerSaveSection.Capture(PlayerId player) => Capture(player);

        object IPlayerSaveSection.Migrate(object data, int savedVersion)
        {
            TData value = data is TData typed ? typed : default;
            return savedVersion < Version ? Migrate(savedVersion, value) : value;
        }

        void IPlayerSaveSection.Restore(PlayerId player, object data) => Restore(player, data is TData typed ? typed : default);

        void IPlayerSaveSection.RestoreDefault(PlayerId player) => RestoreDefault(player);

        /// <summary>Returns <paramref name="player"/>'s data to save.</summary>
        protected abstract TData Capture(PlayerId player);

        /// <summary>Takes back <paramref name="player"/>'s saved data, already migrated to <see cref="Version"/>.</summary>
        protected abstract void Restore(PlayerId player, TData data);

        /// <summary>
        /// Brings data written with an older <paramref name="savedVersion"/> up to <see cref="Version"/>,
        /// without touching the game; see <see cref="Mu3Library.Save.SaveSection{TData}"/>. The
        /// default returns the data as it is.
        /// </summary>
        protected virtual TData Migrate(int savedVersion, TData data) => data;

        /// <summary>
        /// Gives <paramref name="player"/> the game's defaults when a slot that loads holds nothing
        /// for them. The default does nothing.
        /// </summary>
        protected virtual void RestoreDefault(PlayerId player)
        {
        }
    }

    /// <summary>
    /// Builds a player section from callbacks, for data that needs no class of its own.
    /// </summary>
    public static class PlayerSaveSection
    {
        public static PlayerSaveSection<TData> Create<TData>(
            string key,
            Func<PlayerId, TData> capture,
            Action<PlayerId, TData> restore,
            int version = 1,
            Action<PlayerId> restoreDefault = null,
            Func<int, TData, TData> migrate = null)
        {
            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            if (restore == null)
            {
                throw new ArgumentNullException(nameof(restore));
            }

            return new DelegatePlayerSaveSection<TData>(key, version, capture, restore, restoreDefault, migrate);
        }



        private sealed class DelegatePlayerSaveSection<TData> : PlayerSaveSection<TData>
        {
            private readonly string _key;
            private readonly int _version;
            private readonly Func<PlayerId, TData> _capture;
            private readonly Action<PlayerId, TData> _restore;
            private readonly Action<PlayerId> _restoreDefault;
            private readonly Func<int, TData, TData> _migrate;

            public override string Key => _key;
            public override int Version => _version;



            public DelegatePlayerSaveSection(
                string key,
                int version,
                Func<PlayerId, TData> capture,
                Action<PlayerId, TData> restore,
                Action<PlayerId> restoreDefault,
                Func<int, TData, TData> migrate)
            {
                _key = key;
                _version = version;
                _capture = capture;
                _restore = restore;
                _restoreDefault = restoreDefault;
                _migrate = migrate;
            }

            protected override TData Capture(PlayerId player) => _capture(player);
            protected override void Restore(PlayerId player, TData data) => _restore(player, data);
            protected override TData Migrate(int savedVersion, TData data) =>
                _migrate != null ? _migrate(savedVersion, data) : data;
            protected override void RestoreDefault(PlayerId player) => _restoreDefault?.Invoke(player);
        }
    }
}
