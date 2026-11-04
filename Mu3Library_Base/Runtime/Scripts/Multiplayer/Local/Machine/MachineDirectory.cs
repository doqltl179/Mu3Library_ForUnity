using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    /// <para>
    /// A replacement is not one step on Windows: <see cref="File.Replace(string, string, string)"/>
    /// renames the old file to a backup beside it, named after it with <c>~RF</c> and a number,
    /// moves the new one in, and deletes the backup. For a moment the folder lists neither file
    /// under the lobby file's name, and opening it fails as missing or as held. A reader therefore
    /// goes by every file of an owner, its lobby file, its temporary file, and that backup, and
    /// reads a lobby file it cannot open again shortly while another of them was written within
    /// <see cref="ReplaceSeconds"/>, or was listed and is gone a moment later, as a file the
    /// replacement just moved. Since that takes a sleep or more, <see cref="MachinePeer"/> reads
    /// on a thread of the pool, never on the main thread. A process that ends in the middle of a
    /// write or a replacement leaves the temporary file or the backup behind, and a reader deletes
    /// them once they are <see cref="StaleSeconds"/> old and the lobby file is gone, as it deletes
    /// a lobby file that stopped being rewritten.
    /// </para>
    /// <para>
    /// A reader takes a file only within the bounds an owner of this version keeps to:
    /// <see cref="MaxFileBytes"/>, <see cref="MaxListedLobbies"/>, and
    /// <see cref="MaxLobbyDataEntries"/>. It skips a file past them as malformed, before it reads
    /// what the file declares, and goes on with the other files.
    /// </para>
    /// <para>
    /// On an AMD Ryzen 7 5800X3D with Windows 11 Pro and Unity 6000.6.7f1, a dispose or a
    /// simulated crash that met a write in progress took 7 to 11 ms instead of about 1 ms, and a
    /// listing that adds a lobby while the file is there took 7 to 11 ms for its own replacement,
    /// 5 to 10 ms more when it met a write. A write runs for those milliseconds every two seconds,
    /// or every quarter second while the lobbies keep changing, so few calls meet one; the listing
    /// that deletes the file after the last lobby left comes a quarter second after the last
    /// listing at the soonest and met none. Not waiting would take a second file for each owner,
    /// and a delete that a process ending right after its dispose could miss, so the directory waits.
    /// </para>
    /// </remarks>
    internal sealed class MachineDirectory
    {
        public const double StaleSeconds = 10.0;

        private const int FileMagic = 0x4C33554D;
        private const int FileVersion = 1;
        private const string FilePrefix = "peer-";
        private const string Extension = ".lobbies";
        private const string TemporaryExtension = ".tmp";

        // What File.Replace names the backup of the file it replaces on Windows: the file's name,
        // this mark, a number, and the extension.
        private const string BackupMark = "~RF";
        private const string BackupExtension = ".TMP";

        // What a reader names a lobby file it takes aside to delete: the file's name, this mark,
        // a number of its own, and the temporary extension (DeleteIfUnchanged).
        private const string AsideMark = "~del";

        /// <summary>
        /// How long one read waits at most, in all, for lobby files whose owners replace them; a
        /// replacement takes a few milliseconds.
        /// </summary>
        internal const double ReplaceWaitSeconds = 0.05;

        /// <summary>
        /// How recently an owner in the middle of a replacement wrote one of its files: it writes
        /// the new file a few milliseconds before it moves it in. A lobby file that is missing
        /// while the owner's files are all older belongs to a process that ended in the middle of
        /// a write or a replacement, and a read does not wait for it.
        /// </summary>
        /// <remarks>
        /// While <see cref="File.Replace(string, string, string)"/> or the first move runs, either
        /// the temporary file was just written or the new lobby file, which keeps its time when it
        /// is moved, has a fresh one; the backup keeps the old file's time and does not count. A
        /// lobby file a read can miss this way belongs to a writer that stalled for more than a
        /// second between writing the temporary file and finishing the replacement, or one that a
        /// third program holds while its time is old, and the read would give up on either after
        /// <see cref="ReplaceWaitSeconds"/> anyway. How long a replacement takes under an
        /// antivirus scan or on a slow disk was not measured.
        /// </remarks>
        internal const double ReplaceSeconds = 1.0;

        /// <summary>
        /// Most lobbies one file lists. Each backend writes a file of its own, and the backend of
        /// a <see cref="MultiplayerManager"/> owns one lobby at a time; the rest leaves room for a
        /// test or a tool that creates lobbies through a <see cref="LocalBackend"/> itself, whose
        /// <see cref="LocalBackend.CreateLobby"/> creates no more. An owner lists no more, leaving
        /// out the lobbies past it that taking lobbies over brings, so a file that declares more
        /// is malformed.
        /// </summary>
        internal const int MaxListedLobbies = 64;

        /// <summary>
        /// Most data entries one listed lobby carries: as many as the frames that bring a lobby to
        /// its members carry. A <see cref="MultiplayerManager"/>'s lobby holds at most 2,119: its
        /// own data takes at most <see cref="MultiplayerLimits.MaxLobbyDataSize"/> bytes of
        /// distinct keys and their values, at most 128 keys take a single byte and the others two
        /// or more, and the library adds seven keys of its own. A <see cref="LocalBackend"/>
        /// refuses a lobby or a new key past it, and an owner would leave a lobby with more out of
        /// its file.
        /// </summary>
        internal const int MaxLobbyDataEntries = MachineProtocol.MaxListLength;

        /// <summary>
        /// Largest file, in bytes. A <see cref="MultiplayerManager"/>'s lobby takes under 16 KiB
        /// of it with the length of each string, so <see cref="MaxListedLobbies"/> of them fit,
        /// and so does any one lobby that can reach its members: the frame that carries a lobby
        /// (<see cref="MachineProtocol.MaxFrameBytes"/>) takes more bytes for it than the file
        /// does. A <see cref="LocalBackend"/> refuses a lobby or a change that would take its file
        /// past it with every lobby it owns listed (<see cref="ListingFixedBytes"/>,
        /// <see cref="ListedBytes"/>), and an owner leaves a lobby that does not fit, which only
        /// taking lobbies over brings, out of its file.
        /// </summary>
        internal const int MaxFileBytes = 1024 * 1024;

        /// <summary>
        /// What <see cref="Publish"/> writes before the lobbies: the file's mark and version, the
        /// time written, the owner's port and ID, and the lobby count.
        /// </summary>
        internal const int ListingFixedBytes = sizeof(int) + sizeof(int) + sizeof(long) + sizeof(int) + sizeof(ulong) + sizeof(int);

        // What Publish writes for a lobby before its data: the lobby ID, the visibility, the
        // capacity, the member count, whether it is joinable, and the data count.
        private const int ListedLobbyFixedBytes = sizeof(ulong) + 1 + sizeof(int) + sizeof(int) + 1 + sizeof(int);

        private enum FileRead
        {
            /// <summary>Read, or not a file this version wrote.</summary>
            Done,
            /// <summary>Not rewritten for <see cref="StaleSeconds"/>.</summary>
            Stale,
            /// <summary>Not there, as in the middle of a replacement, or after the owner ended.</summary>
            Missing,
            /// <summary>Held by another handle, as in the middle of a replacement.</summary>
            Held,
        }

        // The writer thread only writes small files; the default stack would be a megabyte.
        private const int WriterStackBytes = 128 * 1024;

        private readonly string _folder;
        private readonly string _path;
        private readonly string _temporaryPath;

        // Main thread only: the latest listing, written into the same buffer each time, and the
        // lobbies it took, also those it left out past the bounds.
        private readonly MemoryStream _listing = new MemoryStream();
        private readonly BinaryWriter _listingWriter;
        private readonly List<LobbyId> _listed = new List<LobbyId>();
        private Thread _writer = null;
        private EventHandler _onDomainUnload = null;
        private bool _hasFile = false;
        private bool _hasWarnedLeftOut = false;

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
        private bool _isFileInPlace = false;

        public MachineDirectory(string folder, int port)
        {
            _folder = folder;
            _path = PathOf(folder, port);
            _temporaryPath = Path.ChangeExtension(_path, TemporaryExtension);
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
        /// so, and the caller lists them again later. Past the bounds a reader takes, the listing
        /// leaves a lobby out, with a warning the first time.
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

            // The count follows once the lobbies that fit are known.
            long countPosition = _listing.Position;
            writer.Write(0);
            int count = 0;
            for (int i = 0; i < lobbies.Count; i++)
            {
                MachineLobby lobby = lobbies[i];

                // Also one left out, so it does not count as a lobby the next listing adds.
                _listed.Add(lobby.Id);
                if (count < MaxListedLobbies && lobby.Data.Count <= MaxLobbyDataEntries)
                {
                    long start = _listing.Position;
                    writer.Write(lobby.Id.Value);
                    writer.Write((byte)lobby.Visibility);
                    writer.Write(ListedCapacity(lobby));
                    writer.Write(lobby.Members.Count);
                    writer.Write(lobby.IsJoinable);
                    writer.Write(lobby.Data.Count);
                    foreach (KeyValuePair<string, string> pair in lobby.Data)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value ?? string.Empty);
                    }

                    if (_listing.Length <= MaxFileBytes)
                    {
                        count++;
                        continue;
                    }

                    _listing.SetLength(start);
                    _listing.Position = start;
                }

                WarnLeftOut(lobby.Id);
            }

            _listing.Position = countPosition;
            writer.Write(count);
            _listing.Position = _listing.Length;
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

        /// <summary>
        /// The bytes <see cref="Publish"/> writes for the lobby, counted field by field as it
        /// writes them, without writing; a file takes <see cref="ListingFixedBytes"/> more. Change
        /// it with <see cref="Publish"/>: <c>MultiplayerLocalMachineTests</c> compare the two.
        /// </summary>
        internal static int ListedBytes(MachineLobby lobby)
        {
            int bytes = ListedLobbyFixedBytes;
            foreach (KeyValuePair<string, string> pair in lobby.Data)
            {
                bytes += ListedEntryBytes(pair.Key, pair.Value);
            }

            return bytes;
        }

        /// <summary>The bytes one lobby data entry takes in what <see cref="Publish"/> writes.</summary>
        internal static int ListedEntryBytes(string key, string value) => StringBytes(key) + StringBytes(value);

        /// <summary>
        /// The capacity <see cref="Publish"/> lists for the lobby, which search shows (#368): the
        /// lobby's own, cut to its members and as many more players as the frames can still carry
        /// it with whatever their names, <see cref="MachineProtocol.MaxMemberBytes"/> each, when
        /// that is less. So a lobby whose owner could refuse the next player for the frame's sake
        /// lists as full and leaves search, as a full lobby does, and a player that search shows
        /// a free slot fits in it whatever its name.
        /// </summary>
        internal static int ListedCapacity(MachineLobby lobby)
        {
            int free = lobby.Capacity - lobby.Members.Count;
            if (free <= 0)
            {
                return lobby.Capacity;
            }

            // Counted as the owner counts a join (MachinePeer.AcceptJoin), without allocating;
            // a lobby taken over past a frame has no room at all.
            int room = MachineProtocol.MaxSnapshotBytes - MachineProtocol.SnapshotBytes(lobby);
            int fits = room > 0 ? room / MachineProtocol.MaxMemberBytes : 0;
            return fits < free ? lobby.Members.Count + fits : lobby.Capacity;
        }

        /// <summary>
        /// What <see cref="BinaryWriter.Write(string)"/> writes: the count of UTF-8 bytes as a
        /// 7-bit encoded number, seven bits to a byte, then the bytes.
        /// </summary>
        private static int StringBytes(string value)
        {
            int length = Encoding.UTF8.GetByteCount(value ?? string.Empty);
            int prefix = 1;
            for (int rest = length >> 7; rest != 0; rest >>= 7)
            {
                prefix++;
            }

            return prefix + length;
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
            _isFileInPlace = false;
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

        private void WarnLeftOut(LobbyId lobby)
        {
            if (!_hasWarnedLeftOut)
            {
                _hasWarnedLeftOut = true;
                Debug.LogWarning($"[Multiplayer] A backend on the local network lists at most {MaxListedLobbies} of its lobbies, each with at most {MaxLobbyDataEntries} data entries, in at most {MaxFileBytes} bytes; lobby {lobby} is left out of search and of joins by its ID while it does not fit. This warning is not repeated for this backend.");
            }
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

        /// <summary>
        /// Writes the file from the listing: what the writer thread does for each listing, and the
        /// main thread for one that adds a lobby. Internal so the tests can measure one write.
        /// </summary>
        internal bool TryWrite(byte[] listing, int length)
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

            // A reader sees the old file or the new one, never half of one. Whether the file is
            // there follows from the last write, rather than from a File.Exists on every write,
            // which allocates; a file that something else deleted or created since, as a reader
            // deletes the file of an owner it takes for ended, takes the other call.
            try
            {
                MoveIntoPlace(_isFileInPlace);
            }
            catch (IOException) when (File.Exists(_path) != _isFileInPlace)
            {
                MoveIntoPlace(!_isFileInPlace);
            }

            _isFileInPlace = true;
        }

        private void MoveIntoPlace(bool replaces)
        {
            if (replaces)
            {
                File.Replace(_temporaryPath, _path, null);
            }
            else
            {
                File.Move(_temporaryPath, _path);
            }
        }

        /// <summary>
        /// Every lobby in the folder whose owner still runs, also one whose owner is replacing its
        /// file in the meantime. Files left by processes that ended are deleted on the way.
        /// </summary>
        /// <remarks>
        /// It sleeps while an owner replaces its file, up to <see cref="ReplaceWaitSeconds"/> and
        /// one sleep more in all, so call it off the main thread.
        /// </remarks>
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

                files = Directory.GetFiles(folder, FilePrefix + "*");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return entries;
            }

            // An owner's port, from whichever of its files the folder lists at the moment.
            List<int> ports = new List<int>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                if (TryGetPort(Path.GetFileName(files[i]), out int port) && !ports.Contains(port))
                {
                    ports.Add(port);
                }
            }

            long now = DateTime.UtcNow.Ticks;
            long staleBefore = now - TimeSpan.FromSeconds(StaleSeconds).Ticks;
            long replacingSince = now - TimeSpan.FromSeconds(ReplaceSeconds).Ticks;
            long waitUntil = Stopwatch.GetTimestamp() + (long)(ReplaceWaitSeconds * Stopwatch.Frequency);
            for (int i = 0; i < ports.Count; i++)
            {
                string file = PathOf(folder, ports[i]);
                bool isReadAgain = false;
                while (true)
                {
                    FileRead read = ReadFile(file, staleBefore, entries, out long written);
                    if (read == FileRead.Stale)
                    {
                        if (DeleteIfUnchanged(file, written))
                        {
                            RemoveLeftovers(files, ports[i], staleBefore);
                            break;
                        }

                        // Not the file this read found stale any more, as when its owner put a
                        // new one in its place since: what the path holds now is read instead, once.
                        if (isReadAgain)
                        {
                            break;
                        }

                        isReadAgain = true;
                        continue;
                    }

                    if (read == FileRead.Done)
                    {
                        break;
                    }

                    if (!IsReplacing(folder, ports[i], replacingSince))
                    {
                        // Only a lobby file that is gone leaves what is beside it to nobody.
                        if (read == FileRead.Missing)
                        {
                            RemoveLeftovers(files, ports[i], staleBefore);
                        }

                        break;
                    }

                    if (Stopwatch.GetTimestamp() >= waitUntil)
                    {
                        break;
                    }

                    Thread.Sleep(1);
                }
            }

            return KeepNewest(entries);
        }

        /// <summary>The port in the name of a file an owner writes, or a replacement leaves beside it.</summary>
        private static bool TryGetPort(string name, out int port)
        {
            port = 0;
            int end = name.IndexOf('.', FilePrefix.Length);
            return name.StartsWith(FilePrefix, StringComparison.Ordinal)
                && end > FilePrefix.Length
                && int.TryParse(name.Substring(FilePrefix.Length, end - FilePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out port)
                && port > 0;
        }

        /// <summary>
        /// Whether the owner on <paramref name="port"/> is replacing its lobby file: one of its
        /// files was written since <paramref name="since"/>, as the new file is a moment before
        /// the replacement moves it in, or the folder listed it and it is gone a moment later,
        /// moved by the replacement. A backup carries the time of the file it replaced, and what
        /// a process that ended left behind is older and stays where it is.
        /// </summary>
        private static bool IsReplacing(string folder, int port, long since)
        {
            try
            {
                string[] files = Directory.GetFiles(folder, FilePrefix + port.ToString(CultureInfo.InvariantCulture) + ".*");
                for (int i = 0; i < files.Length; i++)
                {
                    // For a file that is not there it reads a time long past, 1601 on Windows.
                    if (File.GetLastWriteTimeUtc(files[i]).Ticks >= since || !File.Exists(files[i]))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // The folder is gone, or the files with it.
            }

            return false;
        }

        /// <summary>
        /// Deletes the temporary file and the replacement backups of the owner on
        /// <paramref name="port"/>, among <paramref name="files"/>, that were written before
        /// <paramref name="staleBefore"/>: with its lobby file gone, they are what a process that
        /// ended in the middle of a write or a replacement left, which nobody else deletes. An
        /// owner that runs writes its temporary file anew for every listing, and a backup carries
        /// the time of a lobby file it rewrites every few seconds, so its own are never that old.
        /// So does a stale lobby file that a reader took aside to delete and did not, since it
        /// ended first.
        /// </summary>
        private static void RemoveLeftovers(string[] files, int port, long staleBefore)
        {
            string name = FilePrefix + port.ToString(CultureInfo.InvariantCulture);
            string temporary = name + TemporaryExtension;
            string backup = name + Extension + BackupMark;
            string aside = name + Extension + AsideMark;
            for (int i = 0; i < files.Length; i++)
            {
                string file = Path.GetFileName(files[i]);
                bool isLeftover = string.Equals(file, temporary, StringComparison.OrdinalIgnoreCase)
                    || (file.StartsWith(backup, StringComparison.OrdinalIgnoreCase) && file.EndsWith(BackupExtension, StringComparison.OrdinalIgnoreCase))
                    || (file.StartsWith(aside, StringComparison.OrdinalIgnoreCase) && file.EndsWith(TemporaryExtension, StringComparison.OrdinalIgnoreCase));

                if (!isLeftover)
                {
                    continue;
                }

                try
                {
                    // A file deleted since reads as written in 1601 on Windows, and deleting it again does nothing.
                    if (File.GetLastWriteTimeUtc(files[i]).Ticks < staleBefore)
                    {
                        File.Delete(files[i]);
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    // Another process holds it; whoever reads the folder next tries again.
                }
            }
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
        /// Adds the lobbies of one file. One that cannot be read adds nothing, nor does one past
        /// <see cref="MaxFileBytes"/>, <see cref="MaxListedLobbies"/>, or
        /// <see cref="MaxLobbyDataEntries"/>, which is checked before anything it declares is
        /// read; a lobby with a key or a value longer than the frames carry is left out.
        /// <paramref name="written"/> is the time written inside a file of this version, 0 for
        /// any other.
        /// </summary>
        private static FileRead ReadFile(string file, long staleBefore, List<MachineDirectoryEntry> entries, out long written)
        {
            int firstEntry = entries.Count;
            written = 0;

            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (stream.Length > MaxFileBytes
                        || reader.ReadInt32() != FileMagic
                        || reader.ReadInt32() != FileVersion)
                    {
                        return FileRead.Done;
                    }

                    written = reader.ReadInt64();
                    if (written < staleBefore)
                    {
                        return FileRead.Stale;
                    }

                    int port = reader.ReadInt32();
                    PlayerId owner = new PlayerId(reader.ReadUInt64());
                    int lobbyCount = ReadCount(reader, MaxListedLobbies);
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

                        int dataCount = ReadCount(reader, MaxLobbyDataEntries);
                        bool isWithinLimits = true;
                        for (int j = 0; j < dataCount; j++)
                        {
                            string key = ReadString(reader, MachineProtocol.MaxDataKeyBytes);
                            string value = ReadString(reader, MachineProtocol.MaxDataValueBytes);
                            if (key == null || value == null)
                            {
                                isWithinLimits = false;
                            }
                            else
                            {
                                entry.Data[key] = value;
                            }
                        }

                        if (isWithinLimits)
                        {
                            entries.Add(entry);
                        }
                    }
                }

                return FileRead.Done;
            }
            catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                // Deleted, or moved aside by a replacement in the meantime.
                entries.RemoveRange(firstEntry, entries.Count - firstEntry);
                return FileRead.Missing;
            }
            catch (Exception e) when ((e is IOException && !(e is EndOfStreamException)) || e is UnauthorizedAccessException)
            {
                // Replaced or held in the meantime.
                entries.RemoveRange(firstEntry, entries.Count - firstEntry);
                return FileRead.Held;
            }
            catch (Exception e) when (e is EndOfStreamException || e is FormatException || e is ArgumentException)
            {
                // Not a file this version wrote.
                entries.RemoveRange(firstEntry, entries.Count - firstEntry);
                return FileRead.Done;
            }
        }

        /// <summary>A count the file declares, or a <see cref="FormatException"/> past <paramref name="max"/>.</summary>
        private static int ReadCount(BinaryReader reader, int max)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > max)
            {
                throw new FormatException($"Invalid count {count}.");
            }

            return count;
        }

        /// <summary>
        /// Reads a string that <see cref="BinaryWriter"/> wrote: its UTF-8 bytes, after their count
        /// as a 7-bit encoded number. Null when there are more than <paramref name="maxBytes"/>,
        /// which are skipped undecoded, so the reader goes on with what follows them.
        /// </summary>
        private static string ReadString(BinaryReader reader, int maxBytes)
        {
            int length = 0;
            for (int shift = 0; ; shift += 7)
            {
                if (shift > 28)
                {
                    throw new FormatException("A string length takes more than five bytes.");
                }

                byte part = reader.ReadByte();
                length |= (part & 0x7F) << shift;
                if ((part & 0x80) == 0)
                {
                    break;
                }
            }

            if (length < 0)
            {
                throw new FormatException($"Invalid string length {length}.");
            }

            if (length > maxBytes)
            {
                Stream stream = reader.BaseStream;
                if (length > stream.Length - stream.Position)
                {
                    throw new EndOfStreamException();
                }

                stream.Seek(length, SeekOrigin.Current);
                return null;
            }

            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length < length)
            {
                throw new EndOfStreamException();
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Deletes the file of the process that listened on <paramref name="port"/> when the file
        /// still names <paramref name="owner"/>: that process ended or stalled, while a newer
        /// process on the same port keeps its own file, and so does the owner once it rewrote its
        /// file after this read it (<see cref="DeleteIfUnchanged"/>).
        /// </summary>
        public static void RemoveEnded(string folder, int port, PlayerId owner)
        {
            string file = PathOf(folder, port);
            if (TryReadHeader(file, out long written, out int filePort, out PlayerId fileOwner)
                && written != 0
                && filePort == port
                && fileOwner == owner)
            {
                DeleteIfUnchanged(file, written);
            }
        }

        /// <summary>
        /// Deletes the lobby file at <paramref name="file"/> when it is still the one a read found
        /// written at <paramref name="written"/>, and leaves a file that replaced it since: an
        /// owner whose main thread stalled for longer than <see cref="StaleSeconds"/>, as under a
        /// debugger or a long load, can replace its file between that read and this delete.
        /// True when it deleted that file; false when the path may hold another file, which a
        /// read then reads instead: one that went back or took the place, or one it could not
        /// take aside, being held, as in the middle of a replacement, or gone.
        /// </summary>
        /// <remarks>
        /// A delete by path would take whatever file is there, and a check of the file before it
        /// would leave the moment between them. So it first moves the file aside, a single step that
        /// takes the file the path names at that moment, under a name that only this call uses;
        /// what it reads and deletes then is the very file it took. A file written since goes
        /// back, unless the owner wrote a newer one in the meantime, as it does when it finds its
        /// file gone; until then a reader that misses the lobby file finds the file taken aside,
        /// written as recently as the owner wrote it, and waits for it as for a replacement
        /// (<see cref="IsReplacing"/>). A reader that ends between the move and the delete leaves
        /// a stale file beside the lobby file, which <see cref="RemoveLeftovers"/> deletes.
        /// </remarks>
        internal static bool DeleteIfUnchanged(string file, long written)
        {
            string aside = file + AsideMark + Guid.NewGuid().ToString("N") + TemporaryExtension;
            try
            {
                File.Move(file, aside);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Gone already, or held, as in the middle of a replacement.
                return false;
            }

            if (TryReadHeader(aside, out long asideWritten, out _, out _) && asideWritten == written)
            {
                TryDelete(aside);
                return true;
            }

            try
            {
                File.Move(aside, file);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // A newer file took the place.
                TryDelete(aside);
            }

            return false;
        }

        /// <summary>The time written, the port, and the owner that a lobby file of this version starts with.</summary>
        private static bool TryReadHeader(string file, out long written, out int port, out PlayerId owner)
        {
            written = 0;
            port = 0;
            owner = PlayerId.None;
            try
            {
                using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    if (reader.ReadInt32() != FileMagic || reader.ReadInt32() != FileVersion)
                    {
                        return false;
                    }

                    written = reader.ReadInt64();
                    port = reader.ReadInt32();
                    owner = new PlayerId(reader.ReadUInt64());
                    return true;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                return false;
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
