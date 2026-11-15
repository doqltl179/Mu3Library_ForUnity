using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Protocol
{
    internal static class ReasonCodes
    {
        // Steam reserves 1000-1999 for an application's normal close reasons; the local backend
        // passes the same codes so both report a close the same way.
        private const int Base = 1000;
        private const int End = 2000;

        public static int ToWire(MultiplayerReason reason) => Base + (int)reason;

        public static MultiplayerReason FromWire(int code)
        {
            if (code <= Base || code >= End)
            {
                return MultiplayerReason.None;
            }

            MultiplayerReason reason = (MultiplayerReason)(code - Base);
            return System.Enum.IsDefined(typeof(MultiplayerReason), reason) ? reason : MultiplayerReason.None;
        }
    }
}
