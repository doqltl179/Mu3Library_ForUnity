using Mu3Library.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CombatSystem {
    /*
     * ------------------------------------------------------------------
     * ┌---------------------┐
     * | CombatSystemManager |
     * └---------------------┘
     *            |            \
     * ┌---------------------┐   ┌---------------------┐
     * | CameraSystemManager |   | CharacterController |
     * └---------------------┘   └---------------------┘
     * 
     * 
     * 
     * 
     * 
     * 
     * 
     * ------------------------------------------------------------------
     */
    public class CombatSystemManager : Singleton<CombatSystemManager> {
        [SerializeField] private CharacterController player;
        [SerializeField] private CameraSystemManager cameraSystem;

        #region Public Get Properties

        public Vector3 MainCameraPosition => cameraSystem.MainCameraPosition;
        public Vector3 MainCameraEulerAngles => cameraSystem.MainCameraEulerAngles;
        public Quaternion MainCameraRotation => cameraSystem.MainCameraRotation;

        public Vector3 MainCameraForward => cameraSystem.MainCameraForward;
        public Vector3 MainCameraRight => cameraSystem.MainCameraRight;

        #endregion

        [Space(20)]
        /// <summary>
        /// 아군 리스트
        /// </summary>
        [SerializeField] private List<CharacterController> playerAllies;
        /// <summary>
        /// 적군 리스트
        /// </summary>
        [SerializeField] private List<CharacterController> enemyForces;




        private void FixedUpdate() {
            if(player != null) {
                player.FixedUpdateAction();
            }

            foreach(CharacterController c in playerAllies) {
                c.FixedUpdateAction();
            }
            foreach(CharacterController c in enemyForces) {
                c.FixedUpdateAction();
            }
        }

        // Update 함수에서 카메라를 움직여준다.
        private void Update() {
            if(player != null) {
                player.UpdateAction();
            }

            // 카메라 이동 및 회전
            if(cameraSystem != null) {
                cameraSystem.UpdateAction();
            }

            foreach(CharacterController c in playerAllies) {
                c.UpdateAction();
            }
            foreach(CharacterController c in enemyForces) {
                c.UpdateAction();
            }
        }

        private void LateUpdate() {
            if(player != null) {
                player.LateUpdateAction();
            }

            foreach(CharacterController c in playerAllies) {
                c.LateUpdateAction();
            }
            foreach(CharacterController c in enemyForces) {
                c.LateUpdateAction();
            }
        }

        #region Utility
        public void ChangeCameraStateToFollowPlayerCharacter(Camera camera) {
            if(cameraSystem == null || player == null) {
                return;
            }

            cameraSystem.ChangeCameraStateToFollowCharacter(camera, player);
        }

        public void InitSystem() {
            if(player != null) {
                player.Init();
                player.IsPlayer = true;
            }

            if(cameraSystem != null) {
                cameraSystem.ChangeCameraStateToNone();
            }
            else {
                Debug.LogWarning($"Camera System not found.");
            }

            foreach(CharacterController ally in playerAllies) {
                ally.Init();
                ally.IsPlayer = false;
            }

            foreach(CharacterController enemy in enemyForces) {
                enemy.Init();
                enemy.IsPlayer = false;
            }
        }
        #endregion
    }
}
