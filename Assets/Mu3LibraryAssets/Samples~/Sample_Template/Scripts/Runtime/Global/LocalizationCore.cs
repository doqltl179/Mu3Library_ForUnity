using Mu3Library.DI;
using UnityEngine;
#if TEMPLATE_LOCALIZATION_SUPPORT
using Mu3Library.Localization;
#endif

namespace Mu3Library.Sample.Template.Global
{
    public class LocalizationCore : CoreBase
    {
        protected override void Awake()
        {
            base.Awake();

#if TEMPLATE_LOCALIZATION_SUPPORT
            Register<LocalizationManager>();
#else
            Debug.LogWarning("Localization is not installed.");
#endif
        }
    }
}
