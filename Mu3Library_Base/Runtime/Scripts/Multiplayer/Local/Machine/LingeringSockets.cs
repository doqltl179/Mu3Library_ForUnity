using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// Finishes closing connections in the background. A socket closed while data it never read
    /// waits on it resets the connection, and the other side then loses what it had not read yet,
    /// such as the close frame that names the reason. So a closed connection sends what is left,
    /// stops sending, and reads until the other side closes too, or until
    /// <see cref="LingerMilliseconds"/> pass, before the socket closes.
    /// </summary>
    internal static class LingeringSockets
    {
        private const int LingerMilliseconds = 5000;

        private static readonly object Gate = new object();
        private static readonly Dictionary<Socket, Timer> Lingering = new Dictionary<Socket, Timer>();

        // What arrives after the close is read into this and thrown away, by every socket at once.
        private static readonly byte[] Discard = new byte[4096];

        private static bool _isHooked = false;

        /// <summary>Takes the socket over, with the bytes still to send on it.</summary>
        public static void Close(Socket socket, byte[] pending, int offset, int count)
        {
            if (socket == null)
            {
                return;
            }

            byte[] rest = null;
            if (count > 0)
            {
                rest = new byte[count];
                Buffer.BlockCopy(pending, offset, rest, 0, count);
            }

            lock (Gate)
            {
                if (!_isHooked)
                {
                    // Unity reloads scripts without ending the process; nothing must outlive that.
                    AppDomain.CurrentDomain.DomainUnload += (sender, args) => CloseAll();
                    _isHooked = true;
                }

                Lingering[socket] = new Timer(state => Finish((Socket)state), socket, LingerMilliseconds, Timeout.Infinite);
            }

            try
            {
                socket.Blocking = true;
                if (rest != null)
                {
                    socket.BeginSend(rest, 0, rest.Length, SocketFlags.None, OnSent, socket);
                }
                else
                {
                    Drain(socket);
                }
            }
            catch (Exception e) when (IsSocketFailure(e))
            {
                Finish(socket);
            }
        }

        private static void OnSent(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;
            try
            {
                socket.EndSend(result);
                Drain(socket);
            }
            catch (Exception e) when (IsSocketFailure(e))
            {
                Finish(socket);
            }
        }

        private static void Drain(Socket socket)
        {
            socket.Shutdown(SocketShutdown.Send);
            socket.BeginReceive(Discard, 0, Discard.Length, SocketFlags.None, OnReceived, socket);
        }

        private static void OnReceived(IAsyncResult result)
        {
            Socket socket = (Socket)result.AsyncState;
            try
            {
                if (socket.EndReceive(result) > 0)
                {
                    socket.BeginReceive(Discard, 0, Discard.Length, SocketFlags.None, OnReceived, socket);
                    return;
                }
            }
            catch (Exception e) when (IsSocketFailure(e))
            {
                // Closed below either way.
            }

            Finish(socket);
        }

        private static void Finish(Socket socket)
        {
            Timer timer;
            lock (Gate)
            {
                if (!Lingering.TryGetValue(socket, out timer))
                {
                    return;
                }

                Lingering.Remove(socket);
            }

            timer.Dispose();
            try
            {
                socket.Close();
            }
            catch (Exception e) when (IsSocketFailure(e))
            {
                // Already closed.
            }
        }

        private static void CloseAll()
        {
            List<Socket> sockets;
            lock (Gate)
            {
                sockets = new List<Socket>(Lingering.Keys);
            }

            for (int i = 0; i < sockets.Count; i++)
            {
                Finish(sockets[i]);
            }
        }

        private static bool IsSocketFailure(Exception e)
        {
            return e is SocketException || e is ObjectDisposedException || e is InvalidOperationException;
        }
    }
}
