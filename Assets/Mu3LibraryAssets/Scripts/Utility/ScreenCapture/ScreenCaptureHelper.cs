using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mu3Library.Utility {
    public static class ScreenCaptureHelper {
        private static bool isScreenCaptureRunning = false;



        #region Utility
        public static void ScreenShot(string directory, int superSize) {
            RecalculateSuperSize(ref superSize);

            string fileName = GetFileName();
            string filePath = $"{directory}/{fileName}.png";
            // 이유는 모르겠으나, EditMode에서 캡처한 이미지를 처리하는데 한 박자씩 작업이 밀리는 것처럼 보이는 경향이 있다.
            // 이 때, 이미지가 생성되었다는 로그는 나오지만 이미지 파일이 보이지 않는 문제가 있는데
            // Editor에서 플레이를 한 번 해주면 파일이 보이게 된다.
            ScreenCapture.CaptureScreenshot(filePath, superSize);

            Debug.Log($"Screen Capture. path: {filePath}");
        }

        public static void ScreenShot(string directory, int superSize, Action<Texture2D> callback) {
            if(!Application.isPlaying) {
                Debug.LogError($"Current mode is not PlayMode.");

                return;
            }

            GameObject go = new GameObject("ScreenCaptureObject");
            ScreenCaptureObject scObj = go.AddComponent<ScreenCaptureObject>();

            RecalculateSuperSize(ref superSize);

            scObj.ScreenShot(superSize, (tex) => {
                if(tex != null) {
                    string fileName = GetFileName();
                    string filePath = $"{directory}/{fileName}.png";
                    byte[] bytes = tex.EncodeToPNG();
                    File.WriteAllBytes(filePath, bytes);

                    Debug.Log($"Screen Capture. path: {filePath}");
                }

                callback?.Invoke(tex);

                if(go != null) {
                    MonoBehaviour.Destroy(go);
                }
            });
        }
        #endregion

        private static void RecalculateSuperSize(ref int superSize) {
            if(superSize <= 0) {
                // 기본 해상도로 설정
                superSize = 1;
            }
            else {
                // 최대 해상도 설정
                const int widthMax = 7680;
                const int heightMax = 4320;

                int width = Screen.width;
                int height = Screen.height;
                if(Camera.main != null) {
                    width = Camera.main.scaledPixelWidth;
                    height = Camera.main.scaledPixelHeight;
                }

                int superSizeWidth = width * superSize;
                int superSizeHeight = height * superSize;
                if(superSizeWidth > widthMax || superSizeHeight > heightMax) {
                    int widthDiff = superSizeWidth - widthMax;
                    int heightDiff = superSizeHeight - heightMax;

                    int ceilWidth = Mathf.CeilToInt((float)widthDiff / width);
                    int ceilHeight = Mathf.CeilToInt((float)heightDiff / height);

                    superSize -= Mathf.Max(ceilWidth, ceilHeight);
                    if(superSize <= 0) {
                        superSize = 1;
                    }

                    int changedWidth = width * superSize;
                    int changedHeight = height * superSize;
                    Debug.Log($"SuperSize Changed. original: {width}x{height}, superSize: {superSizeWidth}x{superSizeHeight}, changed: {changedWidth}x{changedHeight}");
                }
            }
        }

        private static string GetFileName() {
            return $"ScreenCapture_{DateTime.Now.ToString("yyyyMMdd_HHmmss_fffffff")}";
        }
    }
}