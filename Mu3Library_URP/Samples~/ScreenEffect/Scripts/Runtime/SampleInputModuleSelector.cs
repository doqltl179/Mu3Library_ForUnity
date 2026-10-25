using UnityEngine;
using UnityEngine.EventSystems;
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace Mu3Library.URP.Sample.ScreenEffect
{
    /// <summary>
    /// Gives the <c>EventSystem</c> on this object the UI input module of the input backend the project enables,
    /// by the rule <c>MVPManager</c> follows for the <c>EventSystem</c> it creates. The scene holds a
    /// <c>StandaloneInputModule</c>, which reads the Input Manager. With the Input System package installed and
    /// its backend on, under the Active Input Handling settings "Input System Package (New)" and "Both", this
    /// replaces it with an <c>InputSystemUIInputModule</c> before the <c>EventSystem</c> first updates.
    /// </summary>
    [DisallowMultipleComponent]
    public class SampleInputModuleSelector : MonoBehaviour
    {
        private void Awake()
        {
#if TEMPLATE_INPUTSYSTEM_SUPPORT && ENABLE_INPUT_SYSTEM
            if (TryGetComponent(out StandaloneInputModule standaloneModule))
            {
                // Disabling drops it from the EventSystem at once; Destroy only takes effect after this frame.
                standaloneModule.enabled = false;
                Destroy(standaloneModule);
            }

            if (!TryGetComponent(out InputSystemUIInputModule _))
            {
                gameObject.AddComponent<InputSystemUIInputModule>();
            }
#endif
        }
    }
}
