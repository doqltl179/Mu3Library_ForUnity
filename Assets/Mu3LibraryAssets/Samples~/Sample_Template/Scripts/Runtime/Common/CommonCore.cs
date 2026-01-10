using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.Scene;

namespace Mu3Library.Sample.Template.Common
{
    public class CommonCore : CoreBase
    {



        protected override void Awake()
        {
            base.Awake();

            _container.Register<AudioManager>();
            _container.Register<SceneLoader>();

            DontDestroyOnLoad(gameObject);
        }
    }
}
