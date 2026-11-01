using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// The sending thread of a peer: the main thread hands it what each connection queued, and
    /// it hands each socket only what the socket takes now, so no send waits for a player that
    /// does not read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unity's <see cref="Socket.Send(byte[], int, int, SocketFlags, out SocketError)"/> calls the
    /// system again until the socket took everything, also on a non-blocking socket, and spins
    /// while the socket is full: the class library of Unity 6000.6.7f1 ends the method with a loop
    /// that goes on after <see cref="SocketError.WouldBlock"/>, in the Editor and in Mono and
    /// IL2CPP players on every platform. A send on the main thread to a player whose socket was
    /// full held the frame until that player read, and with both players in one process, for ever.
    /// </para>
    /// <para>
    /// The thread asks a socket whether it can take more (<see cref="Socket.Poll(int, SelectMode)"/>)
    /// before each call, waits a millisecond, or until more comes to send, while none can, and
    /// meanwhile sends on the connections that can. Windows takes the whole of a send on a socket
    /// that is writable, so there the array overload calls the system once and allocates nothing;
    /// elsewhere a writable socket may take only part of it, so the thread sends through the
    /// overload that takes a list of segments, which calls the system once and allocates two
    /// small arrays a call, on this thread.
    /// </para>
    /// <para>
    /// The main thread only moves buffers under a lock, instead of a socket call for each
    /// connection, which takes about 9 µs for 1 KB on Windows loopback. What a connection handed
    /// over and its socket did not take counts toward <see cref="MaxPendingBytes"/>, past which a
    /// send answers false, as a full send buffer on Steam does.
    /// </para>
    /// </remarks>
    internal sealed partial class MachinePeer
    {
        // The sending thread only sends; the default stack would be a megabyte.
        private const int SenderStackBytes = 128 * 1024;

        // How long the sending thread waits before it asks a full socket again, unless more to send comes first.
        private const int FullSocketWaitMilliseconds = 1;

        // A socket that select calls writable takes the whole of a send on Windows (measured with
        // sends of 1 KiB to 1 MiB), so the array overload of Socket.Send calls the system once.
        private static readonly bool TakesWholeSends = Environment.OSVersion.Platform == PlatformID.Win32NT;

        private readonly object _sendGate = new object();

        // Under the gate: the connections the sending thread has something to do for, and how
        // many times the main thread handed it something, so a hand-over between the thread's
        // last look and its wait does not wait.
        private readonly List<MachineLink> _sendList = new List<MachineLink>();
        private int _handOvers = 0;
        private bool _isSenderStopping = false;

        // Sending thread only.
        private readonly List<MachineLink> _sendBatch = new List<MachineLink>();
        private readonly ArraySegment<byte>[] _sendSegments = new ArraySegment<byte>[1];

        // Main thread only.
        private Thread _sender = null;
        private EventHandler _onSenderDomainUnload = null;

        private long _socketSends = 0;
        private volatile bool _sendsSegments = false;

        /// <summary>How many times this peer has handed data to a socket; tests count it.</summary>
        public long SocketSends => Interlocked.Read(ref _socketSends);

        /// <summary>
        /// Test switch: sends through the overload of <see cref="Socket.Send(IList{ArraySegment{byte}}, SocketFlags, out SocketError)"/>
        /// that takes a list of segments, as on systems other than Windows.
        /// </summary>
        public bool SendsSegments
        {
            get => _sendsSegments;
            set => _sendsSegments = value;
        }

        /// <summary>True while the sending thread holds something to send or to close; tests wait for it.</summary>
        public bool HasSendsInFlight
        {
            get
            {
                lock (_sendGate)
                {
                    return _sendList.Count > 0;
                }
            }
        }

        /// <summary>
        /// Hands what an open connection queued to the sending thread; a failure the thread
        /// meets waits for the next update to be reported.
        /// </summary>
        private void Flush(MachineLink link)
        {
            if (link.PendingCount == 0 || link.HasFailed)
            {
                return;
            }

            lock (_sendGate)
            {
                HandOver(link);
                Monitor.Pulse(_sendGate);
            }

            StartSender();
        }

        /// <summary>
        /// Hands what the session queued during the update to the sending thread, under one lock
        /// for every connection that has something to send. A session connection that an outage
        /// cuts off keeps it.
        /// </summary>
        private void FlushQueued()
        {
            if (_isDisposed)
            {
                return;
            }

            bool isCutOff = IsCutOff;
            bool isLocked = false;
            try
            {
                for (int i = 0; i < _links.Count; i++)
                {
                    MachineLink link = _links[i];
                    if (link.PendingCount > 0 && link.State == MachineLinkState.Open && !link.HasFailed
                        && !(isCutOff && link.Role == MachineLinkRole.Session))
                    {
                        if (!isLocked)
                        {
                            Monitor.Enter(_sendGate, ref isLocked);
                        }

                        HandOver(link);
                    }
                }
            }
            finally
            {
                if (isLocked)
                {
                    Monitor.Pulse(_sendGate);
                    Monitor.Exit(_sendGate);
                }
            }

            if (isLocked)
            {
                StartSender();
            }
        }

        /// <summary>Under the gate: moves what the connection queued to its outbox and lists it for the sending thread.</summary>
        private void HandOver(MachineLink link)
        {
            MoveToOutbox(link);
            _handOvers++;
            if (!link.IsSendListed)
            {
                link.IsSendListed = true;
                _sendList.Add(link);
            }
        }

        /// <summary>Under the gate: appends what the connection queued to its outbox, or trades the buffers when the outbox is empty.</summary>
        private static void MoveToOutbox(MachineLink link)
        {
            int count = link.PendingCount;
            if (count == 0)
            {
                return;
            }

            if (link.OutboxCount == 0)
            {
                byte[] outbox = link.Outbox;
                link.Outbox = link.Pending;
                link.Pending = outbox;
            }
            else
            {
                int required = link.OutboxCount + count;
                if (required > link.Outbox.Length)
                {
                    Array.Resize(ref link.Outbox, Math.Max(required, link.Outbox.Length * 2));
                }

                Buffer.BlockCopy(link.Pending, 0, link.Outbox, link.OutboxCount, count);
            }

            link.OutboxCount += count;
            link.PendingCount = 0;
            Volatile.Write(ref link.HandedBytes, link.HandedBytes + count);
        }

        /// <summary>Under the gate: forgets what the connection handed over and its socket did not take.</summary>
        private static void ClearHanded(MachineLink link)
        {
            link.OutboxCount = 0;
            link.SendingStart = 0;
            link.SendingCount = 0;
            Volatile.Write(ref link.HandedBytes, 0);
        }

        /// <summary>
        /// A connection that closes from this side hands what its socket has not taken to a
        /// lingering socket: from here, or from the sending thread once its call on the
        /// connection returns.
        /// </summary>
        private void LingerSends(MachineLink link)
        {
            Socket socket;
            byte[] first;
            int firstOffset;
            int firstCount;
            byte[] second;
            int secondCount;
            lock (_sendGate)
            {
                // The sending thread reads the socket under the gate while the link is open.
                socket = link.Socket;
                link.Socket = null;
                MoveToOutbox(link);
                if (link.IsInSocketCall)
                {
                    // The thread holds the socket of its call.
                    link.SendEnd = MachineSendEnd.Linger;
                    return;
                }

                first = link.Sending;
                firstOffset = link.SendingStart;
                firstCount = link.SendingCount;
                second = link.Outbox;
                secondCount = link.OutboxCount;
                ClearHanded(link);
                link.SendEnd = MachineSendEnd.Drop;
            }

            LingeringSockets.Close(socket, first, firstOffset, firstCount, second, secondCount);
        }

        /// <summary>A connection that ends without lingering: the sending thread drops what it holds of it.</summary>
        private void DropSends(MachineLink link)
        {
            link.PendingCount = 0;
            lock (_sendGate)
            {
                ClearHanded(link);
                link.SendEnd = MachineSendEnd.Drop;
            }
        }

        private void StartSender()
        {
            if (_sender != null || _isDisposed)
            {
                return;
            }

            // A backend dropped without Dispose would leave the thread waiting until the process
            // ends, and Unity reloads scripts without ending the process.
            if (_onSenderDomainUnload == null)
            {
                _onSenderDomainUnload = (sender, args) => AskSenderToStop(dropsUnsent: true);
            }

            AppDomain.CurrentDomain.DomainUnload += _onSenderDomainUnload;
            _sender = new Thread(RunSender, SenderStackBytes)
            {
                IsBackground = true,
                Name = "Mu3Library.Multiplayer local sends",
            };
            _sender.Start();
        }

        /// <summary>
        /// Lets the sending thread end once it let go of every connection it holds, without
        /// waiting for it: a socket call it is in returns at once.
        /// </summary>
        private void StopSender()
        {
            if (_sender == null)
            {
                return;
            }

            AppDomain.CurrentDomain.DomainUnload -= _onSenderDomainUnload;
            AskSenderToStop(dropsUnsent: false);
            _sender = null;
        }

        private void AskSenderToStop(bool dropsUnsent)
        {
            lock (_sendGate)
            {
                _isSenderStopping = true;
                if (dropsUnsent)
                {
                    for (int i = 0; i < _sendList.Count; i++)
                    {
                        _sendList[i].SendEnd = MachineSendEnd.Drop;
                    }
                }

                Monitor.Pulse(_sendGate);
            }
        }

        /// <summary>The sending thread: sends on every connection that has something to send, until it stops.</summary>
        private void RunSender()
        {
            try
            {
                bool isWaiting = false;
                int handOvers = 0;
                while (true)
                {
                    lock (_sendGate)
                    {
                        if (isWaiting && handOvers == _handOvers && !_isSenderStopping)
                        {
                            Monitor.Wait(_sendGate, FullSocketWaitMilliseconds);
                        }

                        while (_sendList.Count == 0)
                        {
                            if (_isSenderStopping)
                            {
                                return;
                            }

                            Monitor.Wait(_sendGate);
                        }

                        // Every connection closed before the stop, so what is still open has nobody to send to.
                        for (int i = 0; i < _sendList.Count; i++)
                        {
                            MachineLink link = _sendList[i];
                            if (_isSenderStopping && link.SendEnd == MachineSendEnd.None)
                            {
                                link.SendEnd = MachineSendEnd.Drop;
                            }

                            _sendBatch.Add(link);
                        }

                        handOvers = _handOvers;
                    }

                    isWaiting = false;
                    for (int i = 0; i < _sendBatch.Count; i++)
                    {
                        isWaiting |= SendSome(_sendBatch[i]);
                    }

                    _sendBatch.Clear();
                }
            }
            catch (Exception e) when (!(e is ThreadAbortException))
            {
                // Logged rather than thrown, since on this thread it would end the process; the
                // connections it held are reported lost.
                Debug.LogException(e);
                lock (_sendGate)
                {
                    for (int i = 0; i < _sendList.Count; i++)
                    {
                        _sendList[i].HasFailed = true;
                        _sendList[i].IsSendListed = false;
                    }

                    _sendList.Clear();
                }
            }
        }

        /// <summary>
        /// The sending thread: hands the connection's socket what it takes now, or lets the
        /// connection go. True when the socket is full.
        /// </summary>
        private bool SendSome(MachineLink link)
        {
            Socket socket;
            byte[] buffer;
            int offset;
            int count;
            lock (_sendGate)
            {
                if (link.SendEnd != MachineSendEnd.None || link.SendingCount + link.OutboxCount == 0)
                {
                    Unlist(link);
                    return false;
                }

                if (link.SendingCount == 0)
                {
                    byte[] sending = link.Sending;
                    link.Sending = link.Outbox;
                    link.Outbox = sending;
                    link.SendingStart = 0;
                    link.SendingCount = link.OutboxCount;
                    link.OutboxCount = 0;
                }

                socket = link.Socket;
                buffer = link.Sending;
                offset = link.SendingStart;
                count = link.SendingCount;
                link.IsInSocketCall = true;
            }

            int sent = 0;
            bool isFull = false;
            bool hasFailed = false;
            try
            {
                if (socket.Poll(0, SelectMode.SelectWrite))
                {
                    Interlocked.Increment(ref _socketSends);
                    sent = SendOnce(socket, buffer, offset, count, out SocketError error);
                    if (error == SocketError.WouldBlock)
                    {
                        sent = 0;
                        isFull = true;
                    }
                    else if (error != SocketError.Success)
                    {
                        sent = 0;
                        hasFailed = true;
                    }
                }
                else
                {
                    isFull = true;
                }
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException)
            {
                // Closed under the call, or broken.
                hasFailed = true;
            }

            byte[] second;
            int secondCount;
            lock (_sendGate)
            {
                link.IsInSocketCall = false;
                if (link.SendEnd == MachineSendEnd.None)
                {
                    if (hasFailed)
                    {
                        // Reported by the next update.
                        link.HasFailed = true;
                        ClearHanded(link);
                        Unlist(link);
                        return false;
                    }

                    link.SendingStart += sent;
                    link.SendingCount -= sent;
                    Volatile.Write(ref link.HandedBytes, link.HandedBytes - sent);
                    if (link.SendingCount + link.OutboxCount == 0)
                    {
                        Unlist(link);
                    }

                    return isFull;
                }

                if (link.SendEnd == MachineSendEnd.Drop || hasFailed)
                {
                    ClearHanded(link);
                    link.SendEnd = MachineSendEnd.Drop;
                    Unlist(link);
                    if (hasFailed)
                    {
                        Close(socket);
                    }

                    return false;
                }

                // Closed during the call: the lingering socket sends the rest.
                buffer = link.Sending;
                offset = link.SendingStart + sent;
                count = link.SendingCount - sent;
                second = link.Outbox;
                secondCount = link.OutboxCount;
                ClearHanded(link);
                link.SendEnd = MachineSendEnd.Drop;
                Unlist(link);
            }

            LingeringSockets.Close(socket, buffer, offset, count, second, secondCount);
            return false;
        }

        /// <summary>One system call: see the remarks.</summary>
        private int SendOnce(Socket socket, byte[] buffer, int offset, int count, out SocketError error)
        {
            if (TakesWholeSends && !_sendsSegments)
            {
                return socket.Send(buffer, offset, count, SocketFlags.None, out error);
            }

            _sendSegments[0] = new ArraySegment<byte>(buffer, offset, count);
            return socket.Send(_sendSegments, SocketFlags.None, out error);
        }

        /// <summary>Under the gate: takes the connection off the sending thread's list.</summary>
        private void Unlist(MachineLink link)
        {
            if (link.IsSendListed)
            {
                link.IsSendListed = false;
                _sendList.Remove(link);
            }
        }
    }
}
