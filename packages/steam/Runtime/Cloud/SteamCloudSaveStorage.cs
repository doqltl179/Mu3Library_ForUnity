using System;
using System.Collections.Generic;
using Mu3Library.Save;

namespace Mu3Library.Steam.Cloud
{
    /// <summary>
    /// An <see cref="ISaveStorage"/> on Steam Remote Storage, so a <see cref="SaveManager"/> keeps its
    /// slots in Steam Cloud: each slot is the file <c>&lt;folder&gt;/&lt;slot ID&gt;.sav</c> of the
    /// app's Steam Cloud files, which Steam keeps on this computer and sends to the cloud while Steam
    /// Cloud is on for the account and the app. It uses the Steam the game started and starts none
    /// itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steam writes a file whole in one call (<c>FileWrite</c>); whether a write that a crash cuts
    /// short leaves the previous bytes is up to the Steam client, which this storage cannot check.
    /// Steam's file names ignore case and Steam lists them in lower case, so
    /// <see cref="GetSlotIds"/>, and with it <see cref="SaveManager.GetSlots"/>, answers lower-case
    /// slot IDs: give slots lower-case IDs, or compare them ignoring case. A Steam call
    /// that throws because Steam cannot run in this process answers
    /// <see cref="SaveFailureReason.StorageFailed"/> from a read, write, or delete, false from
    /// <see cref="Exists"/>, and no slot from <see cref="GetSlotIds"/>; a write Steam refuses,
    /// such as one past the app's Steam Cloud quota or larger than 100 MiB, answers
    /// <see cref="SaveFailureReason.StorageFailed"/> too.
    /// </para>
    /// <para>
    /// The assembly compiles only with Steamworks.NET 2025.164.1 or later, and only in the editor
    /// and in 64-bit standalone players, as <c>Mu3Library.Steam.Multiplayer</c> does.
    /// </para>
    /// </remarks>
    public sealed class SteamCloudSaveStorage : ISaveStorage
    {
        public const string DefaultFolder = "Saves";
        public const string FileExtension = ".sav";

        // Steam refuses one file larger than this (k_unMaxCloudFileChunkSize).
        private const int MaxFileSize = 100 * 1024 * 1024;

        private readonly ISteamRemoteStorage _steam;

        /// <summary>
        /// The folder of the slots within the app's Steam Cloud files, or empty for none.
        /// </summary>
        public string Folder { get; }

        /// <summary>
        /// True while Steam runs and Steam Cloud is on for both this player's account and the app;
        /// otherwise the files stay on this computer.
        /// </summary>
        public bool IsCloudEnabled
        {
            get
            {
                try
                {
                    return _steam.IsCloudEnabledForAccount() && _steam.IsCloudEnabledForApp();
                }
                catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
                {
                    return false;
                }
            }
        }



        /// <param name="folder">
        /// The folder of the slots: letters, digits, <c>_</c>, and <c>-</c>, with <c>/</c> between
        /// folder names, or empty for the top of the app's files.
        /// </param>
        public SteamCloudSaveStorage(string folder = DefaultFolder) : this(folder, new SteamworksRemoteStorage())
        {
        }

        internal SteamCloudSaveStorage(string folder, ISteamRemoteStorage steam)
        {
            if (folder == null)
            {
                throw new ArgumentNullException(nameof(folder));
            }

            if (!IsValidFolder(folder))
            {
                throw new ArgumentException($"'{folder}' is not a folder name: use letters, digits, _, and -, with / between folder names.", nameof(folder));
            }

            Folder = folder;
            _steam = steam;
        }

        /// <summary>The name of the slot's file among the app's Steam Cloud files.</summary>
        public string GetFileName(string slotId) => Folder.Length > 0 ? $"{Folder}/{slotId}{FileExtension}" : slotId + FileExtension;

        public bool Exists(string slotId)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return false;
            }

            try
            {
                return _steam.FileExists(GetFileName(slotId));
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
            {
                return false;
            }
        }

        public SaveResult Read(string slotId, out byte[] data)
        {
            data = null;
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            string fileName = GetFileName(slotId);
            try
            {
                if (!_steam.FileExists(fileName))
                {
                    return SaveResult.Failure(SaveFailureReason.SlotNotFound);
                }

                int size = _steam.GetFileSize(fileName);
                if (size < 0 || size > MaxFileSize)
                {
                    return SaveResult.Failure(SaveFailureReason.StorageFailed, message: $"Steam reported {size} bytes for {fileName}.");
                }

                byte[] bytes = new byte[size];
                int read = size > 0 ? _steam.FileRead(fileName, bytes, size) : 0;
                if (read != size)
                {
                    return SaveResult.Failure(SaveFailureReason.StorageFailed, message: $"Steam read {read} of the {size} bytes of {fileName}.");
                }

                data = bytes;
                return SaveResult.Success;
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
            {
                return NotRunning(exception);
            }
        }

        public SaveResult Write(string slotId, byte[] data)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            if (data == null)
            {
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: "No data to write.");
            }

            if (data.Length > MaxFileSize)
            {
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: $"Steam writes at most {MaxFileSize} bytes to one file.");
            }

            string fileName = GetFileName(slotId);
            try
            {
                return _steam.FileWrite(fileName, data, data.Length)
                    ? SaveResult.Success
                    : SaveResult.Failure(SaveFailureReason.StorageFailed, message: $"Steam did not write {fileName}: the app's Steam Cloud quota may be full.");
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
            {
                return NotRunning(exception);
            }
        }

        public SaveResult Delete(string slotId)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            string fileName = GetFileName(slotId);
            try
            {
                if (!_steam.FileExists(fileName) || _steam.FileDelete(fileName))
                {
                    return SaveResult.Success;
                }

                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: $"Steam did not delete {fileName}.");
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
            {
                return NotRunning(exception);
            }
        }

        public void GetSlotIds(List<string> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            string prefix = Folder.Length > 0 ? Folder + "/" : string.Empty;
            try
            {
                int count = _steam.GetFileCount();
                for (int i = 0; i < count; i++)
                {
                    string fileName = _steam.GetFileNameAndSize(i, out _);
                    if (fileName == null ||
                        fileName.Length <= prefix.Length + FileExtension.Length ||
                        !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                        !fileName.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string slotId = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - FileExtension.Length);
                    if (SaveLimits.IsValidSlotId(slotId))
                    {
                        results.Add(slotId);
                    }
                }
            }
            catch (Exception exception) when (SteamCalls.IsSteamUnavailable(exception))
            {
                // Without Steam the storage holds no slot it can read.
            }
        }



        private static SaveResult NotRunning(Exception exception) =>
            SaveResult.Failure(SaveFailureReason.StorageFailed, message: $"Steam is not running: {exception.Message}");

        private static bool IsValidFolder(string folder)
        {
            if (folder.Length == 0)
            {
                return true;
            }

            foreach (string part in folder.Split('/'))
            {
                if (part.Length == 0)
                {
                    return false;
                }

                foreach (char c in part)
                {
                    bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                    if (!ok)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
