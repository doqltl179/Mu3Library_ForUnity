using Mu3Library.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Mu3Library.Sample.UI {
    public class SampleUIController : MonoBehaviour {
        [Header("Int Slider")]
        [SerializeField] private IntSlider intSlider;
        [SerializeField] private TextMeshPro intSliderValueText;



        #region Action
        public void OnIntSliderValueChanged() {

        }
        #endregion
    }
}