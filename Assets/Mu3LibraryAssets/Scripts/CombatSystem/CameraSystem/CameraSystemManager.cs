using Mu3Library.Utility;
using UnityEngine;

namespace Mu3Library.CombatSystem {
    public enum CameraState {
        None = 0, 

        /// <summary>
        /// CharacterController를 따라다닌다.
        /// </summary>
        FollowCharacter = 1,
        /// <summary>
        /// Transform을 따라다닌다.
        /// </summary>
        FollowTarget = 2, 
    }

    public abstract class CameraSystemManager : MonoBehaviour {
        protected Camera mainCamera;

        #region Public Get Properties

        public Vector3 MainCameraPosition => mainCamera.transform.position;
        public Vector3 MainCameraEulerAngles => mainCamera.transform.eulerAngles;
        public Quaternion MainCameraRotation => mainCamera.transform.rotation;

        public Vector3 MainCameraForward => mainCamera.transform.forward;
        public Vector3 MainCameraRight => mainCamera.transform.right;

        #endregion

        public CameraState CurrentState => currentState;
        protected CameraState currentState = CameraState.None;

        protected ICameraStateAction currentStateAction = null;



        #region Utility
        public virtual void UpdateAction() {
            if(mainCamera == null) {
                return;
            }

            if(currentStateAction != null) {
                currentStateAction.Update();
            }
        }

        public abstract void ChangeCameraStateToFollowCharacter(Camera camera, Transform target);

        public abstract void ChangeCameraStateToFollowCharacter(Camera camera, CharacterController controller);

        public virtual void ChangeCameraStateToNone() {
            if(currentStateAction != null) {
                currentStateAction.Exit();
                currentStateAction = null;
            }

            mainCamera = null;

            currentState = CameraState.None;
        }
        #endregion
    }
}
