using System;
using System.Collections;
using UnityEngine;

namespace Mu3Library.ScreenShot {
    /// <summary>
    /// ScreenCapture를 할 때 Coroutine을 사용하기 위함.
    /// </summary>
    public class ScreenCaptureObject : MonoBehaviour {




        #region Utility
        public void ScreenShot(int superSize, Action<Texture2D> callback) {
            if(gameObject == null) {
                Debug.LogError("GameObject not found.");

                return;
            }
            if(!Application.isPlaying) {
                Debug.LogError($"Current mode is not PlayMode.");

                return;
            }

            StartCoroutine(ScreenShotCoroutine(superSize, callback));
        }
        #endregion

        private IEnumerator ScreenShotCoroutine(int superSize, Action<Texture2D> callback) {
            // 프레임의 마지막으로 이동하지 않고 캡처를 하면 이미지가 이상하게 나오는 경우가 있다.
            yield return new WaitForEndOfFrame();

            // "PlayMode"에서만 사용이 가능하다.
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture(superSize);

            callback?.Invoke(tex);
        }
    }
}