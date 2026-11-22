using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mu3Library.Save
{
    /// <summary>
    /// Keeps each slot in a file of its own, <c>&lt;slot ID&gt;.sav</c>, in one folder: by default
    /// <c>Saves</c> under <see cref="Application.persistentDataPath"/>. A write goes to a temporary
    /// file first, which then replaces the slot's file, so a write that stops partway leaves the
    /// previous save readable.
    /// </summary>
    public class FileSaveStorage : ISaveStorage
    {
        public const string FileExtension = ".sav";
        private const string TemporaryExtension = ".tmp";

        /// <summary>
        /// The folder that holds the slot files.
        /// </summary>
        public string Directory { get; }



        /// <summary>
        /// Keeps the slots in <c>Saves</c> under <see cref="Application.persistentDataPath"/>.
        /// Construct it on the main thread, which that property needs.
        /// </summary>
        public FileSaveStorage() : this(Path.Combine(Application.persistentDataPath, "Saves"))
        {
        }

        public FileSaveStorage(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("The save folder is empty.", nameof(directory));
            }

            Directory = directory;
        }

        public bool Exists(string slotId) => IsValid(slotId) && File.Exists(GetPath(slotId));

        public SaveResult Read(string slotId, out byte[] data)
        {
            data = null;
            if (!IsValid(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            string path = GetPath(slotId);
            try
            {
                if (!File.Exists(path))
                {
                    return SaveResult.Failure(SaveFailureReason.SlotNotFound);
                }

                data = File.ReadAllBytes(path);
                return SaveResult.Success;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: exception.Message);
            }
        }

        public SaveResult Write(string slotId, byte[] data)
        {
            if (!IsValid(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            if (data == null)
            {
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: "No data to write.");
            }

            string path = GetPath(slotId);
            string temporaryPath = path + TemporaryExtension;
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporaryPath, path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        File.Delete(path);
                        File.Move(temporaryPath, path);
                    }
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                return SaveResult.Success;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                TryDelete(temporaryPath);
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: exception.Message);
            }
        }

        public SaveResult Delete(string slotId)
        {
            if (!IsValid(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            string path = GetPath(slotId);
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                TryDelete(path + TemporaryExtension);
                return SaveResult.Success;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                return SaveResult.Failure(SaveFailureReason.StorageFailed, message: exception.Message);
            }
        }

        public void GetSlotIds(List<string> results)
        {
            if (!System.IO.Directory.Exists(Directory))
            {
                return;
            }

            try
            {
                foreach (string path in System.IO.Directory.EnumerateFiles(Directory))
                {
                    if (!string.Equals(Path.GetExtension(path), FileExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string slotId = Path.GetFileNameWithoutExtension(path);
                    if (SaveLimits.IsValidSlotId(slotId))
                    {
                        results.Add(slotId);
                    }
                }
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                // A folder that cannot be listed holds no slot this storage can read.
            }
        }

        /// <summary>
        /// The path of the slot's file.
        /// </summary>
        public string GetPath(string slotId) => Path.Combine(Directory, slotId + FileExtension);



        private static bool IsValid(string slotId) => SaveLimits.IsValidSlotId(slotId);

        private static bool IsStorageException(Exception exception) =>
            exception is IOException || exception is UnauthorizedAccessException || exception is NotSupportedException ||
            exception is System.Security.SecurityException;

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                // A temporary file that stays is replaced by the next write.
            }
        }
    }
}
