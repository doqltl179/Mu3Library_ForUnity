using Mu3Library.Base.CameraUtil;
using UnityEngine;

namespace Mu3Library.Base.Sample.CameraView {
    public class SampleSceneCameraViewController : MonoBehaviour {
        enum ViewState {
            None = 0, 

            FreeView = 1, 
            ThirdPersonView = 2,
            FirstPersonView = 3,
        }

        [SerializeField] private SimpleCameraFreeView freeView;
        [SerializeField] private SimpleCameraThirdPersonView thirdPersonView;
        [SerializeField] private SimpleCameraFirstPersonView firstPersonView;

        [Space(20)]
        [SerializeField] private Transform thirdPersonTarget;

        private ViewState currentViewState = ViewState.None;



        private void Start() {
            freeView.gameObject.SetActive(false);
            thirdPersonView.gameObject.SetActive(false);
            firstPersonView.gameObject.SetActive(false);

            ActivateView(currentViewState);
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.BackQuote)) {
                if(currentViewState == ViewState.None) return;
                DeactivateView(currentViewState);
                currentViewState = ViewState.None;
            }
            else if(Input.GetKeyDown(KeyCode.Alpha1)) {
                if(currentViewState == ViewState.FreeView) return;
                DeactivateView(currentViewState);
                currentViewState = ViewState.FreeView;
                ActivateView(currentViewState);
            }
            else if(Input.GetKeyDown(KeyCode.Alpha2)) {
                if(currentViewState == ViewState.ThirdPersonView) return;
                DeactivateView(currentViewState);
                currentViewState = ViewState.ThirdPersonView;
                ActivateView(currentViewState);
            }
            else if(Input.GetKeyDown(KeyCode.Alpha3)) {
                if(currentViewState == ViewState.FirstPersonView) return;
                DeactivateView(currentViewState);
                currentViewState = ViewState.FirstPersonView;
                ActivateView(currentViewState);
            }
        }

        private void ActivateView(ViewState state) {
            switch(state) {
                case ViewState.FreeView: {
                        freeView.gameObject.SetActive(true);

                        freeView.Init(Camera.main);
                    }
                    break;
                case ViewState.ThirdPersonView: {
                        thirdPersonView.gameObject.SetActive(true);

                        thirdPersonView.Init(Camera.main, thirdPersonTarget);
                        thirdPersonView.SetCameraBehindTarget(false);
                    }
                    break;
                case ViewState.FirstPersonView: {
                        firstPersonView.gameObject.SetActive(true);

                        firstPersonView.Init(Camera.main, thirdPersonTarget);
                        firstPersonView.SetCameraForwardTarget(false);
                    }
                    break;
            }
        }

        private void DeactivateView(ViewState state) {
            switch(state) {
                case ViewState.FreeView: freeView.gameObject.SetActive(false); break;
                case ViewState.ThirdPersonView: thirdPersonView.gameObject.SetActive(false); break;
                case ViewState.FirstPersonView: firstPersonView.gameObject.SetActive(false); break;
            }
        }
    }
}