using Mu3Library.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Mu3Library.Demo.Sample_UI {
    public class SampleSceneUIController : MonoBehaviour {
        [Header("Int Slider")]
        [SerializeField] private IntSlider intSlider;
        [SerializeField] private TextMeshProUGUI intSliderValueText;

        [Header("Date Picker")]
        [SerializeField] private DatePicker datePicker;



        private void Start() {
            datePicker.SetDateToNow();
        }

        #region Action
        public void OnIntSliderValueChanged() {
            intSliderValueText.text = $"({intSlider.IntValueMin}/{intSlider.IntValue}/{intSlider.IntValueMax})";
        }
        #endregion
    }
}