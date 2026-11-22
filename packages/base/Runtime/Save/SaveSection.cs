using System;

namespace Mu3Library.Save
{
    /// <summary>
    /// A section whose data is one serializable class or struct. The default serializer,
    /// <see cref="JsonSaveSerializer"/>, follows <see cref="UnityEngine.JsonUtility"/>: public fields
    /// and <see cref="UnityEngine.SerializeField"/> fields of serializable types, Unity types such as
    /// <see cref="UnityEngine.Vector3"/> included, but not dictionaries, properties, or a bare
    /// collection as the data itself.
    /// </summary>
    public abstract class SaveSection<TData> : ISaveSection
    {
        public abstract string Key { get; }
        public virtual int Version => 1;

        Type ISaveSection.DataType => typeof(TData);



        object ISaveSection.Capture() => Capture();

        object ISaveSection.Migrate(object data, int savedVersion)
        {
            TData value = data is TData typed ? typed : default;
            return savedVersion < Version ? Migrate(savedVersion, value) : value;
        }

        void ISaveSection.Restore(object data) => Restore(data is TData typed ? typed : default);

        void ISaveSection.RestoreDefault() => RestoreDefault();

        /// <summary>
        /// Returns the data to save.
        /// </summary>
        protected abstract TData Capture();

        /// <summary>
        /// Takes back saved data, already migrated to <see cref="Version"/>.
        /// </summary>
        protected abstract void Restore(TData data);

        /// <summary>
        /// Brings data written with an older <paramref name="savedVersion"/> up to <see cref="Version"/>.
        /// The data was read into the current <typeparamref name="TData"/>, so a field the old version
        /// lacked holds its default and a field it had that the type dropped is gone; fill or derive
        /// fields here, without touching the game: it runs while the slot is read, before any section
        /// restores. The default returns the data as it is.
        /// </summary>
        protected virtual TData Migrate(int savedVersion, TData data) => data;

        /// <summary>
        /// Resets the section when a slot that loads holds nothing for it. The default does nothing.
        /// </summary>
        protected virtual void RestoreDefault()
        {
        }
    }

    /// <summary>
    /// Builds a section from callbacks, for data that needs no class of its own.
    /// </summary>
    public static class SaveSection
    {
        public static SaveSection<TData> Create<TData>(
            string key,
            Func<TData> capture,
            Action<TData> restore,
            int version = 1,
            Action restoreDefault = null,
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

            return new DelegateSaveSection<TData>(key, version, capture, restore, restoreDefault, migrate);
        }



        private sealed class DelegateSaveSection<TData> : SaveSection<TData>
        {
            private readonly string _key;
            private readonly int _version;
            private readonly Func<TData> _capture;
            private readonly Action<TData> _restore;
            private readonly Action _restoreDefault;
            private readonly Func<int, TData, TData> _migrate;

            public override string Key => _key;
            public override int Version => _version;



            public DelegateSaveSection(
                string key,
                int version,
                Func<TData> capture,
                Action<TData> restore,
                Action restoreDefault,
                Func<int, TData, TData> migrate)
            {
                _key = key;
                _version = version;
                _capture = capture;
                _restore = restore;
                _restoreDefault = restoreDefault;
                _migrate = migrate;
            }

            protected override TData Capture() => _capture();
            protected override void Restore(TData data) => _restore(data);
            protected override TData Migrate(int savedVersion, TData data) =>
                _migrate != null ? _migrate(savedVersion, data) : data;
            protected override void RestoreDefault() => _restoreDefault?.Invoke();
        }
    }
}
