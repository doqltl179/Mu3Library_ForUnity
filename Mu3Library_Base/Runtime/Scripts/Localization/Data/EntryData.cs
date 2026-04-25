namespace Mu3Library.Localization.Data
{
    public sealed class EntryData
    {
        public string TableName { get; }
        public string Key { get; }
        public string Id { get; }

        public EntryData(string tableName, string key, string id)
        {
            TableName = tableName;
            Key = key;
            Id = id;
        }
    }
}