namespace Mu3Library.Observable
{
    /// <summary>
    /// An Observable whose serialized value Unity can restore without going through its methods,
    /// as an undo, a redo, or a prefab revert does in the editor. Deserialization only marks the
    /// restore, since it can run on a loading thread; the editor delivers it on the main thread.
    /// </summary>
    internal interface IRestorableObservable
    {
        /// <summary>
        /// Notifies the subscribers when Unity deserialized a value into this instance since they
        /// were last notified and the value differs from theirs, and answers whether it did. Call it
        /// on the main thread only.
        /// </summary>
        bool NotifyIfRestored();
    }
}
