using Mu3Library.DI;
using UnityEngine;
#if TEMPLATE_LOCALIZATION_SUPPORT
using Mu3Library.Localization;
#endif

namespace Mu3Library.Sample.Template.Global
{
    public class LocalizationCore : CoreBase
    {
        protected override void ConfigureContainer()
        {
#if TEMPLATE_LOCALIZATION_SUPPORT
            RegisterClass<LocalizationManager>();
#else
            Debug.LogWarning("Localization is not installed.");
#endif
        }
    }
}
