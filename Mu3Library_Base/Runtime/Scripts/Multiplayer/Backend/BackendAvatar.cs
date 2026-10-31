namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// An avatar image as a backend read it: RGBA32 pixels, four bytes each, in rows from the top
    /// row down, as Steam hands them over. The manager turns it into a texture.
    /// </summary>
    public readonly struct BackendAvatar
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// <see cref="Width"/> times <see cref="Height"/> times 4 bytes. The backend hands the array
        /// over: it neither keeps nor reuses it, and the manager may change it.
        /// </summary>
        public byte[] Pixels { get; }

        public BackendAvatar(int width, int height, byte[] pixels)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
        }
    }
}
