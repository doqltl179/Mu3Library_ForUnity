using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.DI;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Save;
using UnityEngine;

namespace Mu3Library.SessionSave
{
    /// <summary>
    /// Saves a multiplayer session and shares the save with its players, on the public message API
    /// of a multiplayer manager and an <see cref="ISaveManager"/>. See <see cref="ISessionSaveManager"/>.
    /// </summary>
    /// <remarks>
    /// The share uses two game message types, <c>messageType</c> for what the host sends and
    /// <c>messageType + 1</c> for what the receiving player answers, which the manager registers
    /// when it is created and unregisters when it is disposed; every player creates its manager
    /// with the same <c>messageType</c>, and the game registers no other message there.
    /// </remarks>
    public partial class SessionSaveManager : ISessionSaveManager, ISessionSaveManagerEventBus, IUpdatable, IDisposable
    {
        public const int DefaultShareChunkSize = 16 * 1024;
        public const int DefaultShareBytesPerSecond = 128 * 1024;
        public const float DefaultShareTimeoutSeconds = 60f;

        private readonly IMultiplayerManager _session;
        private readonly IMultiplayerMessenger _messenger;
        private readonly Dictionary<string, IPlayerSaveSection> _playerSections = new();
        private readonly List<IPlayerSaveSection> _playerSectionOrder = new();
        private readonly List<string> _registeredAdapters = new();
        private bool _isDisposed;

        public ISaveManager Saves { get; }

        /// <summary>
        /// Seconds since an arbitrary start, for pacing and timeouts. Null uses
        /// <c>Time.realtimeSinceStartupAsDouble</c>; tests hand in their own clock.
        /// </summary>
        public Func<double> Clock { get; set; }

        private int _shareChunkSize = DefaultShareChunkSize;
        public int ShareChunkSize
        {
            get => _shareChunkSize;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The chunk size must be positive.");
                }

