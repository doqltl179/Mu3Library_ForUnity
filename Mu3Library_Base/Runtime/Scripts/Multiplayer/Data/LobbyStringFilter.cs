namespace Mu3Library.Multiplayer.Data
{
    public readonly struct LobbyStringFilter
    {
        public string Key { get; }
        public string Value { get; }
        public LobbyComparison Comparison { get; }

        public LobbyStringFilter(string key, string value, LobbyComparison comparison)
        {
            Key = key;
            Value = value;
            Comparison = comparison;
        }
    }
}
