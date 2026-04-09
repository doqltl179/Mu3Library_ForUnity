using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Mu3Library.URP.Sample.ScreenEffect.Util
{
    public class SliderValueTextSetter : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private TextMeshProUGUI _text;

        [Space(20)]
        [SerializeField, Range(0, 3)] private int _decimalPlace = 2;



        private void Start()
        {
            _slider.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnDestroy()
        {
            _slider.onValueChanged.RemoveListener(OnValueChanged);
        }

        private void OnValueChanged(float value)
        {
            _text.text = value.ToString($"F{_decimalPlace}");
        }
    }
}