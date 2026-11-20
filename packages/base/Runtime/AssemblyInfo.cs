using System.Runtime.CompilerServices;

// The editor delivers the notifications of Observable values that an undo, a redo, or a prefab
// revert restored, through IRestorableObservable, which stays internal so the Observables keep
// their public surface.
[assembly: InternalsVisibleTo("Mu3Library.Editor")]
