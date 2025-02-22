using Mu3Library.Attribute;
using Mu3Library.UI;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Demo.UI {
    public class SampleSceneUIController : MonoBehaviour {
        [Title("Int Slider")]
        [SerializeField] private IntSlider intSlider;
        [SerializeField] private TextMeshProUGUI intSliderValueText;
        [SerializeField] private int intValueMin = 0;
        [SerializeField] private int intValueMax = 10;
        [SerializeField] private int intValueDefault = 5;

        [Title("Date Picker")]
        [SerializeField] private DatePicker datePicker;

        [Title("Fade")]
        [SerializeField] private Image fadeImage;
        [SerializeField] private float fadeTime;
        [SerializeField] private EasingFunction.Ease easingType = EasingFunction.Ease.Linear;
        [SerializeField] private bool deactivateFadeHelperWhenAlphaEqualsZero = true;
        private FadeHelper fadeHelper;



        private void Start() {
            intSlider.SetMinMaxValue(intValueMin, intValueMax, intValueDefault);
            OnIntSliderValueChanged();

            datePicker.SetDateToNow();

            fadeHelper = fadeImage.gameObject.AddComponent<FadeHelper>();
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.I)) {
                fadeHelper.FadeIn(fadeTime, easingType, () => {
                    Debug.Log("FadeIn callback.");
                });
            }
            else if(Input.GetKeyDown(KeyCode.O)) {
                fadeHelper.DeactivateWhenAlphaEqualsZero = deactivateFadeHelperWhenAlphaEqualsZero;
                fadeHelper.FadeOut(fadeTime, easingType, () => {
                    Debug.Log("FadeOut callback.");
                });
            }
            else if(Input.GetKeyDown(KeyCode.P)) {
                fadeHelper.DeactivateWhenAlphaEqualsZero = deactivateFadeHelperWhenAlphaEqualsZero;
                fadeHelper.FadeOut(fadeTime, easingType, () => {
                    Debug.Log("FadeOut callback.");

                    fadeHelper.FadeIn(fadeTime, easingType, () => {
                        Debug.Log("FadeIn callbaack");
                    });
                });
            }
            else if(Input.GetKeyDown(KeyCode.U)) {
                fadeHelper.DeactivateWhenAlphaEqualsZero = deactivateFadeHelperWhenAlphaEqualsZero;
                fadeHelper.FadeIn(fadeTime, easingType, () => {
                    Debug.Log("FadeIn callback.");

                    fadeHelper.FadeOut(fadeTime, easingType, () => {
                        Debug.Log("FadeOut callbaack");
                    });
                });
            }
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