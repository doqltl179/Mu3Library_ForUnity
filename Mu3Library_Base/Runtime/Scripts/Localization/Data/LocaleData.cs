namespace Mu3Library.Localization.Data
{
    public sealed class LocaleData
    {
        public string Code { get; }
        public string EnglishName { get; }
        public string NativeName { get; }

        public LocaleData(string code, string englishName, string nativeName)
        {
            Code = code;
            EnglishName = englishName;
            NativeName = nativeName;
        }
    }
}