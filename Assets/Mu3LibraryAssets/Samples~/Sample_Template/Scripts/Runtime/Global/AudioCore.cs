using Mu3Library.Audio;
using Mu3Library.DI;

namespace Mu3Library.Sample.Template.Global
{
    public class AudioCore : CoreBase
    {
        protected override void Awake()
        {
            base.Awake();

            _container.Register<AudioManager>();
        }
    }
}
