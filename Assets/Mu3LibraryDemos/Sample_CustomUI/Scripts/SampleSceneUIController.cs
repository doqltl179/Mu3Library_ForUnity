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

        [Header("Date Picker")]
        [SerializeField] private DatePicker datePicker;



        private void Start() {
            datePicker.SetDateToNow();
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.Space)) {
                StartCoroutine(Capture());
            }
        }

        private IEnumerator Capture() {
            yield return new WaitForEndOfFrame();

            // 현재 화면 크기 가져오기
            int screenWidth = 1920;
            int screenHeight = 1080;

            // Texture2D 생성 (스크린 크기와 동일)
            Texture2D screenshot = new Texture2D(screenWidth, screenHeight, TextureFormat.RGB24, false);

            // GPU에서 화면 데이터를 읽어오기
            screenshot.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
            screenshot.Apply();

            string directory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string fileName = $"ScreenCapture_{screenWidth}x{screenHeight}_{DateTime.Now.ToString("yyyyMMdd_HHmmss_fffffff")}";
            string filePath = $"{directory}/{fileName}.png";

            // PNG로 저장
            byte[] bytes = screenshot.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);

            Debug.Log($"Screenshot saved to: {filePath}");

            // 메모리 해제
            Destroy(screenshot);
        }

        #region Action
        public void OnIntSliderValueChanged() {
            intSliderValueText.text = $"({intSlider.IntValueMin}/{intSlider.IntValue}/{intSlider.IntValueMax})";
        }
        #endregion
    }
}