using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SceneLoader = Mu3Library.Scene.SceneLoader;
using SceneType = Mu3Library.Scene.SceneLoader.SceneType;

namespace Mu3Library.CameraUtil {
    public class CameraManager : GenericSingleton<CameraManager> {
        private Camera cam = null;

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

        private Transform followingTarget = null;
        /// <summary>
        /// Offset calculated on Local Space.
        /// </summary>
        private Vector3 followingPosOffset = Vector3.zero;
        /// <summary>
        /// Range: 0.0 ~ 1.0
        /// </summary>
        private float followingStrength = 1.0f;
        public bool IsFollowing => IsFollowing;
        private bool isFollowing = false;
        /// <summary>
        /// Apply posOffset as Local or World space.
        /// </summary>
        private bool followingPosAsLocalSpace;

        private Transform lookingTarget = null;
        /// <summary>
        /// Offset calculated on Local Space.
        /// </summary>
        private Vector3 lookingPosOffset = Vector3.zero;
        /// <summary>
        /// Range: 0.0 ~ 1.0
        /// </summary>
        private float lookingStrength = 1.0f;
        public bool IsLooking => isLooking;
        private bool isLooking = false;
        /// <summary>
        /// Apply posOffset as Local or World space.
        /// </summary>
        private bool lookingPosAsLocalSpace;

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
                if(isFollowing) {
                    Vector3 targetPos;
                    if(followingPosAsLocalSpace) {
                        targetPos = followingTarget.transform.TransformPoint(followingPosOffset);
                    }
                    else {
                        targetPos = followingTarget.transform.position + followingPosOffset;
                    }

                    if(followingStrength < 1) {
                        cam.transform.position = Vector3.Lerp(cam.transform.position, targetPos, followingStrength);
                    }
                    else {
                        cam.transform.position = targetPos;
                    }
                }

                if(isLooking) {
                    Vector3 targetPos;
                    if(lookingPosAsLocalSpace) {
                        targetPos = lookingTarget.transform.TransformPoint(lookingPosOffset);
                    }
                    else {
                        targetPos = lookingTarget.transform.position + lookingPosOffset;
                    }

                    Quaternion lookingRot = Quaternion.LookRotation((targetPos - cam.transform.position).normalized, Vector3.up);
                    if(lookingStrength < 1) {
                        cam.transform.rotation = Quaternion.Lerp(cam.transform.rotation, lookingRot, lookingStrength);
                    }
                    else {
                        cam.transform.rotation = lookingRot;
                    }
                }
            }
        }

        #region Utility
        public void ChangeFollowingPosOffset(Vector3 posOffset) {
            followingPosOffset = posOffset;
        }

        #region StartFollowing Variant
        public void StartFollowing() {
            isFollowing = true;
        }

        public void StartFollowing(Transform target, bool localSpace) {
            SetFollowingProperties(target, Vector3.zero, 1.0f, localSpace);
        }

        public void StartFollowing(Transform target, Vector3 posOffset, bool localSpace) {
            SetFollowingProperties(target, posOffset, 1.0f, localSpace);
        }

        public void StartFollowing(Transform target, float strength, bool localSpace) {
            SetFollowingProperties(target, Vector3.zero, strength, localSpace);
        }

        public void StartFollowing(Transform target, Vector3 posOffset, float strength, bool localSpace) {
            SetFollowingProperties(target, posOffset, strength, localSpace);
        }
        #endregion

        public void StopFollowing() {
            SetFollowingProperties(null, Vector3.zero, 1.0f, false);
        }

        public void PauseFollowing() {
            isFollowing = false;
        }

        #region StartLooking Variant
        public void StartLooking() {
            isLooking = true;
        }

        public void StartLooking(Transform target, bool localSpace) {
            SetLookingProperties(target, Vector3.zero, 1.0f, localSpace);
        }

        public void StartLooking(Transform target, Vector3 posOffset, bool localSpace) {
            SetLookingProperties(target, posOffset, 1.0f, localSpace);
        }

        public void StartLooking(Transform target, float strength, bool localSpace) {
            SetLookingProperties(target, Vector3.zero, strength, localSpace);
        }

        public void StartLooking(Transform target, Vector3 posOffset, float strength, bool localSpace) {
            SetLookingProperties(target, posOffset, strength, localSpace);
        }
        #endregion

        public void StopLooking() {
            SetLookingProperties(null, Vector3.zero, 1.0f, true);
        }

        public void PauseLooking() {
            isLooking = false;
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

        private void SetFollowingProperties(Transform target, Vector3 posOffset, float strength, bool localSpace) {
            followingTarget = target;
            followingPosOffset = posOffset;
            followingStrength = Mathf.Clamp01(strength);
            followingPosAsLocalSpace = localSpace;

            isFollowing = target != null;
        }

        private void SetLookingProperties(Transform target, Vector3 posOffset, float strength, bool localSpace) {
            lookingTarget = target;
            lookingPosOffset = posOffset;
            lookingStrength = Mathf.Clamp01(strength);
            lookingPosAsLocalSpace = localSpace;

            isLooking = target != null;
        }
    }

    public enum CameraMoveSystemType {
        None,


    }

    public enum CameraRotateSystemType {
        None, 

        
    }
}