using System;
using Mu3Library.Multiplayer.Data;
using UnityEngine;

namespace Mu3Library.Multiplayer.Sync
{
    public sealed partial class StateSyncManager
    {
        #region Sending
        private void RunTick(double now)
        {
            _isTicking = true;
            try
            {
                RunTickCore(now);
            }
            finally
            {
                _isTicking = false;
            }
        }

        private void RunTickCore(double now)
        {
            // The tick follows the clock, so a manager created anew on the same player goes on
            // from where the last one stopped instead of sending ticks its copies read as old.
            uint byClock = unchecked((uint)(long)Math.Floor(now / _tickInterval));
            _tick = unchecked(_tick + 1);
            if (unchecked((int)(byClock - _tick)) > 0)
            {
                _tick = byClock;
            }

            RefreshSlots();
            _tickBytes.Clear();

            bool isHost = _session.IsHost;
            PlayerId local = _session.LocalPlayerId;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                entry.FullOffset = -1;
                entry.FullFailed = false;
                entry.DeltaOffset = -1;
                entry.DeltaFailed = false;

                bool clientOwned = IsClientOwned(entry);
                if (isHost)
                {
                    // The host sends its own states as they change, and a client's whole as each
                    // update from its owner arrives.
                    entry.IsRelay = clientOwned;
                    entry.Sends = !clientOwned || entry.HasReceived;
                    entry.Changed = clientOwned ? entry.Received : ReadDirty(entry);
                }
                else
                {
                    entry.IsRelay = false;
                    entry.Sends = clientOwned && entry.Owner == local;
                    entry.Changed = entry.Sends && ReadDirty(entry);
                }
            }

            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                PlayerId recipient = _slots[slot];
                if (recipient.IsValid)
                {
                    SendTo(slot, recipient, isHost, now);
                }

                // A handler the send ran may end the game or dispose this manager.
                if (_isDisposed || _session.State != SessionState.InGame)
                {
                    break;
                }
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Sends && !entry.IsRelay && entry.Changed)
                {
                    try
                    {
                        entry.State.ClearDirty();
                    }
                    catch (Exception e)
                    {
                        LogFailure("Clearing the change", e, entry.Id);
                    }
                }

