// Part of the DI stripping harness (tools/README.md#di-stripping-harness): a pure-domain assembly
// that does not reference Mu3Library.DI. Assembly-CSharp registers these classes; nothing creates
// them with `new`, so only the link.xml's constructor entries keep them in a stripped player.
namespace StripHarness.Domain
{
    public interface IRepo
    {
        int Count { get; }
    }

    public sealed class Repo : IRepo
    {
        public int Count => 3;
    }

    public interface IScoreService
    {
        IRepo Repo { get; }
    }

    public sealed class ScoreService : IScoreService
    {
        public ScoreService(IRepo repo)
        {
            Repo = repo;
        }

        public IRepo Repo { get; }
    }
}
