using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Scene {
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    /// <summary>
    /// Create once at each scenes.
    /// </summary>
    public class SceneUI : MonoBehaviour {
        protected RectTransform rectTransform;
        protected Canvas canvas;
        protected CanvasScaler canvasScaler;
        protected GraphicRaycaster graphicRaycaster;

        public bool IsTransitioning => transitionCoroutine != null;
        private IEnumerator transitionCoroutine;

        public SceneUILayer CurrentLayer { get; private set; }



        protected virtual void Awake() {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponent<Canvas>();
            canvasScaler = GetComponent<CanvasScaler>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
        }

        public virtual void OnFirstActivate() { }

        #region Utility
        public void Transition(SceneUILayer layer, float time = 0) {
            if(transitionCoroutine == null) {
                transitionCoroutine = TransitionCoroutine(CurrentLayer, layer, time);
                StartCoroutine(transitionCoroutine);
            }
        }

        public IEnumerator TransitionCoroutine(SceneUILayer from, SceneUILayer to, float time = 0) {
            float halfTime = time * 0.5f;

            if(halfTime == 0) {
                if(from != null) {
                    from.OnDeactivate();

                    from.Alpha = 0.0f;
                    from.SetActive = false;
                }

                if(to != null) {
                    to.Alpha = 1.0f;
                    to.SetActive = true;

                    to.OnActivate();
                }
            }
            else {
                float timer = halfTime;

                if(from != null) {
                    from.Interactable = false;

                    while(timer < halfTime) {
                        timer -= Time.deltaTime;

                        from.Alpha = timer / halfTime;

                        yield return null;
                    }
                }

                if(to != null) {
                    to.Interactable = false;

                    while(timer > 0) {
                        timer += Time.deltaTime;

                        to.Alpha = timer / halfTime;

                        yield return null;
                    }

                    to.Interactable = true;
                }
            }

            CurrentLayer = to;

            transitionCoroutine = null;
        }
        #endregion
    }
}