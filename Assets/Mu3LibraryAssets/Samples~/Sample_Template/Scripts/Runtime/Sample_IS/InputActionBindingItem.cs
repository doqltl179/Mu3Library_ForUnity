using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
using UnityEngine.InputSystem;
#endif

namespace Mu3Library.Sample.Template.IS
{
    public class InputActionBindingItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _bindingText;
        [SerializeField] private Button _button;

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        private InputBinding _inputBinding;
        public InputBinding InputBinding => _inputBinding;
#endif



        #region Utility

#if TEMPLATE_INPUTSYSTEM_SUPPORT
        public void SetInputBinding(InputBinding inputBinding)
        {
            _nameText.text = inputBinding.name;

            string displayPath = string.IsNullOrEmpty(inputBinding.effectivePath) ?
                string.Empty :
                InputControlPath.ToHumanReadableString(
                    inputBinding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            _bindingText.text = displayPath;

            _inputBinding = inputBinding;
        }
#endif

        #endregion
    }
}