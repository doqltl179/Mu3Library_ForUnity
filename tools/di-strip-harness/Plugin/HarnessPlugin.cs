// The precompiled assembly of the DI stripping harness (tools/README.md#di-stripping-harness).
// mu3-cli unity di-strip-harness compiles it with the Editor's own Mono compiler into
// Assets/Plugins/HarnessPlugin.dll of the mirror project. It references nothing but mscorlib,
// so it knows nothing of Mu3Library.DI; Assembly-CSharp registers its classes and nothing
// creates them with `new`.
namespace StripHarness.Plugin
{
    public interface IPluginRepo
    {
        int Count { get; }
    }

    public sealed class PluginRepo : IPluginRepo
    {
        public int Count
        {
            get { return 5; }
        }
    }

    public interface IPluginService
    {
        IPluginRepo Repo { get; }
    }

    public sealed class PluginService : IPluginService
    {
        private readonly IPluginRepo _repo;

        public PluginService(IPluginRepo repo)
        {
            _repo = repo;
        }

        public IPluginRepo Repo
        {
            get { return _repo; }
        }
    }
}
