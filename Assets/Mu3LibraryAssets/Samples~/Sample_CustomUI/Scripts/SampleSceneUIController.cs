using Mu3Library.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace Mu3Library.Demo.UI {
    public class SampleSceneUIController : MonoBehaviour {
        [Header("Int Slider")]
        [SerializeField] private IntSlider intSlider;
        [SerializeField] private TextMeshProUGUI intSliderValueText;
        [SerializeField] private int intValueMin = 0;
        [SerializeField] private int intValueMax = 10;
        [SerializeField] private int intValueDefault = 5;

        [Header("Date Picker")]
        [SerializeField] private DatePicker datePicker;



        private void Start() {
            intSlider.SetMinMaxValue(intValueMin, intValueMax, intValueDefault);
            OnIntSliderValueChanged();

            datePicker.SetDateToNow();
        }

        #region Action
        public void OnIntSliderValueChanged() {
            intSliderValueText.text = $"min: {intSlider.IntValueMin}\r\n" +
                $"max: {intSlider.IntValueMax}\r\n" +
                $"current: {intSlider.IntValue}";
        }
        #endregion
    }
}