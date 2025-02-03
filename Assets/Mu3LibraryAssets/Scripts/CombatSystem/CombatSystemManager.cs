using Mu3Library.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CombatSystem {
    public class CombatSystemManager : Singleton<CombatSystemManager> {
        /// <summary>
        /// 아군 리스트
        /// </summary>
        [SerializeField] private List<CharacterController> playerAllies;
        /// <summary>
        /// 적군 리스트
        /// </summary>
        [SerializeField] private List<CharacterController> enemyForces;

        [Space(20)]
        [SerializeField] private Camera playerCamera;



        #region Utility
        public Camera GetPlayerCamera() {
            return playerCamera;
        }

        public void InitSystem() {
            foreach(CharacterController ally in playerAllies) {
                ally.Init();
            }

            foreach(CharacterController enemy in enemyForces) {
                enemy.Init();
            }
        }
        #endregion
    }
}
