using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Mu3Library.Multiplayer.Data;
using Debug = UnityEngine.Debug;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// The lobbies of a machine-wide network, as files in its folder: each process that owns a
    /// lobby writes one file with the port it listens on and every lobby it owns, rewrites it
    /// while it runs, and deletes it when it stops. A file that has not been rewritten for
    /// <see cref="StaleSeconds"/> belongs to a process that ended without deleting it.
    /// </summary>
    /// <remarks>
    /// The owner lists its lobbies on the main thread, into a buffer it keeps. A listing that adds
    /// a lobby is written there and then, so the lobby is in the folder by the end of the update
    /// that created or took it over; every other listing goes to a thread of this directory,
    /// which writes the file from a copy: replacing a file takes milliseconds (about 7 ms on
    /// Windows) that a frame cannot spare, and the owner rewrites the file every few seconds even
    /// when nothing changed, since readers judge it by the time written inside. The thread starts
    /// with the first listing and ends when the file is deleted or abandoned, or when scripts
    /// reload; deleting and abandoning wait for a write in progress first, so no write lands after
    /// them.
    /// </remarks>
    internal sealed class MachineDirectory
    {
        public const double StaleSeconds = 10.0;

        private const int FileMagic = 0x4C33554D;
        private const int FileVersion = 1;
        private const string Extension = ".lobbies";

        // The writer thread only writes small files; the default stack would be a megabyte.
        private const int WriterStackBytes = 128 * 1024;

        private readonly string _folder;
        private readonly string _path;
        private readonly string _temporaryPath;

        // Main thread only: the latest listing, written into the same buffer each time, and the
        // lobbies it listed.
        private readonly MemoryStream _listing = new MemoryStream();
        private readonly BinaryWriter _listingWriter;
        private readonly List<LobbyId> _listed = new List<LobbyId>();
        private Thread _writer = null;
        private EventHandler _onDomainUnload = null;
        private bool _hasFile = false;

        // Shared with the writer thread under _gate: the listing it writes next.
        private readonly object _gate = new object();
        private byte[] _pending = Array.Empty<byte>();
        private int _pendingLength = 0;
        private bool _hasPending = false;
        private bool _isStopping = false;

        // Set when a write fails, taken by the main thread.
        private int _hasFailed = 0;

        // Writer thread only.
        private byte[] _writing = Array.Empty<byte>();

        // Whichever thread writes the file: the main thread only while no writer thread runs.
        private bool _hasFolder = false;

        public MachineDirectory(string folder, int port)
        {
            _folder = folder;
            _path = PathOf(folder, port);
            _temporaryPath = Path.ChangeExtension(_path, ".tmp");
            _listingWriter = new BinaryWriter(_listing, Encoding.UTF8, leaveOpen: true);
        }

        /// <summary>True when the lobbies include one that the last listing did not.</summary>
        public bool AddsLobby(List<MachineLobby> lobbies)
        {
            for (int i = 0; i < lobbies.Count; i++)
            {
                if (!IsListed(lobbies[i].Id))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Lists the lobbies this process owns, or deletes its file when it owns none. A listing
        /// that adds a lobby is written at once, the others in the background; when a write fails,
        /// for example while another program holds the file, <see cref="TakeFailedWrite"/> says
        /// so, and the caller lists them again later.
        /// </summary>
        public void Publish(PlayerId owner, int port, List<MachineLobby> lobbies)
        {
            if (lobbies.Count == 0)
            {
                Remove();
                return;
            }

            bool addsLobby = AddsLobby(lobbies);
            _listed.Clear();
            _listing.SetLength(0);
            BinaryWriter writer = _listingWriter;
            writer.Write(FileMagic);
            writer.Write(FileVersion);
            writer.Write(DateTime.UtcNow.Ticks);
            writer.Write(port);
            writer.Write(owner.Value);
            writer.Write(lobbies.Count);
            for (int i = 0; i < lobbies.Count; i++)
            {
                MachineLobby lobby = lobbies[i];
                _listed.Add(lobby.Id);
                writer.Write(lobby.Id.Value);
                writer.Write((byte)lobby.Visibility);
                writer.Write(lobby.Capacity);
                writer.Write(lobby.Members.Count);
                writer.Write(lobby.IsJoinable);
                writer.Write(lobby.Data.Count);
                foreach (KeyValuePair<string, string> pair in lobby.Data)
                {
                    writer.Write(pair.Key);
                    writer.Write(pair.Value ?? string.Empty);
                }
            }

            writer.Flush();
            int length = (int)_listing.Length;

            // Set before the first write, so Remove also deletes a temporary file a failed write left.
            _hasFile = true;

            if (addsLobby || _writer == null)
            {
                // A process that learns the new lobby's ID at once can join it. The first listing
                // creates the file, a fraction of a replacement; a later one that adds a lobby,
                // which a process owning several lobbies makes, replaces it here, after the write
                // in progress if there is one.
                StopWriting();
                if (!TryWrite(_listing.GetBuffer(), length))
                {
                    Interlocked.Exchange(ref _hasFailed, 1);
                }

                StartWriting();
                return;
            }

            lock (_gate)
            {
                if (_pending.Length < length)
                {
                    _pending = new byte[Math.Max(length, _pending.Length * 2)];
                }

                // Replaces a listing the thread has not taken yet, which is out of date now.
                Buffer.BlockCopy(_listing.GetBuffer(), 0, _pending, 0, length);
                _pendingLength = length;
                _hasPending = true;
                Monitor.Pulse(_gate);
            }
        }

        /// <summary>True once after a listing could not be written.</summary>
        public bool TakeFailedWrite()
        {
            return Volatile.Read(ref _hasFailed) != 0 && Interlocked.Exchange(ref _hasFailed, 0) != 0;
        }

        /// <summary>Deletes the file, after the write in progress if there is one.</summary>
        public void Remove()
        {
            StopWriting();
            _listed.Clear();
            if (!_hasFile)
            {
                return;
            }

            _hasFile = false;
            TryDelete(_path);
            TryDelete(_temporaryPath);
        }

        /// <summary>
        /// Stops writing and leaves the file as it is, as a process that crashed does, after the
        /// write in progress if there is one: the file goes stale unless a reader deletes it sooner.
        /// </summary>
        public void Abandon()
        {
            StopWriting();
            _listed.Clear();
            _hasFile = false;
        }

        private bool IsListed(LobbyId lobby)
        {
            for (int i = 0; i < _listed.Count; i++)
            {
                if (_listed[i] == lobby)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartWriting()
        {
            // A backend dropped without Dispose would leave the thread waiting until the process
            // ends, and Unity reloads scripts without ending the process.
            if (_onDomainUnload == null)
            {
                _onDomainUnload = (sender, args) => AskToStop();
            }

            AppDomain.CurrentDomain.DomainUnload += _onDomainUnload;
            _isStopping = false;
            _writer = new Thread(WriteListings, WriterStackBytes)
            {
                IsBackground = true,
                Name = "Mu3Library.Multiplayer lobby file",
            };
            _writer.Start();
        }

        /// <summary>
        /// Ends the writer thread, dropping a listing it has not taken yet, and waits for the
        /// write in progress, so nothing writes the file once this returns.
        /// </summary>
        private void StopWriting()
        {
            if (_writer == null)
            {
                return;
            }

            AskToStop();
            _writer.Join();
            _writer = null;
            AppDomain.CurrentDomain.DomainUnload -= _onDomainUnload;
            Interlocked.Exchange(ref _hasFailed, 0);
        }

        private void AskToStop()
        {
            lock (_gate)
            {
                _isStopping = true;
                _hasPending = false;
                Monitor.Pulse(_gate);
            }
        }

        /// <summary>The writer thread: writes the latest listing whenever there is one, until it stops.</summary>
        private void WriteListings()
        {
            while (true)
            {
                int length;
                lock (_gate)
                {
                    while (!_hasPending && !_isStopping)
                    {
                        Monitor.Wait(_gate);
                    }

                    if (_isStopping)
                    {
                        return;
                    }

                    if (_writing.Length < _pendingLength)
                    {
                        _writing = new byte[_pending.Length];
                    }

                    Buffer.BlockCopy(_pending, 0, _writing, 0, _pendingLength);
                    length = _pendingLength;
                    _hasPending = false;
                }

                if (!TryWrite(_writing, length))
                {
                    Interlocked.Exchange(ref _hasFailed, 1);
                }
            }
        }

        private bool TryWrite(byte[] listing, int length)
        {
            try
            {
                if (!_hasFolder)
                {
                    Directory.CreateDirectory(_folder);
                    _hasFolder = true;
                }

                try
                {
                    WriteFile(listing, length);
                }
                catch (DirectoryNotFoundException)
                {
                    // Something deleted the folder since.
                    Directory.CreateDirectory(_folder);
                    WriteFile(listing, length);
                }

                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception e) when (!(e is ThreadAbortException))
            {
                // Logged rather than thrown, since on the writer thread it would end the thread.
                Debug.LogException(e);
                return false;
            }
        }

        private void WriteFile(byte[] listing, int length)
        {
            // Written in one call, so the stream needs no buffer of its own.
            using (FileStream file = new FileStream(_temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1))
            {
                file.Write(listing, 0, length);
            }

            // A reader sees the old file or the new one, never half of one.
            if (File.Exists(_path))
            {
                File.Replace(_temporaryPath, _path, null);
            }
            else
            {
                File.Move(_temporaryPath, _path);
            }
        }

        /// <summary>
        /// Every lobby in the folder whose owner still runs. Files left by processes that ended
        /// are deleted on the way.
        /// </summary>
        public static List<MachineDirectoryEntry> Read(string folder)
        {
            List<MachineDirectoryEntry> entries = new List<MachineDirectoryEntry>();

            string[] files;
            try
            {
                if (!Directory.Exists(folder))
                {
                    return entries;
                }

                files = Directory.GetFiles(folder, "*" + Extension);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return entries;
            }

            long staleBefore = DateTime.UtcNow.Ticks - TimeSpan.FromSeconds(StaleSeconds).Ticks;
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (!file.EndsWith(Extension, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReadFile(file, staleBefore, entries))
                {
                    TryDelete(file);
                }
            }

            return KeepNewest(entries);
        }

        /// <summary>
        /// A lobby that passed to another member shows up in two files until the old owner's goes
        /// stale or is deleted; the newer file names the owner that runs it now.
        /// </summary>
        private static List<MachineDirectoryEntry> KeepNewest(List<MachineDirectoryEntry> entries)
        {
            Dictionary<LobbyId, int> newest = new Dictionary<LobbyId, int>();
            for (int i = 0; i < entries.Count; i++)
            {
                if (!newest.TryGetValue(entries[i].Lobby, out int index) || entries[index].Written < entries[i].Written)
                {
                    newest[entries[i].Lobby] = i;
                }
            }

            if (newest.Count == entries.Count)
            {
                return entries;
            }

            List<MachineDirectoryEntry> kept = new List<MachineDirectoryEntry>(newest.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (newest[entries[i].Lobby] == i)
                {
                    kept.Add(entries[i]);
                }
            }

            return kept;
        }

        /// <summary>
        /// Adds the lobbies of one file. True when the file is stale; one that cannot be read adds
        /// nothing.
        /// </summary>
        private static bool ReadFile(string file, long staleBefore, List<MachineDirectoryEntry> entries)
        {
            int firstEntry = entries.Count;

            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadInt32() != FileMagic || reader.ReadInt32() != FileVersion)
                    {
                        return false;
                    }

                    long written = reader.ReadInt64();
                    if (written < staleBefore)
                    {
                        return true;
                    }

                    int port = reader.ReadInt32();
                    PlayerId owner = new PlayerId(reader.ReadUInt64());
                    int lobbyCount = reader.ReadInt32();
                    for (int i = 0; i < lobbyCount; i++)
                    {
                        MachineDirectoryEntry entry = new MachineDirectoryEntry
                        {
                            Lobby = new LobbyId(reader.ReadUInt64()),
                            Owner = owner,
                            Written = written,
                            Port = port,
                            Visibility = (LobbyVisibility)reader.ReadByte(),
                            Capacity = reader.ReadInt32(),
                            MemberCount = reader.ReadInt32(),
                            IsJoinable = reader.ReadBoolean(),
                        };

                        int dataCount = reader.ReadInt32();
                        for (int j = 0; j < dataCount; j++)
                        {
                            string key = reader.ReadString();
                            entry.Data[key] = reader.ReadString();
                        }

                        entries.Add(entry);
                    }
                }

                return false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is FormatException || e is ArgumentException)
            {
                // Deleted or replaced while being read, or not a file this version wrote.
                entries.RemoveRange(firstEntry, entries.Count - firstEntry);
                return false;
            }
        }

        /// <summary>
        /// Deletes the file of the process that listened on <paramref name="port"/> when the file
        /// still names <paramref name="owner"/>: that process ended or stalled, while a newer
        /// process on the same port keeps its own file.
        /// </summary>
        public static void RemoveEnded(string folder, int port, PlayerId owner)
        {
            string file = PathOf(folder, port);
            bool isTheirs;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    isTheirs = reader.ReadInt32() == FileMagic
                        && reader.ReadInt32() == FileVersion
                        && reader.ReadInt64() != 0
                        && reader.ReadInt32() == port
                        && reader.ReadUInt64() == owner.Value;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                return;
            }

            if (isTheirs)
            {
                TryDelete(file);
            }
        }

        private static string PathOf(string folder, int port)
        {
            return Path.Combine(folder, "peer-" + port.ToString(CultureInfo.InvariantCulture) + Extension);
        }

        private static void TryDelete(string file)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Another process holds it; whoever reads it next tries again.
            }
        }
    }
}
