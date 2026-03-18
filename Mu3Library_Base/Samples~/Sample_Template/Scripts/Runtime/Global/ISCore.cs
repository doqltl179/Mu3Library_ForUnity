using Mu3Library.DI;
using Mu3Library.IS;
using UnityEngine;

namespace Mu3Library.Sample.Template.Global
{
    public class ISCore : CoreBase
    {
        protected override void ConfigureContainer()
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
            RegisterClass<InputSystemManager>();
#else
            Debug.LogWarning("Input System is not installed.");
#endif
        }
    }
}
