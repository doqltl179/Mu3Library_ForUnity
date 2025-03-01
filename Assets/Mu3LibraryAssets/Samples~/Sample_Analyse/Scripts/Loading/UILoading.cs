using Mu3Library.UI;
using Mu3Library.Utility;
using UnityEngine;

namespace Mu3Library.Demo.Analyse {
    [RequireComponent(typeof(FadeHelper))]
    public class UILoading : Singleton<UILoading> {
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