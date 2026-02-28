using TMPro;
using UnityEngine;
using System.Collections.Generic;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class InputActionItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private InputActionBindingItem _bindingItemResource;
        private readonly List<InputActionBindingItem> _bindingItems = new();

        public int BindingItemCount => _bindingItems.Count;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        private InputDevice _device;
        public InputDevice Device => _device;

        private InputAction _inputAction;
        public InputAction InputAction => _inputAction;
#endif



        private void Awake()
        {
            _bindingItemResource.gameObject.SetActive(false);
        }

        #region Utility

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        public void SetInputAction(InputDevice device, InputAction inputAction)
        {
            _nameText.text = inputAction.name;

            foreach (var item in _bindingItems)
            {
                Destroy(item.gameObject);
            }
            _bindingItems.Clear();

            foreach (var binding in inputAction.bindings)
            {
                if (string.IsNullOrEmpty(binding.effectivePath) ||
                    !InputControlPath.Matches(binding.effectivePath, device))
                {
                    continue;
                }

                var bindingItem = Instantiate(_bindingItemResource, _bindingItemResource.transform.parent);
                bindingItem.gameObject.SetActive(true);

                bindingItem.SetInputBinding(binding);

                _bindingItems.Add(bindingItem);
            }

            _device = device;
            _inputAction = inputAction;
        }
#endif

        #endregion
    }
}