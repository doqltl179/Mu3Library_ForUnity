namespace Mu3Library.Resource.Data
{
    public sealed class ResourcePathData
    {
        public string Path { get; }
        public string Name { get; }

        public ResourcePathData(string path, string name)
        {
            Path = path;
            Name = name;
        }
    }
}
