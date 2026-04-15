using System.Collections.Generic;
using Mu3Library.Localization.Data;

namespace Mu3Library.Sample.Template.Localization
{
    public static class LocalizationTableKeys
    {
        public static class Locales
        {
            public static readonly LocaleData En = new LocaleData("en", "English", "English");
            public static readonly LocaleData Ja = new LocaleData("ja", "Japanese", "日本語");
            public static readonly LocaleData Ko = new LocaleData("ko", "Korean", "한국어");
            
            public static readonly IReadOnlyDictionary<string, LocaleData> All = new Dictionary<string, LocaleData>
            {
                { En.Code, En },
                { Ja.Code, Ja },
                { Ko.Code, Ko },
            };
        }
        

        public static class Tables
        {
            public static readonly TestStringTableData TestStringTable = new TestStringTableData();
            
            public static readonly IReadOnlyDictionary<string, TableData> All = new Dictionary<string, TableData>
            {
                { TestStringTable.Name, TestStringTable },
            };

            public sealed class TestStringTableData : TableData
            {
                public new static class Locales
                {
                    public static readonly LocaleData En = LocalizationTableKeys.Locales.En;
                    public static readonly LocaleData Ja = LocalizationTableKeys.Locales.Ja;
                    public static readonly LocaleData Ko = LocalizationTableKeys.Locales.Ko;
                }
                
                public static readonly EntryData Hello = new EntryData("TestStringTable", "hello", "909386731520");
                public static readonly EntryData Test02Test = new EntryData("TestStringTable", "test-02-test", "37107648355389440");
                
                internal TestStringTableData() : base(
                    "TestStringTable",
                    new Dictionary<string, LocaleData>
                    {
                        { Locales.En.Code, Locales.En },
                        { Locales.Ja.Code, Locales.Ja },
                        { Locales.Ko.Code, Locales.Ko },
                    },
                    new Dictionary<string, EntryData>
                    {
                        { Hello.Key, Hello },
                        { Test02Test.Key, Test02Test },
                    })
                { }
            }
        }
    }
}
