using DG.Tweening;
using Mu3Library.Scene;
using Mu3Library.Utility;
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
            get => fillImage.fillAmount;
            set {
                fillImage.fillAmount = value;
                progressText.text = $"{(value * 100).ToString("0.0")}%";
            }
        }

        private Sequence fadeAnimationTween = null;

        private IEnumerator progressUpdateCoroutine = null;



        #region Utility
        public void SetActive(bool active, float fadeTime = 0.0f) {
            if(fadeAnimationTween != null) {
                fadeAnimationTween.Kill();
            }

            if(fadeTime <= 0) {
                loadingObj.SetActive(active);
                canvasGroup.alpha = active ? 1.0f : 0.0f;
            }
            else {
                fadeAnimationTween = DOTween.Sequence();
                loadingObj.SetActive(true);

                fadeAnimationTween.Append(DOTween.To(
                    () => canvasGroup.alpha,
                    (value) => canvasGroup.alpha = value,
                    active ? 1.0f : 0.0f,
                    fadeTime));
                if(!active) {
                    fadeAnimationTween.AppendCallback(() => {
                        loadingObj.SetActive(false);
                    });
                }
                fadeAnimationTween.AppendCallback(() => {
                    fadeAnimationTween = null;
                });

                fadeAnimationTween.Play();
            }
        }

        public void UpdateProgress() {
            if(progressUpdateCoroutine == null) {
                progressUpdateCoroutine = ProgressUpdateCoroutine();
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

        private IEnumerator ProgressUpdateCoroutine() {
            while(SceneLoader.Instance.IsLoading) {
                Progress = SceneLoader.Instance.ProgressNum;

                yield return null;
            }

            progressUpdateCoroutine = null;
        }
    }
}