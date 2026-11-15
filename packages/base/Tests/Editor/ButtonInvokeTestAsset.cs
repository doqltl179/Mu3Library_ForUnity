using Mu3Library.Attribute;
using UnityEngine;

namespace Mu3Library.Tests
{
    /// <summary>A ScriptableObject without a custom editor, so the Base fallback editor draws it.</summary>
    public class ButtonInvokeTestAsset : ScriptableObject
    {
        [SerializeField] private UIToolkitOnlyValue _value = new UIToolkitOnlyValue();

        public int Count { get; private set; }

        [ButtonInvoke]
        private void Increment()
        {
            Count += _value.Number + 1;
        }
    }
}
