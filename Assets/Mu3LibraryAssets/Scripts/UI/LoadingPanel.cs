using Mu3Library.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.UI {
    public class LoadingPanel : MonoBehaviour {
        public static LoadingPanel Instance {
            get {
                if(instance == null) {
                    instance = FindObjectOfType<LoadingPanel>();
                    if(instance == null) {
                        GameObject obj = ResourceLoader.GetResource<GameObject>($"{nameof(LoadingPanel)}");
                        if(obj != null) {
                            GameObject go = Instantiate(obj);
                            instance = go.GetComponent<LoadingPanel>();

                            DontDestroyOnLoad(go);
                        }
                    }
                }

                return instance;
            }
        }
        private static LoadingPanel instance = null;

        [SerializeField] private GameObject loadingObj;
        [SerializeField] private CanvasGroup canvasGroup;

        [Space(20)]
        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI progressText;
        public float Progress {
            get => progress;
            set {
                fillImage.fillAmount = value;
                progressText.text = $"{(value * 100).ToString("0.0")}%";

                progress = value;
            }
        }
        private float progress = 0.0f;

        private IEnumerator fadeAnimationCoroutine = null;
        private IEnumerator progressUpdateCoroutine = null;



        #region Utility
        public void SetActive(bool active, float fadeTime = 0.0f) {
            if(fadeTime <= 0) {
                loadingObj.SetActive(active);
                canvasGroup.alpha = active ? 1.0f : 0.0f;
            }
            else {
                if(fadeAnimationCoroutine == null) {
                    fadeAnimationCoroutine = FadeAnimationCoroutine(active, fadeTime);
                    StartCoroutine(fadeAnimationCoroutine);
                }
            }
        }

        public void UpdateProgress(Func<bool> keepProgressFunc, Func<float> progressUpdateFunc) {
            if(progressUpdateCoroutine == null) {
                progressUpdateCoroutine = ProgressUpdateCoroutine(keepProgressFunc, progressUpdateFunc);
                StartCoroutine(progressUpdateCoroutine);
            }
        }

        public void StopProgressUpdate() {
            if(progressUpdateCoroutine != null) {
                StopCoroutine(progressUpdateCoroutine);
                progressUpdateCoroutine = null;
            }
        }
        #endregion

        private IEnumerator FadeAnimationCoroutine(bool active, float fadeTime) {
            if(active) {
                canvasGroup.gameObject.SetActive(true);

                yield return StartCoroutine(FadeInCoroutine(fadeTime));
            }
            else {
                yield return StartCoroutine(FadeOutCoroutine(fadeTime));

                canvasGroup.gameObject.SetActive(false);
            }

            fadeAnimationCoroutine = null;
        }

        private IEnumerator FadeInCoroutine(float fadeTime) {
            float timer = 0.0f;

            while(timer < fadeTime) {
                timer += Time.deltaTime;

                canvasGroup.alpha = timer / fadeTime;

                yield return null;
            }
        }

        private IEnumerator FadeOutCoroutine(float fadeTime) {
            float timer = fadeTime;

            while(timer > 0) {
                timer -= Time.deltaTime;

                canvasGroup.alpha = timer / fadeTime;

                yield return null;
            }
        }

        private IEnumerator ProgressUpdateCoroutine(Func<bool> keepProgressFunc, Func<float> progressUpdateFunc) {
            while(keepProgressFunc()) {
                Progress = progressUpdateFunc();

                yield return null;
            }

            progressUpdateCoroutine = null;
        }
    }
}