                entry.Received = false;
            }
        }

        /// <summary>
        /// Sends one player this tick's states that are due for them, by claim, as many as fit
        /// the budget, in one message per delivery mode.
        /// </summary>
        private void SendTo(int slot, PlayerId recipient, bool isHost, double now)
        {
            int count = 0;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (!entry.Sends || (entry.IsRelay && entry.Owner == recipient))
                {
                    continue;
                }

                ref Peer peer = ref entry.Peers[slot];
                if (isHost && entry.Interest != null && !IsInterested(entry, recipient))
                {
                    // Its copy stops following, so it takes the whole state when it is let in again.
                    peer.NeedsFull = true;
                    peer.Claim = 0f;
                    continue;
                }

                bool isDue = entry.Mode == DeliveryMode.Unreliable && _fullStateInterval > 0.0 && now >= peer.FullDueAt;
                if (!peer.NeedsFull && !isDue && !entry.Changed)
                {
                    continue;
                }

                peer.Claim += entry.Priority;
                if (count == _candidates.Length)
                {
                    Array.Resize(ref _candidates, _candidates.Length * 2);
                }

                _candidates[count++] = new Candidate
                {
                    Entry = entry,
                    Full = peer.NeedsFull || isDue || entry.IsRelay,
                    Claim = peer.Claim,
                    Order = entry.Order,
                };
            }

            SortCandidates(count);

            int budget = Math.Min(_maxBytesPerTick, _messenger.MaxMessageSize);
            int tickSize = SyncBuffer.VarUIntSize(_tick);
            _reliable.Clear();
            _unreliable.Clear();
            int used = 0;
            int sent = 0;
            int deferred = 0;
            for (int c = 0; c < count; c++)
            {
                ref Candidate candidate = ref _candidates[c];
                candidate.Sent = false;
                Entry entry = candidate.Entry;
                ref Peer peer = ref entry.Peers[slot];
                if (!TryWrite(entry, candidate.Full, out int offset, out int length))
                {
                    // The state's own write threw, which is logged; it goes whole once it writes.
                    peer.NeedsFull = true;
                    deferred++;
                    continue;
                }

                ulong key = ((ulong)entry.Id << 1) | (candidate.Full ? 1UL : 0UL);
                int entrySize = SyncBuffer.VarUIntSize(key) + SyncBuffer.VarUIntSize((ulong)length) + length;
                SyncBuffer bundle = entry.Mode == DeliveryMode.Reliable ? _reliable : _unreliable;
                int size = entrySize + (bundle.IsEmpty ? tickSize : 0);
                if (used + size > budget)
                {
                    if (entrySize + tickSize > budget)
                    {
                        LogTooLarge(entry, entrySize + tickSize, budget);
                    }

                    // The player misses this tick's state, so the next one that reaches them is whole.
                    peer.NeedsFull = true;
                    deferred++;
                    continue;
                }

                if (bundle.IsEmpty)
                {
                    bundle.WriteVarUInt(_tick);
                }

                bundle.WriteVarUInt(key);
                bundle.WriteVarUInt((ulong)length);
                bundle.WriteRaw(_tickBytes.Slice(offset, length));
                used += size;
                sent++;
                candidate.Sent = true;
                peer.Claim = 0f;
                peer.NeedsFull = false;
                if (candidate.Full)
                {
                    peer.FullDueAt = now + _fullStateInterval;
                }
            }

            ref Stats stats = ref _stats[slot];
            stats.LastTickBytes = used;
            stats.LastTickStates = sent;
            stats.LastTickDeferred = deferred;
            if (used > 0)
            {
                stats.LastTick = _tick;
                stats.TotalBytes += used;
                stats.MaxTickBytes = Math.Max(stats.MaxTickBytes, used);
            }

            MessageTarget target = isHost ? MessageTarget.To(recipient) : MessageTarget.Host;
            if (!_reliable.IsEmpty)
            {
                Deliver(slot, count, _reliable, target, DeliveryMode.Reliable);
                if (_isDisposed || _session.State != SessionState.InGame)
                {
                    return;
                }
            }

            if (!_unreliable.IsEmpty)
            {
                Deliver(slot, count, _unreliable, target, DeliveryMode.Unreliable);
            }
        }

        private void Deliver(int slot, int count, SyncBuffer bundle, MessageTarget target, DeliveryMode mode)
        {
            if (_messenger.Send(_messageType, bundle.Written, target, mode).IsSuccess)
            {
                return;
            }

            // What did not leave reaches the player whole once it can.
            for (int c = 0; c < count; c++)
            {
                Candidate candidate = _candidates[c];
                if (candidate.Sent && candidate.Entry.Mode == mode)
                {
                    candidate.Entry.Peers[slot].NeedsFull = true;
                }
            }
        }

        /// <summary>The state's bytes for this tick, written once a tick for its changes and once whole.</summary>
        private bool TryWrite(Entry entry, bool full, out int offset, out int length)
        {
            if (full)
            {
                if (entry.FullOffset < 0 && !entry.FullFailed)
                {
                    entry.FullFailed = !WriteState(entry, true, out entry.FullOffset, out entry.FullLength);
                }

                offset = entry.FullOffset;
                length = entry.FullLength;
                return !entry.FullFailed;
            }

            if (entry.DeltaOffset < 0 && !entry.DeltaFailed)
            {
                entry.DeltaFailed = !WriteState(entry, false, out entry.DeltaOffset, out entry.DeltaLength);
            }

            offset = entry.DeltaOffset;
            length = entry.DeltaLength;
            return !entry.DeltaFailed;
        }

        private bool WriteState(Entry entry, bool full, out int offset, out int length)
        {
            _stateWriter.Clear();
            try
            {
                if (full)
                {
                    entry.State.WriteFull(_stateWriter);
                }
                else
                {
                    entry.State.WriteDelta(_stateWriter);
                }
            }
            catch (Exception e)
            {
                LogFailure(full ? "Writing the whole state" : "Writing the changes", e, entry.Id);
                offset = -1;
                length = 0;
                return false;
            }

            offset = _tickBytes.Length;
            length = _stateWriter.Length;
            _tickBytes.WriteRaw(_stateWriter.Written);
            return true;
        }

        private bool ReadDirty(Entry entry)
        {
            try
            {
                return entry.State.IsDirty;
            }
            catch (Exception e)
            {
                LogFailure("Reading whether it changed", e, entry.Id);
                return false;
            }
        }

        private bool IsInterested(Entry entry, PlayerId recipient)
        {
            try
            {
                return entry.Interest(entry.State, recipient);
            }
            catch (Exception e)
            {
                LogFailure("The interest filter", e, entry.Id);
                return false;
            }
        }

        private void LogTooLarge(Entry entry, int size, int budget)
        {
            if (entry.LoggedTooLarge)
            {
                return;
            }

            entry.LoggedTooLarge = true;
            Debug.LogWarning($"[StateSync] State {entry.Id} takes {size} bytes in a tick, past the {budget} bytes a tick sends one player, so it is never sent. "
                + "Raise MaxBytesPerTick or split the state.");
        }

        /// <summary>Puts the candidates in the order they go in: the largest claim first, then the earliest registered.</summary>
        private void SortCandidates(int count)
        {
            Candidate[] items = _candidates;
            for (int start = count / 2 - 1; start >= 0; start--)
            {
                SiftDown(items, start, count);
            }

            for (int end = count - 1; end > 0; end--)
            {
                Candidate last = items[end];
                items[end] = items[0];
                items[0] = last;
                SiftDown(items, 0, end);
            }
        }

        /// <summary>Keeps the heap's root the candidate that goes last.</summary>
        private static void SiftDown(Candidate[] items, int root, int count)
        {
            while (true)
            {
                int child = 2 * root + 1;
                if (child >= count)
                {
                    return;
                }

                if (child + 1 < count && GoesBefore(items[child], items[child + 1]))
                {
                    child++;
                }

                if (!GoesBefore(items[root], items[child]))
                {
                    return;
                }

                Candidate swap = items[root];
                items[root] = items[child];
                items[child] = swap;
                root = child;
            }
        }

        private static bool GoesBefore(in Candidate a, in Candidate b)
        {
            return a.Claim > b.Claim || (a.Claim == b.Claim && a.Order < b.Order);
        }
        #endregion

        #region Receiving
        private void OnMessage(MessageContext context, ReadOnlySpan<byte> payload)
        {
            if (_isDisposed || _session.State != SessionState.InGame)
            {
                return;
            }

            bool isHost = _session.IsHost;
            PlayerId sender = context.Sender;
            if (!isHost && sender != _session.HostId)
            {
                return;
            }

            PlayerId local = _session.LocalPlayerId;
            SyncBundleReader reader = new SyncBundleReader(payload);
            uint tick;
            try
            {
                tick = (uint)reader.ReadVarUInt(uint.MaxValue);
            }
            catch (PayloadFormatException e)
            {
                LogFailure("Reading a tick", e, 0u);
                return;
            }

            SyncReadContext readContext = new SyncReadContext(sender, tick, _tickInterval, Now, context.DeliveryMode);
            while (reader.Remaining > 0)
            {
                ulong key;
                ReadOnlySpan<byte> bytes;
                try
                {
                    key = reader.ReadVarUInt(((ulong)uint.MaxValue << 1) | 1UL);
                    int length = (int)reader.ReadVarUInt(int.MaxValue);
                    bytes = reader.Take(length);
                }
                catch (PayloadFormatException e)
                {
                    // Past a malformed entry nothing is where it should be, so the rest of the tick goes.
                    LogFailure("Reading a tick", e, 0u);
                    return;
                }

                uint id = (uint)(key >> 1);
                bool full = (key & 1UL) != 0UL;
                if (!_byId.TryGetValue(id, out Entry entry))
                {
                    // Not registered here, or not yet.
                    continue;
                }

                // The host takes a client's state from its owner only; a client takes every state
                // from the host but its own.
                bool fromItsSender = isHost
                    ? IsClientOwned(entry) && entry.Owner == sender
                    : !(IsClientOwned(entry) && entry.Owner == local);
                if (!fromItsSender)
                {
                    LogOnce("sender", $"Dropped an update of state {id} from {sender}, who does not send it.");
                    continue;
                }

                if (context.DeliveryMode == DeliveryMode.Unreliable && entry.HasLastTick)
                {
                    int age = unchecked((int)(entry.LastTick - tick));
                    if (age >= 0 && age < ReorderWindowTicks)
                    {
                        // As old as an update this copy already read.
                        continue;
                    }
                }

                entry.HasLastTick = true;
                entry.LastTick = tick;

                MessageReader stateReader = new MessageReader(bytes);
                try
                {
                    if (full)
                    {
                        entry.State.ReadFull(ref stateReader, readContext);
                    }
                    else
                    {
                        entry.State.ReadDelta(ref stateReader, readContext);
                    }
                }
                catch (Exception e)
                {
                    LogFailure(full ? "Reading the whole state" : "Reading the changes", e, id);
                    continue;
                }

                if (isHost)
                {
                    entry.Received = true;
                    entry.HasReceived = true;
                }

                // A state's read may dispose this manager or end the game.
                if (_isDisposed || _session.State != SessionState.InGame)
                {
                    return;
                }
            }
        }
        #endregion
    }
}
