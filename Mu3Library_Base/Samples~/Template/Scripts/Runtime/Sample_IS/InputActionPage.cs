using System;
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

        public int InputActionItemCount => _inputActionItems.Count;

        public event Action<InputActionItem, InputActionBindingItem> OnBindingItemClicked;



        private void Awake()
        {
            _inputActionItemResource.gameObject.SetActive(false);
        }

        #region Utility

        public void Patch()
        {
            foreach (var item in _inputActionItems)
            {
                item.Patch();
            }
        }

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

            inputActionItem.OnBindingItemClicked += OnBindingItemClickedEvent;

            _inputActionItems.Add(inputActionItem);
        }
#endif

        #endregion

        private void OnBindingItemClickedEvent(InputActionItem inputActionItem, InputActionBindingItem bindingItem)
        {
            OnBindingItemClicked?.Invoke(inputActionItem, bindingItem);
        }
    }
}