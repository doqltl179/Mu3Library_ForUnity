namespace Mu3Library.Sample.Template.Localization
{
    public static class LocalizationTableKeys
    {
        public static class Locales
        {
            public static readonly string[] All = new string[] { En.Code, Ja.Code, Ko.Code };
            
            public static class En
            {
                public const string Code = "en";
                public const string EnglishName = "English";
                public const string NativeName = "English";
            }

            public static class Ja
            {
                public const string Code = "ja";
                public const string EnglishName = "Japanese";
                public const string NativeName = "日本語";
            }

            public static class Ko
            {
                public const string Code = "ko";
                public const string EnglishName = "Korean";
                public const string NativeName = "한국어";
            }
        }
        

        public static class Tables
        {
            public static readonly string[] All = new string[] { TestStringTable };
            
            public const string TestStringTable = LocalizationTableKeys.TestStringTable.Name;
        }
        

        public static class TestStringTable
        {
            public const string Name = "TestStringTable";
            
            public static class Locales
            {
                public static readonly string[] All = new string[] { En.Code, Ja.Code, Ko.Code };
                
                public static class En
                {
                    public const string Code = LocalizationTableKeys.Locales.En.Code;
                    public const string EnglishName = LocalizationTableKeys.Locales.En.EnglishName;
                    public const string NativeName = LocalizationTableKeys.Locales.En.NativeName;
                }

                public static class Ja
                {
                    public const string Code = LocalizationTableKeys.Locales.Ja.Code;
                    public const string EnglishName = LocalizationTableKeys.Locales.Ja.EnglishName;
                    public const string NativeName = LocalizationTableKeys.Locales.Ja.NativeName;
                }

                public static class Ko
                {
                    public const string Code = LocalizationTableKeys.Locales.Ko.Code;
                    public const string EnglishName = LocalizationTableKeys.Locales.Ko.EnglishName;
                    public const string NativeName = LocalizationTableKeys.Locales.Ko.NativeName;
                }
            }
            

            public static class Hello
            {
                public const string Key = "hello";
                public const string Id = "909386731520";
            }

            public static class Test02Test
            {
                public const string Key = "test-02-test";
                public const string Id = "37107648355389440";
            }
        }
    }
}
