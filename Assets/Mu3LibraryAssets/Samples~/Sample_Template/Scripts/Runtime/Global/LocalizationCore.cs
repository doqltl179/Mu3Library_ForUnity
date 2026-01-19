using Mu3Library.DI;
using UnityEngine;
#if TEMPLATE_LOCALIZATION_SUPPORT
using Mu3Library.Localization;
#endif

namespace Mu3Library.Sample.Template.Global
{
    public class LocalizationCore : CoreBase
    {
        protected override void ConfigureContainer(ContainerScope scope)
        {
#if TEMPLATE_LOCALIZATION_SUPPORT
            scope.Register<LocalizationManager>();
#else
            Debug.LogWarning("Localization is not installed.");
#endif
        }
    }
}
