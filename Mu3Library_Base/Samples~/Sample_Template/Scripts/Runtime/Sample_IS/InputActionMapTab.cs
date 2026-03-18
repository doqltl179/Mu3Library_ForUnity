using UnityEngine;
using UnityEngine.UI;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class InputActionMapTab : Tab
    {
#if TEMPLATE_INPUTSYSTEM_SUPPORT
        private InputActionMap _inputActionMap;
        public InputActionMap InputActionMap => _inputActionMap;
#endif



        #region Utility

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        public void SetInputActionMap(InputActionMap inputActionMap)
        {
            SetTabName(inputActionMap.name);

            _inputActionMap = inputActionMap;
        }
#endif

        #endregion
    }
}
