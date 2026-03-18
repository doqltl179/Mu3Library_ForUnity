using UnityEngine;
using UnityEngine.UI;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class DeviceTab : Tab
    {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
        private InputDevice _device;
        public InputDevice Device => _device;
#endif



        #region Utility

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        public void SetDevice(InputDevice device)
        {
            SetTabName(device.name);

            _device = device;
        }
#endif

        #endregion
    }
}