                _shareChunkSize = value;
            }
        }

        private int _shareBytesPerSecond = DefaultShareBytesPerSecond;
        public int ShareBytesPerSecond
        {
            get => _shareBytesPerSecond;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The pace must be positive.");
                }

                _shareBytesPerSecond = value;
            }
        }

        private float _shareTimeoutSeconds = DefaultShareTimeoutSeconds;
        public float ShareTimeoutSeconds
        {
            get => _shareTimeoutSeconds;
            set
            {
                if (!(value > 0f))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive.");
                }

                _shareTimeoutSeconds = value;
            }
        }

        public event Action<SaveShareOffer> OnShareOffered;
        public event Action<SaveShareTransfer> OnShareProgress;
        public event Action<SaveShareTransfer> OnShareCompleted;
        public event Action<SaveShareTransfer, SaveShareFailureReason> OnShareFailed;

        private double Now => Clock != null ? Clock() : Time.realtimeSinceStartupAsDouble;

        private bool IsHosting =>
            _session.IsHost && (_session.State == SessionState.Lobby || _session.State == SessionState.InGame);



        public SessionSaveManager(MultiplayerManager multiplayer, ISaveManager saves, ushort messageType)
            : this(multiplayer, multiplayer, saves, messageType)
        {
        }

        /// <param name="session">The session to save, usually the same manager as <paramref name="messenger"/>.</param>
        /// <param name="messenger">The messages of that session.</param>
        /// <param name="saves">The save manager the session saves through.</param>
        /// <param name="messageType">
        /// The first of the two message types the share uses, below <see cref="ushort.MaxValue"/>.
        /// </param>
        /// <exception cref="InvalidOperationException">Either message type is already registered.</exception>
        public SessionSaveManager(IMultiplayerManager session, IMultiplayerMessenger messenger, ISaveManager saves, ushort messageType)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            Saves = saves ?? throw new ArgumentNullException(nameof(saves));
            if (messageType == ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "The share needs this message type and the next one.");
            }

            _hostMessageType = messageType;
            _playerMessageType = (ushort)(messageType + 1);
            RegisterMessages();
        }

        #region Player Sections
        public SaveResult RegisterPlayerSection(IPlayerSaveSection section)
        {
            ThrowIfDisposed();
            if (section == null)
            {
                throw new ArgumentNullException(nameof(section));
            }

            string key = section.Key;
            if (!SaveLimits.IsValidSectionKey(key) || key.Length > SessionSaveKeys.MaxPlayerSectionKeyLength)
            {
                return SaveResult.Failure(SaveFailureReason.InvalidKey, key);
            }

            if (section.Version < 1 || section.DataType == null)
            {
                return SaveResult.Failure(SaveFailureReason.InvalidSection, key, "A section needs a version of 1 or more and a data type.");
            }

            if (_playerSections.ContainsKey(key))
            {
                return SaveResult.Failure(SaveFailureReason.SectionAlreadyRegistered, key);
            }

            _playerSections.Add(key, section);
            _playerSectionOrder.Add(section);
            return SaveResult.Success;
        }

        public bool UnregisterPlayerSection(string key)
        {
            ThrowIfDisposed();
            if (key == null || !_playerSections.TryGetValue(key, out IPlayerSaveSection section))
            {
                return false;
            }

            _playerSections.Remove(key);
            _playerSectionOrder.Remove(section);
            return true;
        }

        public bool IsPlayerSectionRegistered(string key) => key != null && _playerSections.ContainsKey(key);
        #endregion

        #region Save And Load
        public SessionSaveResult Save(string slotId, string sessionName = null)
        {
            ThrowIfDisposed();
            if (!IsHosting)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.NotHost);
            }

            if (sessionName != null && sessionName.Length > SaveLimits.MaxMetadataValueLength)
            {
                return SessionSaveResult.FromSave(SaveResult.Failure(
                    SaveFailureReason.TooLarge,
                    SessionSaveKeys.SessionName,
                    $"A session name holds at most {SaveLimits.MaxMetadataValueLength} characters."));
            }

            SaveResult saved = RegisterAdapters(_session.Participants);
            try
            {
                if (saved.IsSuccess)
                {
                    saved = Saves.Save(slotId);
                }
            }
            finally
            {
                UnregisterAdapters();
            }

            if (!saved.IsSuccess)
            {
                return SessionSaveResult.FromSave(saved);
            }

            _session.TryGetParticipant(_session.LocalPlayerId, out Participant host);
            return SessionSaveResult.FromSave(WriteSessionMetadata(slotId, host.Name, sessionName));
        }

        public SessionSaveResult Load(string slotId)
        {
            ThrowIfDisposed();
            if (!IsHosting)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.NotHost);
            }

            SaveResult loaded = RegisterAdapters(_session.Participants);
            try
            {
                if (loaded.IsSuccess)
                {
                    loaded = Saves.Load(slotId);
                }
            }
            finally
            {
                UnregisterAdapters();
            }

            return SessionSaveResult.FromSave(loaded);
        }

        public SessionSaveResult LoadPlayer(string slotId, PlayerId player)
        {
            ThrowIfDisposed();
            if (!IsHosting)
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.NotHost);
            }

            if (!_session.TryGetParticipant(player, out _))
            {
                return SessionSaveResult.Failure(SessionSaveFailureReason.PlayerNotInSession);
            }

            if (!SaveLimits.IsValidSlotId(slotId))
            {
                return SessionSaveResult.FromSave(SaveResult.Failure(SaveFailureReason.InvalidKey));
            }

            if (!Saves.HasSlot(slotId))
            {
                return SessionSaveResult.FromSave(SaveResult.Failure(SaveFailureReason.SlotNotFound));
            }

            foreach (IPlayerSaveSection section in _playerSectionOrder.ToArray())
            {
                var adapter = new PlayerSectionAdapter(section, player);
                SaveResult loaded = Saves.RegisterSection(adapter);
                if (!loaded.IsSuccess)
                {
                    return SessionSaveResult.FromSave(loaded);
                }

                try
                {
                    loaded = Saves.Load(slotId, adapter.Key);
                }
                finally
                {
                    Saves.UnregisterSection(adapter.Key);
                }

                if (loaded.Reason == SaveFailureReason.SectionNotFound)
                {
                    loaded = RestoreDefault(section, player, adapter.Key);
                }

                if (!loaded.IsSuccess)
                {
                    return SessionSaveResult.FromSave(loaded);
                }
            }

            return SessionSaveResult.Success;
        }

        public bool TryGetSessionInfo(string slotId, out SessionSaveInfo info)
        {
            ThrowIfDisposed();
            info = null;
            return Saves.TryGetSlotInfo(slotId, out SaveSlotInfo slot) && SessionSaveInfo.TryCreate(slot, out info);
        }

        private SaveResult RegisterAdapters(IReadOnlyList<Participant> participants)
        {
            foreach (Participant participant in participants)
            {
                foreach (IPlayerSaveSection section in _playerSectionOrder)
                {
                    var adapter = new PlayerSectionAdapter(section, participant.Id);
                    SaveResult registered = Saves.RegisterSection(adapter);
                    if (!registered.IsSuccess)
                    {
                        return registered;
                    }

                    _registeredAdapters.Add(adapter.Key);
                }
            }

            return SaveResult.Success;
        }

        private void UnregisterAdapters()
        {
            foreach (string key in _registeredAdapters)
            {
                Saves.UnregisterSection(key);
            }

            _registeredAdapters.Clear();
        }

        // Writes only the values that changed, since each one rewrites the slot.
        private SaveResult WriteSessionMetadata(string slotId, string hostName, string sessionName)
        {
            Saves.TryGetSlotInfo(slotId, out SaveSlotInfo slot);
            string hostId = _session.LocalPlayerId.Value.ToString(CultureInfo.InvariantCulture);

            SaveResult result = WriteMetadata(slot, slotId, SessionSaveKeys.HostId, hostId);
            if (result.IsSuccess)
            {
                result = WriteMetadata(slot, slotId, SessionSaveKeys.HostName, hostName ?? string.Empty);
            }

            if (result.IsSuccess && sessionName != null)
            {
                result = WriteMetadata(slot, slotId, SessionSaveKeys.SessionName, sessionName);
            }

            return result;
        }

        private SaveResult WriteMetadata(SaveSlotInfo slot, string slotId, string key, string value)
        {
            if (slot != null && slot.TryGetMetadata(key, out string current) && current == value)
            {
                return SaveResult.Success;
            }

            return Saves.SetMetadata(slotId, key, value);
        }

        private static SaveResult RestoreDefault(IPlayerSaveSection section, PlayerId player, string key)
        {
            try
            {
                section.RestoreDefault(player);
                return SaveResult.Success;
            }
            catch (Exception exception)
            {
                return SaveResult.Failure(SaveFailureReason.SectionFailed, key, exception.Message);
            }
        }
        #endregion

        #region Lifetime
        /// <summary>
        /// Paces the shares this player sends and ends a share whose other player stopped
        /// answering, left, or whose session ended. Call it every frame.
        /// </summary>
        public void Update()
        {
            if (_isDisposed)
            {
                return;
            }

            double now = Now;
            UpdateOutgoing(now);
            UpdateIncoming(now);
        }

        /// <summary>
        /// Cancels the running shares, telling the other players where a message still can, without
        /// raising events, and unregisters the share's message types.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            CancelAllShares();
            _messenger.UnregisterMessage(_hostMessageType);
            _messenger.UnregisterMessage(_playerMessageType);
            _isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SessionSaveManager));
            }
        }
        #endregion



        /// <summary>One player's part of a player section, as the save manager sees it.</summary>
        private sealed class PlayerSectionAdapter : ISaveSection
        {
            private readonly IPlayerSaveSection _section;
            private readonly PlayerId _player;

            public string Key { get; }
            public int Version => _section.Version;
            public Type DataType => _section.DataType;



            public PlayerSectionAdapter(IPlayerSaveSection section, PlayerId player)
            {
                _section = section;
                _player = player;
                Key = SessionSaveKeys.PlayerSectionKey(player, section.Key);
            }

            public object Capture() => _section.Capture(_player);
            public object Migrate(object data, int savedVersion) => _section.Migrate(data, savedVersion);
            public void Restore(object data) => _section.Restore(_player, data);
            public void RestoreDefault() => _section.RestoreDefault(_player);
        }
    }
}
