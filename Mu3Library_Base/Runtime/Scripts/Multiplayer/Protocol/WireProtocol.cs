using System.Globalization;

namespace Mu3Library.Multiplayer.Protocol
{
    /// <summary>
    /// The version of the packets this library exchanges, apart from the game's own protocol
    /// version. Raise it whenever a packet layout changes, so builds with different library
    /// versions refuse each other instead of misreading packets. A hello always starts with the
    /// kind byte and this version, in every version, so any host can read and refuse it.
    /// </summary>
    internal static class WireProtocol
    {
        public const int Version = 3;
        public static readonly string VersionText = Version.ToString(CultureInfo.InvariantCulture);
    }
}
