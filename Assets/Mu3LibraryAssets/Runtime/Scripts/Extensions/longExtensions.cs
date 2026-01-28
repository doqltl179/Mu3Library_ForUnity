namespace Mu3Library.Extensions
{
    public static class longExtensions
    {
        public static double BytesToKB(this long value)
        {
            return value / 1024f;
        }

        public static double BytesToMB(this long value)
        {
            return value / (1024f * 1024f);
        }

        public static double BytesToGB(this long value)
        {
            return value / (1024f * 1024f * 1024f);
        }
    }
}
