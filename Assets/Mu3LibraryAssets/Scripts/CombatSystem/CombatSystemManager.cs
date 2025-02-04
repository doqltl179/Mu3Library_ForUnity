using Mu3Library.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.CombatSystem {
    /// <summary>
    /// 해당 Combat System은 오픈 월드가 아닌 턴제, 혹은 단판 승부를 가리는 상황에서 사용하기에 적합하다.
    /// </summary>
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

        public CharacterController GetMostNearCharacter(CharacterController from) {
            bool isEnemySide = false;
            if(enemyForces.Any(t => t == from)) {
                isEnemySide = true;
            }

            CharacterController near = null;

            if(isEnemySide) {
                if(playerAllies.Count > 0) {
                    near = playerAllies[0];
                    for(int i = 1; i < playerAllies.Count; i++) {
                        float currentDist = Vector3.Distance(from.Position, near.Position);
                        float compareDist = Vector3.Distance(from.Position, playerAllies[i].Position);
                        if(compareDist > currentDist) {
                            near = playerAllies[i];
                        }
                    }
                }
            }
            else {
                if(enemyForces.Count > 0) {
                    near = enemyForces[0];
                    for(int i = 1; i < playerAllies.Count; i++) {
                        float currentDist = Vector3.Distance(from.Position, near.Position);
                        float compareDist = Vector3.Distance(from.Position, enemyForces[i].Position);
                        if(compareDist > currentDist) {
                            near = enemyForces[i];
                        }
                    }
                }
            }

            return near;
        }

        public void InitSystem() {
            if(player != null) {
                player.Init(false);
                player.IsPlayer = true;
            }

            if(cameraSystem != null) {
                cameraSystem.ChangeCameraStateToNone();
            }
            else {
                Debug.LogWarning($"Camera System not found.");
            }

            foreach(CharacterController ally in playerAllies) {
                ally.Init(true);
                ally.IsPlayer = false;
            }

            foreach(CharacterController enemy in enemyForces) {
                enemy.Init(true);
                enemy.IsPlayer = false;
            }
        }
        #endregion
    }
}
