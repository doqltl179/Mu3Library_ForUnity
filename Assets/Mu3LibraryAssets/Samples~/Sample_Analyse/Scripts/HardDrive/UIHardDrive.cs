/*
 * [ 참고 사항 ]
 * 첫 화면에서 Drive를 선택하면 현재 Directory의 정보를 생성하고 재사용하자.
 * 매 선택마다 정보를 계속해서 불러오게 만드니 로딩에 시간이 엄청 걸림.
 */

using System.Collections;
using System.IO;
using UnityEngine;

namespace Mu3Library.Sample.Analyse {
    public class UIHardDrive : AnalyseCanvas {
        [SerializeField] private LayerHardDrives layerHardDrives;
        [SerializeField] private LayerHardDriveInfos layerHardDriveInfos;

        private IEnumerator showDataCoroutine = null;



        private void OnDisable() {
            if(showDataCoroutine != null) {
                StopCoroutine(showDataCoroutine);
                showDataCoroutine = null;
            }
        }

        #region Utility
        public void ShowData() {
            layerHardDrives.SetData(ShowDriveInfo);

            TransitionLayer(layerHardDrives);
        }

        public override void InitLayers() {
            layerHardDrives.FadeHelper.DeactivateWhenAlphaEqualsZero = true;
            layerHardDriveInfos.FadeHelper.DeactivateWhenAlphaEqualsZero = true;

            layerHardDrives.FadeHelper.FadeOut(0, EasingFunction.Ease.Linear);
            layerHardDriveInfos.FadeHelper.FadeOut(0, EasingFunction.Ease.Linear);
        }
        #endregion

        private void ShowDriveInfo(DriveInfo info) {
            TransitionLayer(null);

            UILoading.Instance.FadeHelper.FadeIn(0.6f, EasingFunction.Ease.EaseOutCubic, () => {
                layerHardDriveInfos.SetData(info, () => {
                    TransitionLayer(layerHardDriveInfos);

                    UILoading.Instance.FadeHelper.FadeOut(0.6f, EasingFunction.Ease.EaseOutCubic);
                });
            });
        }
    }
}