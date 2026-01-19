using Mu3Library.DI;
using Mu3Library.Resource;
using UnityEngine;
#if TEMPLATE_ADDRESSABLES_SUPPORT
using Mu3Library.Addressable;
#endif

namespace Mu3Library.Sample.Template.Global
{
    public class ResourceCore : CoreBase
    {
        protected override void ConfigureContainer(ContainerScope scope)
        {
#if TEMPLATE_ADDRESSABLES_SUPPORT
            scope.Register<AddressablesManager>();
#else
            Debug.LogWarning("Addressables is not installed.");
#endif

            scope.Register<ResourceLoader>();
        }
    }
}
