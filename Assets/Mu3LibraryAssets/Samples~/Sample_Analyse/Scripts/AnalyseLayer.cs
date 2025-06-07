using Mu3Library.UI;
using UnityEngine;

namespace Mu3Library.Sample.Analyse {
    [RequireComponent(typeof(FadeHelper))]
    public abstract class AnalyseLayer : MonoBehaviour {
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
    }
}