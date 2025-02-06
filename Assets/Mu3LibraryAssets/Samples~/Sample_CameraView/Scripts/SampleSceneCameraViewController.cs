using Mu3Library.CameraUtil;
using UnityEngine;

namespace Mu3Library.Demo.CameraView {
    public class SampleSceneCameraViewController : MonoBehaviour {
        [SerializeField] private SimpleCameraThirdPersonView thirdPersonView;
        [SerializeField] private Transform thirdPersonTarget;



        private void Start() {
            thirdPersonView.Init(Camera.main, thirdPersonTarget, true);
            thirdPersonView.SetCameraBehindTarget(true, true);
        }
    }
}