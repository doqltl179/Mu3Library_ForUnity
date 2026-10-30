namespace Mu3Library.Multiplayer.Data
{
    public readonly struct LobbyNumericFilter
    {
        public string Key { get; }
        public int Value { get; }
        public LobbyComparison Comparison { get; }

        public LobbyNumericFilter(string key, int value, LobbyComparison comparison)
        {
            Key = key;
            Value = value;
            Comparison = comparison;
        }
    }
}
