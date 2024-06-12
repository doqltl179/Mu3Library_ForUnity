using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Scene {
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public class SceneUI : MonoBehaviour {
        protected RectTransform rectTransform;
        protected Canvas canvas;
        protected CanvasScaler canvasScaler;
        protected GraphicRaycaster graphicRaycaster;

        private Sequence transitionSequence;
        public bool IsTransitioning => transitionSequence != null;

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
            Transition(CurrentLayer, layer, time);
        }

        public void Transition(SceneUILayer from, SceneUILayer to, float time = 0) {
            if(transitionSequence != null) {
                transitionSequence.Kill();
                transitionSequence = null;
            }

            if(time > 0) {
                transitionSequence = DOTween.Sequence();
                float halfTime = time * 0.5f;

                if(from != null) {
                    from.Interactable = false;

                    float fromTransitionTime = (from.Alpha / 1.0f) * halfTime;
                    transitionSequence.Append(DOTween.To(
                        () => from.Alpha,
                        (value) => {
                            from.Alpha = value;
                        },
                        0.0f,
                        fromTransitionTime));

                    transitionSequence.AppendCallback(() => {
                        from.OnDeActivate();

                        from.Interactable = true;
                        from.SetActive = false;
                    });
                }

                if(to != null) {
                    to.Interactable = false;

                    transitionSequence.AppendCallback(() => {
                        to.SetActive = true;

                        to.OnActivate();
                    });

                    float toTransitionTime = (1.0f - (to.Alpha / 1.0f)) * halfTime;
                    transitionSequence.Append(DOTween.To(
                        () => to.Alpha,
                        (value) => {
                            to.Alpha = value;
                        },
                        1.0f,
                        toTransitionTime));

                    transitionSequence.AppendCallback(() => to.Interactable = true);
                }

                transitionSequence.AppendCallback(() => transitionSequence = null);
            }
            else {
                if(from != null) {
                    from.OnDeActivate();

                    from.Alpha = 0.0f;
                    from.SetActive = false;
                }

                if(to != null) {
                    to.Alpha = 1.0f;
                    to.SetActive = true;

                    to.OnActivate();
                }
            }

            CurrentLayer = to;
        }
        #endregion
    }
}