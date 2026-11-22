using System;
using System.Collections.Generic;
using System.Text;

namespace Mu3Library.Save
{
    /// <summary>
    /// Saves and loads registered sections into slots, each slot one block of bytes in an
    /// <see cref="ISaveStorage"/>. See <see cref="ISaveManager"/>.
    /// </summary>
    public class SaveManager : ISaveManager, ISaveManagerEventBus
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

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
        public event Action<string> OnSlotImported;



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
                return SaveResult.Failure(SaveFailureReason.InvalidSection, key, "A section needs a version of 1 or more and a data type.");
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

            // A damaged slot cannot keep what it held, so a whole save writes it anew.
            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (read.Reason == SaveFailureReason.SlotNotFound || read.Reason == SaveFailureReason.Corrupted)
            {
                file = new SaveSlotFile { CreatedTicks = now };
            }
            else if (!read.IsSuccess)
            {
                return read;
            }

            foreach (ISaveSection section in _sectionOrder)
            {
                SaveResult newer = RefuseNewerVersion(file, section);
                if (!newer.IsSuccess)
                {
                    return newer;
                }
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
            SaveResult valid = CheckKeys(slotId, sectionKey);
            if (!valid.IsSuccess)
            {
                return valid;
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

            SaveResult newer = RefuseNewerVersion(file, section);
            if (!newer.IsSuccess)
            {
                return newer;
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

        public SaveResult SetMetadata(string slotId, string key, string value)
        {
            SaveResult valid = CheckKeys(slotId, key);
            if (!valid.IsSuccess)
            {
                return valid;
            }

            if (value != null && value.Length > SaveLimits.MaxMetadataValueLength)
            {
                return SaveResult.Failure(SaveFailureReason.TooLarge, key,
                    $"A metadata value holds up to {SaveLimits.MaxMetadataValueLength} characters.");
            }

            if (value != null && !CanEncode(value))
            {
                return SaveResult.Failure(SaveFailureReason.SerializationFailed, key,
                    "The value holds half of a character pair, such as a string cut in the middle of an emoji.");
            }

            SaveResult read = ReadSlot(slotId, out SaveSlotFile file, out _);
            if (!read.IsSuccess)
            {
                return read;
            }

            if (value == null)
            {
                if (!file.Metadata.Remove(key))
                {
                    return SaveResult.Success;
                }
            }
            else
            {
                if (!file.Metadata.ContainsKey(key) && file.Metadata.Count >= SaveLimits.MaxMetadataEntries)
                {
                    return SaveResult.Failure(SaveFailureReason.TooLarge, key,
                        $"A slot holds up to {SaveLimits.MaxMetadataEntries} metadata values.");
                }

                file.Metadata[key] = value;
            }

            file.UpdatedTicks = DateTime.UtcNow.Ticks;
            return WriteSlot(slotId, file);
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

            // Read, check, and migrate every section first, so one that fails restores none of them.
            var pending = new List<(ISaveSection Section, object Data)>(_sectionOrder.Count);
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

                pending.Add((section, data));
            }

            foreach ((ISaveSection section, object data) in pending)
            {
                SaveResult result = Run(section, () => section.Restore(data));
                if (!result.IsSuccess)
                {
                    return result;
                }
            }

            foreach (ISaveSection section in missing)
            {
                SaveResult result = Run(section, section.RestoreDefault);
                if (!result.IsSuccess)
                {
                    return result;
                }
            }

            OnLoaded?.Invoke(slotId, null);
            return SaveResult.Success;
        }

        public SaveResult Load(string slotId, string sectionKey)
        {
            SaveResult valid = CheckKeys(slotId, sectionKey);
            if (!valid.IsSuccess)
            {
                return valid;
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
                result = Run(section, () => section.Restore(data));
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
            SaveResult valid = CheckKeys(slotId, sectionKey);
            if (!valid.IsSuccess)
            {
                return valid;
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
            SaveResult valid = CheckKeys(slotId, sectionKey);
            if (!valid.IsSuccess)
            {
                return valid;
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

            SaveResult written = Storage.Write(slotId, data);
            if (written.IsSuccess)
            {
                OnSlotImported?.Invoke(slotId);
            }

            return written;
        }
        #endregion

        #region Helpers
        // A bad slot ID names no section; a bad section key does.
        private static SaveResult CheckKeys(string slotId, string sectionKey)
        {
            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey);
            }

            if (!SaveLimits.IsValidSectionKey(sectionKey))
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, sectionKey);
            }

            return SaveResult.Success;
        }

        private static bool CanEncode(string value)
        {
            try
            {
                StrictUtf8.GetByteCount(value);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        private static SaveResult RefuseNewerVersion(SaveSlotFile file, ISaveSection section)
        {
            SaveSlotFile.Entry existing = file.Find(section.Key);
            if (existing != null && existing.Version > section.Version)
            {
                return SaveResult.Failure(
                    SaveFailureReason.VersionTooNew,
                    section.Key,
                    $"The slot holds version {existing.Version}; this build writes {section.Version} and would lose it.");
            }

            return SaveResult.Success;
        }

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

        // Checks the version, deserializes, and migrates, all without touching the game.
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

            object read;
            try
            {
                read = _serializer.Deserialize(entry.Data, section.DataType);
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SerializationFailed, section.Key, exception.Message);
            }

            try
            {
                data = section.Migrate(read, entry.Version);
                return SaveResult.Success;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SectionFailed, section.Key, exception.Message);
            }
        }

        private static SaveResult Run(ISaveSection section, Action action)
        {
            try
            {
                action();
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
