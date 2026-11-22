using System;
using System.Collections.Generic;

namespace Mu3Library.Save
{
    /// <summary>
    /// Saves and loads registered sections into slots, each slot one block of bytes in an
    /// <see cref="ISaveStorage"/>. See <see cref="ISaveManager"/>.
    /// </summary>
    public class SaveManager : ISaveManager, ISaveManagerEventBus
    {
        private readonly ISaveSerializer _serializer;
        private readonly Dictionary<string, ISaveSection> _sections = new();
        private readonly List<ISaveSection> _sectionOrder = new();

        public ISaveStorage Storage { get; }

        private int _maxSlotSize = SaveLimits.DefaultMaxSlotSize;
        public int MaxSlotSize
        {
            get => _maxSlotSize;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The slot size limit must be positive.");
                }

                _maxSlotSize = value;
            }
        }

        public event Action<string, string> OnSaved;
        public event Action<string, string> OnLoaded;
        public event Action<string> OnSlotDeleted;



        /// <summary>
        /// Keeps slots in a <see cref="FileSaveStorage"/> under
        /// <see cref="UnityEngine.Application.persistentDataPath"/> and writes them as JSON.
        /// </summary>
        public SaveManager() : this(new FileSaveStorage(), new JsonSaveSerializer())
        {
        }

        public SaveManager(ISaveStorage storage, ISaveSerializer serializer = null)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _serializer = serializer ?? new JsonSaveSerializer();
        }

        #region Sections
        public SaveResult RegisterSection(ISaveSection section)
        {
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            string key = section.Key;
            if (!SaveLimits.IsValidSectionKey(key))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, key);
            }

            if (section.Version < 1 || section.DataType == null)
            {
                return SaveResult.Failure(SaveFailureReason.SectionFailed, key, "A section needs a version of 1 or more and a data type.");
            }

            if (_sections.ContainsKey(key))
            {
                return SaveResult.Failure(SaveFailureReason.SectionAlreadyRegistered, key);
            }

            _sections.Add(key, section);
            _sectionOrder.Add(section);
            return SaveResult.Success;
        }

        public bool UnregisterSection(string key)
        {
            if (key == null || !_sections.TryGetValue(key, out ISaveSection section))
            {
                return false;
            }

            _sections.Remove(key);
            _sectionOrder.Remove(section);
            return true;
        }

        public bool IsSectionRegistered(string key) => key != null && _sections.ContainsKey(key);
        #endregion

        #region Save
        public SaveResult Save(string slotId)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            var captured = new List<SaveSlotFile.Entry>(_sectionOrder.Count);
            long now = DateTime.UtcNow.Ticks;
            foreach (ISaveSection section in _sectionOrder)
            {
                SaveResult result = CaptureEntry(section, now, out SaveSlotFile.Entry entry);
                if (!result.IsSuccess)
                {
                    return result;
                }

                captured.Add(entry);
            }

            // A damaged slot cannot keep its other sections, so a whole save writes it anew.
            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (read.Reason == SaveFailureReason.SlotNotFound || read.Reason == SaveFailureReason.Corrupted)
            {
                file = new SaveSlotFile { CreatedTicks = now };
            }
            else if (!read.IsSuccess)
            {
                return read;
            }

            foreach (SaveSlotFile.Entry entry in captured)
            {
                file.Set(entry);
            }

            file.UpdatedTicks = now;
            SaveResult written = WriteSlot(slotId, file);
            if (written.IsSuccess)
            {
                OnSaved?.Invoke(slotId, null);
            }

            return written;
        }

        public SaveResult Save(string slotId, string sectionKey)
        {
            if (!SaveLimits.IsValidSlotId(slotId) || !SaveLimits.IsValidSectionKey(sectionKey))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, sectionKey);
            }

            if (!_sections.TryGetValue(sectionKey, out ISaveSection section))
            {
                return SaveResult.Failure(SaveFailureReason.SectionNotRegistered, sectionKey);
            }

            long now = DateTime.UtcNow.Ticks;
            SaveResult result = CaptureEntry(section, now, out SaveSlotFile.Entry entry);
            if (!result.IsSuccess)
            {
                return result;
            }

            // Saving one section into a damaged slot would drop the others it held, so it fails.
            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (read.Reason == SaveFailureReason.SlotNotFound)
            {
                file = new SaveSlotFile { CreatedTicks = now };
            }
            else if (!read.IsSuccess)
            {
                return read;
            }

            file.Set(entry);
            file.UpdatedTicks = now;
            SaveResult written = WriteSlot(slotId, file);
            if (written.IsSuccess)
            {
                OnSaved?.Invoke(slotId, sectionKey);
            }

            return written;
        }
        #endregion

        #region Load
        public SaveResult Load(string slotId)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (!read.IsSuccess)
            {
                return read;
            }

            // Read every section first, so a damaged or too new one restores none of them.
            var pending = new List<(ISaveSection Section, object Data, int Version)>(_sectionOrder.Count);
            var missing = new List<ISaveSection>();
            foreach (ISaveSection section in _sectionOrder)
            {
                SaveSlotFile.Entry entry = file.Find(section.Key);
                if (entry == null)
                {
                    missing.Add(section);
                    continue;
                }

                SaveResult result = ReadEntry(section, entry, out object data);
                if (!result.IsSuccess)
                {
                    return result;
                }

                pending.Add((section, data, entry.Version));
            }

            foreach ((ISaveSection section, object data, int version) in pending)
            {
                SaveResult result = RestoreEntry(section, data, version);
                if (!result.IsSuccess)
                {
                    return result;
                }
            }

            foreach (ISaveSection section in missing)
            {
                try
                {
                    section.RestoreDefault();
                }
                catch (Exception exception)
                {
                    return SaveResult.Failure(SaveFailureReason.SectionFailed, section.Key, exception.Message);
                }
            }

            OnLoaded?.Invoke(slotId, null);
            return SaveResult.Success;
        }

        public SaveResult Load(string slotId, string sectionKey)
        {
            if (!SaveLimits.IsValidSlotId(slotId) || !SaveLimits.IsValidSectionKey(sectionKey))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, sectionKey);
            }

            if (!_sections.TryGetValue(sectionKey, out ISaveSection section))
            {
                return SaveResult.Failure(SaveFailureReason.SectionNotRegistered, sectionKey);
            }

            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (!read.IsSuccess)
            {
                return read;
            }

            SaveSlotFile.Entry entry = file.Find(sectionKey);
            if (entry == null)
            {
                return SaveResult.Failure(SaveFailureReason.SectionNotFound, sectionKey);
            }

            SaveResult result = ReadEntry(section, entry, out object data);
            if (result.IsSuccess)
            {
                result = RestoreEntry(section, data, entry.Version);
            }

            if (result.IsSuccess)
            {
                OnLoaded?.Invoke(slotId, sectionKey);
            }

            return result;
        }

        public SaveResult Peek<TData>(string slotId, string sectionKey, out TData data, out int savedVersion)
        {
            data = default;
            savedVersion = 0;
            if (!SaveLimits.IsValidSlotId(slotId) || !SaveLimits.IsValidSectionKey(sectionKey))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, sectionKey);
            }

            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (!read.IsSuccess)
            {
                return read;
            }

            SaveSlotFile.Entry entry = file.Find(sectionKey);
            if (entry == null)
            {
                return SaveResult.Failure(SaveFailureReason.SectionNotFound, sectionKey);
            }

            try
            {
                data = (TData)_serializer.Deserialize(entry.Data, typeof(TData));
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SerializationFailed, sectionKey, exception.Message);
            }

            savedVersion = entry.Version;
            return SaveResult.Success;
        }
        #endregion

        #region Slots
        public bool HasSlot(string slotId) => SaveLimits.IsValidSlotId(slotId) && Storage.Exists(slotId);

        public bool TryGetSlotInfo(string slotId, out SaveSlotInfo info)
        {
            info = null;
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return false;
            }

            if (!ReadSlot(slotId, out SaveSlotFile file, out int size).IsSuccess)
            {
                return false;
            }

            info = file.ToInfo(slotId, size);
            return true;
        }

        public void GetSlots(List<SaveSlotInfo> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            var slotIds = new List<string>();
            Storage.GetSlotIds(slotIds);
            foreach (string slotId in slotIds)
            {
                if (TryGetSlotInfo(slotId, out SaveSlotInfo info))
                {
                    results.Add(info);
                }
            }
        }

        public SaveResult DeleteSlot(string slotId)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            bool existed = Storage.Exists(slotId);
            SaveResult result = Storage.Delete(slotId);
            if (result.IsSuccess && existed)
            {
                OnSlotDeleted?.Invoke(slotId);
            }

            return result;
        }

        public SaveResult DeleteSection(string slotId, string sectionKey)
        {
            if (!SaveLimits.IsValidSlotId(slotId) || !SaveLimits.IsValidSectionKey(sectionKey))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, sectionKey);
            }

            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (!read.IsSuccess)
            {
                return read;
            }

            if (!file.Remove(sectionKey))
            {
                return SaveResult.Failure(SaveFailureReason.SectionNotFound, sectionKey);
            }

            file.UpdatedTicks = DateTime.UtcNow.Ticks;
            return WriteSlot(slotId, file);
        }

        public SaveResult ExportSlot(string slotId, out byte[] data)
        {
            data = null;
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            SaveResult read = Storage.Read(slotId, out byte[] bytes);
            if (!read.IsSuccess)
            {
                return read;
            }

            SaveResult parsed = SaveSlotFile.TryParse(bytes, out _);
            if (!parsed.IsSuccess)
            {
                return parsed;
            }

            data = bytes;
            return SaveResult.Success;
        }

        public SaveResult ImportSlot(string slotId, byte[] data)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length > _maxSlotSize)
            {
                return SaveResult.Failure(SaveFailureReason.TooLarge, message: $"{data.Length} bytes is over the {_maxSlotSize}-byte limit.");
            }

            SaveResult parsed = SaveSlotFile.TryParse(data, out _);
            if (!parsed.IsSuccess)
            {
                return parsed;
            }

            bool existed = Storage.Exists(slotId);
            SaveResult written = Storage.Write(slotId, data);
            if (written.IsSuccess && existed)
            {
                OnSlotDeleted?.Invoke(slotId);
            }

            return written;
        }
        #endregion

        #region Helpers
        private SaveResult CaptureEntry(ISaveSection section, long now, out SaveSlotFile.Entry entry)
        {
            entry = null;
            object data;
            try
            {
                data = section.Capture();
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SectionFailed, section.Key, exception.Message);
            }

            byte[] bytes;
            try
            {
                bytes = _serializer.Serialize(data, section.DataType);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SerializationFailed, section.Key, exception.Message);
            }

            if (bytes == null)
            {
                return SaveResult.Failure(SaveFailureReason.SerializationFailed, section.Key, "The serializer wrote no data.");
            }

            entry = new SaveSlotFile.Entry
            {
                Key = section.Key,
                Version = section.Version,
                UpdatedTicks = now,
                Data = bytes,
            };
            return SaveResult.Success;
        }

        private SaveResult ReadEntry(ISaveSection section, SaveSlotFile.Entry entry, out object data)
        {
            data = null;
            if (entry.Version > section.Version)
            {
                return SaveResult.Failure(
                    SaveFailureReason.VersionTooNew,
                    section.Key,
                    $"The slot holds version {entry.Version}; the section reads up to {section.Version}.");
            }

            try
            {
                data = _serializer.Deserialize(entry.Data, section.DataType);
                return SaveResult.Success;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SerializationFailed, section.Key, exception.Message);
            }
        }

        private static SaveResult RestoreEntry(ISaveSection section, object data, int version)
        {
            try
            {
                section.Restore(data, version);
                return SaveResult.Success;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SectionFailed, section.Key, exception.Message);
            }
        }

        private SaveResult ReadSlot(string slotId, out SaveSlotFile file, out int size)
        {
            file = null;
            size = 0;
            SaveResult read = Storage.Read(slotId, out byte[] bytes);
            if (!read.IsSuccess)
            {
                return read;
            }

            size = bytes?.Length ?? 0;
            return SaveSlotFile.TryParse(bytes, out file);
        }

        private SaveResult WriteSlot(string slotId, SaveSlotFile file)
        {
            byte[] bytes = file.ToBytes();
            if (bytes.Length > _maxSlotSize)
            {
                return SaveResult.Failure(SaveFailureReason.TooLarge, message: $"{bytes.Length} bytes is over the {_maxSlotSize}-byte limit.");
            }

            return Storage.Write(slotId, bytes);
        }
        #endregion
    }
}
