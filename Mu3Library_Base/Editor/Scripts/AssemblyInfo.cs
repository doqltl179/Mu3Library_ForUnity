using System.Runtime.CompilerServices;

// The edit-mode tests drive the Observable drawers' target lookup, which stays internal so the
// drawers keep their public surface.
[assembly: InternalsVisibleTo("Mu3Library.Tests.Editor")]
