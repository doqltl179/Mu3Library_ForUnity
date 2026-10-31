using System;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer.Local.Machine
{
    internal sealed partial class MachinePeer
    {
        public bool StartListening()
        {
            if (_isDisposed)
            {
                return false;
            }

            _isListening = true;
            return true;
        }

        public void StopListening()
        {
            _isListening = false;
        }

        public ConnectionId Connect(PlayerId host)
        {
            if (_isDisposed || !host.IsValid)
            {
                return ConnectionId.None;
            }

            ConnectionId id = new ConnectionId(_nextConnectionId++);
            int port = FindPort(host);
            MachineLink link;
            if (port > 0)
            {
                link = StartLink(MachineLinkRole.Session, port);
                _writer.Clear();
                _writer.WriteInt(MachineProtocol.Magic);
                _writer.WriteInt(MachineProtocol.Version);
                _writer.WriteULong(LocalPlayerId.Value);
                _writer.WriteString(PlayerName);
                _writer.WriteULong(host.Value);
                QueueFrame(link, MachineFrameKind.SessionHello, _writer.Written, force: true);
            }
            else
            {
                // No lobby this player is in lists that player, so there is no port to reach.
                link = new MachineLink { Role = MachineLinkRole.Session, State = MachineLinkState.Dead };
                Defer(() =>
                {
                    if (_sessions.Remove(id))
                    {
                        _listener?.OnDisconnected(id, host, BackendDisconnectKind.ConnectFailed, 0);
                    }
                });
            }

            link.Connection = id;
            link.Remote = host;
            _sessions[id] = link;
            return id;
        }

        public bool Send(ConnectionId connection, ReadOnlySpan<byte> data, DeliveryMode mode)
        {
            if (_isDisposed || !_sessions.TryGetValue(connection, out MachineLink link) || data.Length > MaxPacketBytes)
            {
                return false;
            }

            // The connection never opened or just broke; the close is on its way, and the data goes nowhere.
            if (link.State == MachineLinkState.Dead || link.HasFailed)
            {
                return true;
            }

            if (FailingSends > 0)
            {
                FailingSends--;
                return false;
            }

            // What the simulated network holds back counts as unread, as Steam's send buffer does.
            if (link.PendingCount + link.DelayedBytes + MachineProtocol.HeaderBytes + 1 + data.Length > MaxPendingBytes)
            {
                return false;
            }

            bool isReliable = mode == DeliveryMode.Reliable;
            link.Meter?.AddSent(data.Length);
            if (Simulate(link, isReliable, isProbe: false, out double delay))
            {
                Transmit(link, MachineFrameKind.Data, data, isReliable, delay);
            }

            return true;
        }

        public bool TryGetConnectionQuality(ConnectionId connection, out ConnectionQuality quality)
        {
            if (!_isDisposed && _sessions.TryGetValue(connection, out MachineLink link)
                && link.State == MachineLinkState.Open && !link.HasFailed && link.IsReported)
            {
                quality = link.Meter.Quality;
                return true;
            }

            quality = default;
            return false;
        }

        #region Simulation
        /// <summary>
        /// Decides what the simulated network does to the next frame of a session connection:
        /// false when it loses an unreliable one, otherwise the delay before the frame reaches the
        /// socket. A reliable frame that the loss takes goes out one round trip later, as its
        /// resend would.
        /// </summary>
        private bool Simulate(MachineLink link, bool isReliable, bool isProbe, out double delay)
        {
            NetworkSimulation simulation = _network.Simulation;
            delay = 0.0;
            link.PacketsOut++;
            if (!simulation.IsActive)
            {
                return true;
            }

            if (isReliable)
            {
                int resends = simulation.ReliableResends();
                link.PacketsOut += resends;
                link.PacketsLostOut += resends;
                delay = simulation.PacketDelay() + resends * simulation.ResendSeconds;
                return true;
            }

            if (isProbe ? simulation.DropsProbe() : simulation.DropsPacket())
            {
                link.PacketsLostOut++;
                return false;
            }

            delay = isProbe ? simulation.ProbeDelay() : simulation.PacketDelay();
            return true;
        }

        /// <summary>
        /// Sends a frame after its simulated delay. Reliable frames keep their order, so one that
        /// a resend holds back holds back the reliable ones after it.
        /// </summary>
        private void Transmit(MachineLink link, MachineFrameKind kind, ReadOnlySpan<byte> payload, bool isReliable, double delay)
        {
            double now = Now;
            double due = now + delay;
            if (isReliable)
            {
                due = Math.Max(due, link.LastOrderedDue);
                link.LastOrderedDue = due;
            }

            // Without a delay, and with nothing held back that it must not overtake, it goes out at once.
            if (due <= now && (link.Delayed == null || link.Delayed.Count == 0))
            {
                QueueFrame(link, kind, payload, force: true);
                if (link.State == MachineLinkState.Open)
                {
                    Flush(link);
                }

                return;
            }

            byte[] buffer = DelayedFrames.Rent(Math.Max(1, payload.Length));
            payload.CopyTo(buffer);
            link.Delayed ??= new DelayQueue<MachineDelayedFrame>();
            link.Delayed.Push(due, new MachineDelayedFrame { Kind = kind, Buffer = buffer, Length = payload.Length });
            link.DelayedBytes += MachineProtocol.HeaderBytes + 1 + payload.Length;
        }

        /// <summary>Queues the frames held back whose time has come, in the order they fall due.</summary>
        private void ReleaseDelayed(MachineLink link, double now)
        {
            DelayQueue<MachineDelayedFrame> delayed = link.Delayed;
            while (delayed != null && delayed.TryPeek(out double due, out _) && due <= now)
            {
                MachineDelayedFrame frame = delayed.Pop();
                link.DelayedBytes -= MachineProtocol.HeaderBytes + 1 + frame.Length;
                QueueFrame(link, frame.Kind, new ReadOnlySpan<byte>(frame.Buffer, 0, frame.Length), force: true);
                DelayedFrames.Return(frame.Buffer);
            }
        }

        private static void DiscardDelayed(MachineLink link)
        {
            DelayQueue<MachineDelayedFrame> delayed = link.Delayed;
            while (delayed != null && delayed.Count > 0)
            {
                DelayedFrames.Return(delayed.Pop().Buffer);
            }

            link.DelayedBytes = 0;
        }

        /// <summary>
        /// Sends a probe, or the answer to one, in a ping frame. It carries its round trip so far
        /// and how many packets this side sent and lost, so the other side learns its loss.
        /// </summary>
        private void SendProbe(MachineLink link, byte probe, double roundTrip)
        {
            if (!Simulate(link, isReliable: false, isProbe: true, out double delay))
            {
                return;
            }

            _writer.Clear();
            _writer.WriteByte(probe);
            _writer.WriteULong((ulong)Math.Round((roundTrip + delay) * 1_000_000.0));
            _writer.WriteULong((ulong)link.PacketsOut);
            _writer.WriteULong((ulong)link.PacketsLostOut);
            Transmit(link, MachineFrameKind.Ping, _writer.Written, isReliable: false, delay);
        }

        private void HandleProbe(MachineLink link, ReadOnlySpan<byte> payload)
        {
            byte probe;
            double roundTrip;
            long sent;
            long lost;
            try
            {
                PacketReader reader = new PacketReader(payload);
                probe = reader.ReadByte();
                roundTrip = reader.ReadULong() / 1_000_000.0;
                sent = (long)reader.ReadULong();
                lost = (long)reader.ReadULong();
            }
            catch (FormatException)
            {
                // A probe of another version; the ping kept the connection alive all the same.
                return;
            }

            // A probe that a newer one overtook tells nothing new.
            if (sent >= link.PacketsIn && lost >= link.PacketsLostIn && lost - link.PacketsLostIn <= sent - link.PacketsIn)
            {
                link.Meter.AddInbound(sent - link.PacketsIn, lost - link.PacketsLostIn);
                link.PacketsIn = sent;
                link.PacketsLostIn = lost;
            }

            if (probe == MachineProtocol.ProbeRequest)
            {
                SendProbe(link, MachineProtocol.ProbeReply, roundTrip);
            }
            else if (probe == MachineProtocol.ProbeReply)
            {
                link.Meter.AddRoundTrip(roundTrip);
            }
        }
        #endregion

        public void Disconnect(ConnectionId connection, int reasonCode)
        {
            if (!_sessions.TryGetValue(connection, out MachineLink link))
            {
                return;
            }

            _sessions.Remove(connection);
            CloseLink(link, sendClose: true, reasonCode);
        }

        public string GetPlayerName(PlayerId player)
        {
            if (player == LocalPlayerId)
            {
                return PlayerName;
            }

            foreach (MachineLobby lobby in _lobbies.Values)
            {
                for (int i = 0; i < lobby.Members.Count; i++)
                {
                    if (lobby.Members[i].Id == player)
                    {
                        return lobby.Members[i].Name;
                    }
                }
            }

            foreach (MachineLink link in _sessions.Values)
            {
                if (link.Remote == player && link.RemoteName != null)
                {
                    return link.RemoteName;
                }
            }

            return player.ToString();
        }

        /// <summary>
        /// The port of a player this player shares a lobby with, preferring the lobby's owner,
        /// whom a session connects to. 0 when no lobby lists the player.
        /// </summary>
        private int FindPort(PlayerId player)
        {
            int memberPort = 0;
            foreach (MachineLobby lobby in _lobbies.Values)
            {
                if (lobby.OwnerId == player && lobby.OwnerPort != Port)
                {
                    return lobby.OwnerPort;
                }

                for (int i = 0; i < lobby.Members.Count; i++)
                {
                    MachineMember member = lobby.Members[i];
                    if (member.Id == player && member.Port != Port)
                    {
                        memberPort = member.Port;
                    }
                }
            }

            return memberPort;
        }

        /// <summary>The first frame of an incoming connection asks for a session with this player.</summary>
        private void AcceptSession(MachineLink link, ReadOnlySpan<byte> payload)
        {
            PlayerId remote;
            string name;
            PlayerId expected;
            try
            {
                PacketReader reader = new PacketReader(payload);
                ReadVersion(ref reader);
                remote = new PlayerId(reader.ReadULong());
                name = reader.ReadString();
                expected = new PlayerId(reader.ReadULong());
            }
            catch (FormatException)
            {
                EndLink(link, LinkEnd.Lost, 0);
                return;
            }

            // A port that changed hands since the other side learned it leads to someone else.
            if (!IsListening || expected != LocalPlayerId || !remote.IsValid)
            {
                QueueFrame(link, MachineFrameKind.SessionRefuse, ReadOnlySpan<byte>.Empty, force: true);
                CloseLink(link, sendClose: false, 0);
                return;
            }

            ConnectionId id = new ConnectionId(_nextConnectionId++);
            link.Role = MachineLinkRole.Session;
            link.Connection = id;
            link.Remote = remote;
            link.RemoteName = name;
            link.IsIncoming = true;
            link.IsReported = true;
            link.Deadline = 0.0;
            link.Meter = new ConnectionMeter(Now);
            _sessions[id] = link;

            _writer.Clear();
            _writer.WriteULong(LocalPlayerId.Value);
            _writer.WriteString(PlayerName);
            QueueFrame(link, MachineFrameKind.SessionAccept, _writer.Written, force: true);
            Flush(link);

            _listener?.OnConnected(id, remote, true);
        }

        private void HandleSessionFrame(MachineLink link, MachineFrameKind kind, ReadOnlySpan<byte> payload)
        {
            switch (kind)
            {
                case MachineFrameKind.Data:
                    if (link.IsReported)
                    {
                        link.Meter.AddReceived(payload.Length);
                        _listener?.OnReceived(link.Connection, link.Remote, payload);
                    }
                    break;

                case MachineFrameKind.SessionAccept:
                    HandleSessionAccepted(link, payload);
                    break;

                case MachineFrameKind.SessionRefuse:
                    EndLink(link, LinkEnd.ConnectFailed, 0);
                    break;

                case MachineFrameKind.Close:
                    int reasonCode = 0;
                    try
                    {
                        PacketReader reader = new PacketReader(payload);
                        reasonCode = reader.ReadInt();
                    }
                    catch (FormatException)
                    {
                        // Closed all the same, without a reason.
                    }

                    EndLink(link, LinkEnd.Closed, reasonCode);
                    break;

                default:
                    EndLink(link, LinkEnd.Lost, 0);
                    break;
            }
        }

        private void HandleSessionAccepted(MachineLink link, ReadOnlySpan<byte> payload)
        {
            if (link.IsIncoming || link.IsReported)
            {
                EndLink(link, LinkEnd.Lost, 0);
                return;
            }

            PlayerId remote;
            string name;
            try
            {
                PacketReader reader = new PacketReader(payload);
                remote = new PlayerId(reader.ReadULong());
                name = reader.ReadString();
            }
            catch (FormatException)
            {
                EndLink(link, LinkEnd.ConnectFailed, 0);
                return;
            }

            if (remote != link.Remote)
            {
                EndLink(link, LinkEnd.ConnectFailed, 0);
                return;
            }

            link.RemoteName = name;
            link.IsReported = true;
            link.Deadline = 0.0;
            link.Meter = new ConnectionMeter(Now);
            _listener?.OnConnected(link.Connection, link.Remote, false);
        }

        private void EndSessionLink(MachineLink link, LinkEnd end, int reasonCode)
        {
            // A connection this side closed itself reports nothing more.
            if (!_sessions.TryGetValue(link.Connection, out MachineLink current) || current != link)
            {
                return;
            }

            _sessions.Remove(link.Connection);

            BackendDisconnectKind kind;
            if (!link.IsReported)
            {
                kind = BackendDisconnectKind.ConnectFailed;
            }
            else if (end == LinkEnd.Closed)
            {
                kind = BackendDisconnectKind.ClosedByPeer;
            }
            else
            {
                kind = BackendDisconnectKind.ConnectionLost;
                reasonCode = 0;
            }

            _listener?.OnDisconnected(link.Connection, link.Remote, kind, reasonCode);
        }

        private static void ReadVersion(ref PacketReader reader)
        {
            if (reader.ReadInt() != MachineProtocol.Magic || reader.ReadInt() != MachineProtocol.Version)
            {
                throw new FormatException("Another version of the local network protocol.");
            }
        }
    }
}
