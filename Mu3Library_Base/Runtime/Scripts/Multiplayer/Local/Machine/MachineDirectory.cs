using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local.Machine
{
    /// <summary>
    /// The lobbies of a machine-wide network, as files in its folder: each process that owns a
    /// lobby writes one file with the port it listens on and every lobby it owns, rewrites it
    /// while it runs, and deletes it when it stops. A file that has not been rewritten for
    /// <see cref="StaleSeconds"/> belongs to a process that ended without deleting it.
    /// </summary>
    internal sealed class MachineDirectory
    {
        public const double StaleSeconds = 10.0;

        private const int FileMagic = 0x4C33554D;
        private const int FileVersion = 1;
        private const string Extension = ".lobbies";

        private readonly string _folder;
        private readonly string _path;
        private readonly string _temporaryPath;
        private bool _hasFile = false;

        public MachineDirectory(string folder, int port)
        {
            _folder = folder;
            _path = PathOf(folder, port);
            _temporaryPath = Path.ChangeExtension(_path, ".tmp");
        }

        /// <summary>
        /// Lists the lobbies this process owns, or deletes its file when it owns none. False when
        /// the file could not be written, for example while another program holds it; the caller
        /// tries again later.
        /// </summary>
        public bool Publish(PlayerId owner, int port, List<MachineLobby> lobbies)
        {
            if (lobbies.Count == 0)
            {
                Remove();
                return true;
            }

            try
            {
                Directory.CreateDirectory(_folder);

                // Set before writing, so Remove also deletes a temporary file a failed write left.
                _hasFile = true;

                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                    {
                        writer.Write(FileMagic);
                        writer.Write(FileVersion);
                        writer.Write(DateTime.UtcNow.Ticks);
                        writer.Write(port);
                        writer.Write(owner.Value);
                        writer.Write(lobbies.Count);
                        for (int i = 0; i < lobbies.Count; i++)
                        {
                            MachineLobby lobby = lobbies[i];
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
                    }

                    File.WriteAllBytes(_temporaryPath, stream.ToArray());
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

                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void Remove()
        {
            if (!_hasFile)
            {
                return;
            }

            _hasFile = false;
            TryDelete(_path);
            TryDelete(_temporaryPath);
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
