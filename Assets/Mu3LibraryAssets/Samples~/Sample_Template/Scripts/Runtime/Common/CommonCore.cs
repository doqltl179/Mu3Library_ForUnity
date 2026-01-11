using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.Localization;
using Mu3Library.Resource;
using Mu3Library.Scene;
using Mu3Library.UI.MVP;

namespace Mu3Library.Sample.Template.Common
{
    public class CommonCore : CoreBase
    {



        protected override void Awake()
        {
            base.Awake();

            _container.Register<AudioManager>();
            _container.Register<SceneLoader>();
            _container.Register<MVPManager>();
            _container.Register<ResourceLoader>();
            _container.Register<LocalizationManager>();

            DontDestroyOnLoad(gameObject);
        }
    }
}
