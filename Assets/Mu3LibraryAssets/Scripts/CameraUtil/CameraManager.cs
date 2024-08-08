using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SceneLoader = Mu3Library.Scene.SceneLoader;
using SceneType = Mu3Library.Scene.SceneLoader.SceneType;

namespace Mu3Library.CameraUtil {
    public class CameraManager : GenericSingleton<CameraManager> {
        private Camera cam = null;

        public bool isCameraExist => cam != null;

        public Vector3 CamPos {
            get => cam.transform.position;
            set => cam.transform.position = value;
        }
        public Vector3 CamEuler {
            get => cam.transform.eulerAngles;
            set => cam.transform.eulerAngles = value;
        }
        public Vector3 CamForward {
            get => cam.transform.forward;
            set => cam.transform.forward = value;
        }
        public Vector3 CamRight {
            get => cam.transform.right;
            set => cam.transform.right = value;
        }
        public Vector3 CamUp {
            get => cam.transform.up;
            set => cam.transform.up = value;
        }
        public Quaternion CamRot {
            get => cam.transform.rotation;
            set => cam.transform.rotation = value;
        }

        private CameraMoveSystem currentMoveSystem = null;
        private CameraMoveSystemType currentMoveSystemType = CameraMoveSystemType.None;

        private CameraRotateSystem currentRotateSystem = null;
        private CameraRotateSystemType currentRotateSystemType = CameraRotateSystemType.None;



        private void Awake() {
            SceneLoader.Instance.OnSceneChanged += OnSceneChanged;
        }

        private void OnDestroy() {
            SceneLoader.Instance.OnSceneChanged -= OnSceneChanged;
        }

        private void Update() {
            if(cam != null) {
                if(currentMoveSystem != null && currentMoveSystem.IsPlaying) {
                    currentMoveSystem.Move(cam);
                }

                if(currentRotateSystem != null && currentRotateSystem.IsPlaying) {
                    currentRotateSystem.Rotate(cam);
                }
            }
        }

        #region Utility

        public void ChangeMoveSystem(CameraMoveSystemType type, object[] param = null) {
            if(currentMoveSystemType == type) {
                Debug.Log("CameraMoveSystemType already applyed.");

                return;
            }

            currentMoveSystem = GetMoveSystem(type, param);
            currentMoveSystemType = type;
        }

        public void ChangeRotateSystem(CameraRotateSystemType type, object[] param = null) {
            if(currentRotateSystemType == type) {
                Debug.Log("CameraRotateSystem already applyed.");

                return;
            }

            currentRotateSystem = GetRotateSystem(type, param);
            currentRotateSystemType = type;
        }

        public void StartMoveAndRotate() {
            if(currentMoveSystem != null) currentMoveSystem.Play();
            if(currentRotateSystem != null) currentRotateSystem.Play();
        }

        public void StopMoveAndRotate() {
            if(currentMoveSystem != null) currentMoveSystem.Stop();
            if(currentRotateSystem != null) currentRotateSystem.Stop();
        }

        public void StartMove() {
            if(currentMoveSystem != null) currentMoveSystem.Play();
        }

        public void StopMove() {
            if(currentMoveSystem != null) currentMoveSystem.Stop();
        }

        public void StartRotate() {
            if(currentRotateSystem != null) currentRotateSystem.Play();
        }

        public void StopRotate() {
            if(currentRotateSystem != null) currentRotateSystem.Stop();
        }

        public void SetCameraToMainCamera() {
            cam = Camera.main;
        }
        #endregion

        #region Action
        private void OnSceneChanged(SceneType from, SceneType to) {
            cam = Camera.main;
        }
        #endregion

        private CameraMoveSystem GetMoveSystem(CameraMoveSystemType type, object[] param = null) {
            CameraMoveSystem system = null;
            switch(type) {
                case CameraMoveSystemType.None: {

                    }
                    break;

                case CameraMoveSystemType.LocalFollowing: {
                        system = new CameraMoveSystem_LocalFollowing();
                    }
                    break;

                case CameraMoveSystemType.WorldFollowing: {
                        system = new CameraMoveSystem_WorldFollowing();
                    }
                    break;

                default: {
                        Debug.Log($"Not defined 'CameraMoveSystem'. type: {type}");
                    }
                    break;
            }

            if(system != null) {
                system.SetProperties(param);
            }

            return system;
        }

        private CameraRotateSystem GetRotateSystem(CameraRotateSystemType type, object[] param = null) {
            CameraRotateSystem system = null;
            switch(type) {
                case CameraRotateSystemType.None: {

                    }
                    break;

                case CameraRotateSystemType.LocalLooking: {
                        system = new CameraRotateSystem_LocalLooking();
                    }
                    break;

                case CameraRotateSystemType.WorldLooking: {
                        system = new CameraRotateSystem_WorldLooking();
                    }
                    break;

                default: {
                        Debug.Log($"Not defined 'CameraRotateSystem'. type: {type}");
                    }
                    break;
            }

            if(system != null) {
                system.SetProperties(param);
            }

            return system;
        }
    }

    public enum CameraMoveSystemType {
        None,

        /// <summary>
        /// Following target with 'local position offset'.
        /// </summary>
        LocalFollowing,
        /// <summary>
        /// Following target with 'world position offset'.
        /// </summary>
        WorldFollowing,
    }

    public enum CameraRotateSystemType {
        None,

        /// <summary>
        /// Looking target with 'local position offset'.
        /// </summary>
        LocalLooking,
        /// <summary>
        /// Looking target with 'world position offset'.
        /// </summary>
        WorldLooking,
    }
}