using Mu3Library.UI;
using UnityEngine;

namespace Mu3Library.Demo.Analyse {
    [RequireComponent(typeof(FadeHelper))]
    public abstract class AnalyseCanvas : MonoBehaviour {
        public FadeHelper FadeHelper {
            get {
                if(fadeHelper == null) {
                    fadeHelper = GetComponent<FadeHelper>();
                    if(fadeHelper == null) {
                        fadeHelper = gameObject.AddComponent<FadeHelper>();
                    }
                }

                return fadeHelper;
            }
        }
        private FadeHelper fadeHelper;

        public AnalyseLayer CurrentLayer => currentLayer;
        protected AnalyseLayer currentLayer = null;



        public abstract void InitLayers();

        protected void TransitionLayer(AnalyseLayer changeTo, float time = 0.6f) {
            if((currentLayer != null && currentLayer.FadeHelper.IsFading) || 
                (changeTo != null && changeTo.FadeHelper.IsFading)) {
                return;
            }

            if(currentLayer != null) {
                currentLayer.FadeHelper.Interactable = false;
                currentLayer.FadeHelper.FadeOut(time, EasingFunction.Ease.EaseOutCubic);
            }

            if(changeTo != null) {
                changeTo.FadeHelper.Interactable = false;
                changeTo.FadeHelper.FadeIn(time, EasingFunction.Ease.EaseOutCubic, () => {
                    changeTo.FadeHelper.Interactable = true;
                });
            }

            currentLayer = changeTo;
        }
    }
}