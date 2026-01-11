using Mu3Library.Addressable;
using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.Localization;
using Mu3Library.Resource;
using Mu3Library.Scene;
using Mu3Library.UI.MVP;
using Mu3Library.WebRequest;

namespace Mu3Library.Sample.Template.Common
{
    public class CommonCore : CoreBase
    {



        protected override void Awake()
        {
            base.Awake();

            _container.Register<AddressablesManager>();
            _container.Register<LocalizationManager>();
            _container.Register<ResourceLoader>();
            _container.Register<AudioManager>();
            _container.Register<SceneLoader>();
            _container.Register<MVPManager>();
            _container.Register<WebRequestManager>();

            DontDestroyOnLoad(gameObject);
        }
    }
}
