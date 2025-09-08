using System;
using System.Collections;
using Mu3Library.Base.Utility;
using UnityEngine;

namespace Mu3Library.Base.UI {
    public class FadeHelper : MonoBehaviour {
        private CanvasGroup cg = null;
        public bool Interactable {
            get => cg.interactable;
            set => cg.interactable = value;
        }
        public bool blocksRaycasts {
            get => cg.blocksRaycasts;
            set => cg.blocksRaycasts = value;
        }
        public bool ignoreParentGroups {
            get => cg.ignoreParentGroups;
            set => cg.ignoreParentGroups = value;
        }

        /// <summary>
        /// CanvasGroup의 alpha값이 0일 때 GameObject를 비활성화 시킨다.
        /// </summary>
        public bool DeactivateWhenAlphaEqualsZero {
            get => deactivateToFalseWhenAlphaEqualsZero;
            set => deactivateToFalseWhenAlphaEqualsZero = value;
        }
        private bool deactivateToFalseWhenAlphaEqualsZero = true;

        public bool IsFading => fadeCoroutine != null;

        private IEnumerator fadeCoroutine = null;



        private void Awake() {
            Init();
        }

        private void OnDisable() {
            StopFading();
        }

        #region Utility
        public void StopFading() {
            if(fadeCoroutine != null) {
                StopCoroutine(fadeCoroutine);
                fadeCoroutine = null;
            }
        }

        public void FadeOut(float time, EasingFunction.Ease easing, Action callback = null) {
            // GameObject가 비활성화 상태일 때는 동작하지 않는다.
            if(!gameObject.activeSelf) {
                return;
            }

            Fade(time, 1, 0, easing, callback);
        }

        public void FadeIn(float time, EasingFunction.Ease easing, Action callback = null) {
            // GameObject가 비활성화 상태에서도 사용 가능하게 만든다.
            // GameObject가 활성화 처음 되었을 때 초기화가 된다.
            gameObject.SetActive(true);

            Fade(time, 0, 1, easing, callback);
        }
        #endregion

        private void Fade(float time, float start, float end, EasingFunction.Ease easing, Action callback = null) {
            if(fadeCoroutine == null) {
                fadeCoroutine = FadeCoroutine(time, start, end, easing, callback);
                StartCoroutine(fadeCoroutine);
            }
        }

        private IEnumerator FadeCoroutine(float time, float start, float end, EasingFunction.Ease easing, Action callback = null) {
            EasingFunction.Function func = EasingFunction.GetEasingFunction(easing);

            float timer = (cg.alpha - start) / (end - start) * time;
            while(timer < time) {
                timer += Time.deltaTime;

                cg.alpha = func(start, end, timer / time);

                yield return null;
            }
            cg.alpha = end;

            fadeCoroutine = null;

            callback?.Invoke();

            // callback에서 다시 한 번 Fade를 했을 때, 조건이 성립되어 GameObject가 비활성화 된다면
            // callback에서 실행된 Fade가 강제종료 당하기 때문에 
            // "fadeCoroutine == null" 조건문을 추가해서 Fade가 불렸는지 체크하고
            // 불리지 않았을 때에만 GameObject를 비활성화 한다.
            if(deactivateToFalseWhenAlphaEqualsZero && end == 0 && fadeCoroutine == null) {
                gameObject.SetActive(false);
            }
        }

        private void Init() {
            if(cg == null) {
                cg = GetComponent<CanvasGroup>();
                if(cg == null) {
                    cg = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }
}
