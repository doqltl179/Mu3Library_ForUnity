using System.Collections.Generic;
using UnityEngine;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class InputActionPage : MonoBehaviour
    {
        [SerializeField] private InputActionItem _inputActionItemResource;
        private readonly List<InputActionItem> _inputActionItems = new();



        private void Awake()
        {
            _inputActionItemResource.gameObject.SetActive(false);
        }

        #region Utility

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        public void AddInputAction(InputDevice device, InputAction inputAction)
        {
            var inputActionItem = Instantiate(_inputActionItemResource, _inputActionItemResource.transform.parent);
            inputActionItem.gameObject.SetActive(true);

            inputActionItem.SetInputAction(device, inputAction);

            if (inputActionItem.BindingItemCount == 0)
            {
                Destroy(inputActionItem.gameObject);
                return;
            }

            _inputActionItems.Add(inputActionItem);
        }
#endif

        #endregion
    }
}