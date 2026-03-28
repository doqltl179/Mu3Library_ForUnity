#if MU3LIBRARY_ADDRESSABLES_SUPPORT

namespace Mu3Library.Addressable.Data
{
    public sealed class LabelData
    {
        public string Value { get; }

        public LabelData(string value)
        {
            Value = value;
        }
    }
}

#endif