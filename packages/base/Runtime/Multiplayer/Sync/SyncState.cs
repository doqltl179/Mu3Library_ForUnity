namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// A state that marks itself changed: call <see cref="SetDirty"/> whenever a value the state
    /// sends changes. By default a change sends the whole state; override <see cref="WriteDelta"/>
    /// and <see cref="ReadDelta"/> to send only what changed, as a state with many values does.
    /// </summary>
    /// <example>
    /// <code>
    /// public sealed class HealthState : SyncState
    /// {
    ///     private int _health;
    ///
    ///     public HealthState(uint id) : base(id) { }
    ///
    ///     public int Health
    ///     {
    ///         get => _health;
    ///         set
    ///         {
    ///             if (_health != value)
    ///             {
    ///                 _health = value;
    ///                 SetDirty();
    ///             }
    ///         }
    ///     }
    ///
    ///     public override void WriteFull(MessageWriter writer) => writer.WriteInt(_health);
    ///     public override void ReadFull(ref MessageReader reader, in SyncReadContext context) => _health = reader.ReadInt();
    /// }
    /// </code>
    /// </example>
    public abstract class SyncState : ISyncState
    {
        private bool _isDirty = false;

        public uint Id { get; }

        public virtual bool IsDirty => _isDirty;



        protected SyncState(uint id)
        {
            Id = id;
        }

        /// <summary>Marks the state changed, so the manager sends it at its next tick.</summary>
        protected void SetDirty()
        {
            _isDirty = true;
        }

        public virtual void ClearDirty()
        {
            _isDirty = false;
        }

        public abstract void WriteFull(MessageWriter writer);

        public abstract void ReadFull(ref MessageReader reader, in SyncReadContext context);

        /// <summary>Writes the whole state unless overridden.</summary>
        public virtual void WriteDelta(MessageWriter writer) => WriteFull(writer);

        /// <summary>Reads the whole state unless overridden, as the default <see cref="WriteDelta"/> wrote it.</summary>
        public virtual void ReadDelta(ref MessageReader reader, in SyncReadContext context) => ReadFull(ref reader, context);
    }
}
