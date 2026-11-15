using System.Runtime.CompilerServices;

// The edit-mode tests read the catalog handle AddressablesManager last received and stand in for
// its initialization, which stay internal so the manager never hands a handle to its callers.
[assembly: InternalsVisibleTo("Mu3Library.Addressables.Tests.Editor")]
