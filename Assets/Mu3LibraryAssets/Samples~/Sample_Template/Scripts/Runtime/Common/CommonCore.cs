#if TEMPLATE_ADDRESSABLES_SUPPORT
using Mu3Library.Addressable;
#endif
#if TEMPLATE_LOCALIZATION_SUPPORT
using Mu3Library.Localization;
#endif
using Mu3Library.Audio;
using Mu3Library.DI;
using Mu3Library.Resource;
using Mu3Library.Scene;
using Mu3Library.UI.MVP;
using Mu3Library.WebRequest;
using UnityEngine;

namespace Mu3Library.Sample.Template.Common
{
    public class CommonCore : CoreBase
    {



        protected override void Awake()
        {
            base.Awake();

#if TEMPLATE_ADDRESSABLES_SUPPORT
            _container.Register<AddressablesManager>();
#else
            Debug.LogWarning("Addressables is not installed.");
#endif

#if TEMPLATE_LOCALIZATION_SUPPORT
            _container.Register<LocalizationManager>();
#else
            Debug.LogWarning("Localization is not installed.");
#endif

            _container.Register<ResourceLoader>();
            _container.Register<AudioManager>();
            _container.Register<SceneLoader>();
            _container.Register<MVPManager>();
            _container.Register<WebRequestManager>();

            DontDestroyOnLoad(gameObject);
        }
    }
}
