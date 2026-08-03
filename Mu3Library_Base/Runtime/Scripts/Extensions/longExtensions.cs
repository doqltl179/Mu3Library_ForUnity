namespace Mu3Library.Extensions
{
    public static class longExtensions
    {
        private const float BytesPerKilobyte = 1024f;
        private const float BytesPerMegabyte = BytesPerKilobyte * BytesPerKilobyte;
        private const float BytesPerGigabyte = BytesPerMegabyte * BytesPerKilobyte;

        public static double BytesToKB(this long value)
        {
            return value / BytesPerKilobyte;
        }

        public static double BytesToMB(this long value)
        {
            return value / BytesPerMegabyte;
        }

        public static double BytesToGB(this long value)
        {
            return value / BytesPerGigabyte;
        }
    }
}
