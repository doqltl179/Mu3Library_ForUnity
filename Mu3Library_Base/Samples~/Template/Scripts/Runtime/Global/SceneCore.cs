using Mu3Library.DI;
using Mu3Library.Scene;

namespace Mu3Library.Sample.Template.Global
{
    public class SceneCore : CoreBase
    {
        protected override void ConfigureContainer()
        {
            RegisterClass<SceneLoader>();
        }
    }
}
