using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    internal sealed class LocalConnection
    {
        public ConnectionId Id;
        public LocalBackend Owner;
        public PlayerId RemotePlayer;
        public LocalConnection Remote;
        public bool IsOpen;
    }
}
