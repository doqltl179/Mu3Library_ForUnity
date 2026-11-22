using System;

namespace Mu3Library.Steam.Stats
{
    /// <summary>One achievement of the app as Steam holds it for this player.</summary>
    public readonly struct SteamAchievement
    {
        /// <summary>The API name the Steamworks partner site gives the achievement.</summary>
        public string Id { get; }

        /// <summary>The name players see, in the language Steam runs in.</summary>
        public string DisplayName { get; }

        public string Description { get; }

        /// <summary>True for an achievement the partner site hides until it is unlocked.</summary>
        public bool IsHidden { get; }

        public bool IsUnlocked { get; }

        /// <summary>When this player unlocked it, in UTC, or null while it is locked.</summary>
        public DateTime? UnlockTimeUtc { get; }



        public SteamAchievement(string id, string displayName, string description, bool isHidden, bool isUnlocked, DateTime? unlockTimeUtc)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            IsHidden = isHidden;
            IsUnlocked = isUnlocked;
            UnlockTimeUtc = unlockTimeUtc;
        }

        public override string ToString() => $"{Id} ({(IsUnlocked ? "unlocked" : "locked")})";
    }
}
