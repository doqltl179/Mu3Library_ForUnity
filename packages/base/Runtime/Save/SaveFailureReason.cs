namespace Mu3Library.Save
{
    public enum SaveFailureReason
    {
        None = 0,

        /// <summary>A slot ID or section key is empty, too long, or holds a character it may not.</summary>
        InvalidKey,
        /// <summary>The section is not registered with the manager.</summary>
        SectionNotRegistered,
        /// <summary>A section with the same key is already registered.</summary>
        SectionAlreadyRegistered,
        /// <summary>The storage holds no slot with this ID.</summary>
        SlotNotFound,
        /// <summary>The slot holds nothing for this section.</summary>
        SectionNotFound,
        /// <summary>The slot's data is damaged or is not a save: its check failed or it could not be read.</summary>
        Corrupted,
        /// <summary>The slot was written by a newer format, or the section by a newer data version.</summary>
        VersionTooNew,
        /// <summary>The serializer could not write or read the section's data.</summary>
        SerializationFailed,
        /// <summary>The section's own capture, restore, or migration threw.</summary>
        SectionFailed,
        /// <summary>The slot would exceed <see cref="SaveManager.MaxSlotSize"/>.</summary>
        TooLarge,
        /// <summary>The storage could not read, write, or delete.</summary>
        StorageFailed,
    }
}
