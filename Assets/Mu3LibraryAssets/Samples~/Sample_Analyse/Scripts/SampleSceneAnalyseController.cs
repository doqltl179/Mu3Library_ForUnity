using System.Collections;
using UnityEngine;

namespace Mu3Library.Sample.Analyse {
    public class SampleSceneAnalyseController : MonoBehaviour {
        [SerializeField] private UIHardDrive uiHardDrive;



        private void Start() {
            UILoading.Instance.FadeHelper.DeactivateWhenAlphaEqualsZero = true;
            UILoading.Instance.FadeHelper.FadeOut(0, EasingFunction.Ease.Linear);

            uiHardDrive.InitLayers();

            uiHardDrive.ShowData();
        }
    }
}