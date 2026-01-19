using Mu3Library.Audio;
using Mu3Library.DI;

namespace Mu3Library.Sample.Template.Global
{
    public class AudioCore : CoreBase
    {
        protected override void ConfigureContainer(ContainerScope scope)
        {
            scope.Register<AudioManager>();
        }
    }
}
