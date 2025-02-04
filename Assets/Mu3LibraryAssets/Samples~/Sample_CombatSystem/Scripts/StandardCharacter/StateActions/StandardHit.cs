using Mu3Library.CombatSystem;
using UnityEngine;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardHit : ICharacterStateAction {
        private StandardCharacter controller;
        private StandardCharacterProperties properties;

        private const float exitTime = 0.86f;
        private const string animationNameRoot = "Hit";

        private bool isInHit = false;



        public StandardHit(StandardCharacter controller) {
            this.controller = controller;
            properties = controller.Properties;

            isInHit = false;

            properties.OnHit += OnHit;
        }

        #region Utility
        public void Enter() {
            controller.SetAnimatorParameter_HitTrigger();
        }

        public bool EnterCheck() {
            return isInHit;
        }

        public void Exit() {
            isInHit = false;

            properties.ForceChangeHitToFalse();

            controller.SetAnimatorParameter_ReturnToFirstAnimation();
        }

        public bool ExitCheck() {
            AnimatorClipInfo[] currentClipInfos = controller.GetCurrentAnimatorClipInfo();
            if(currentClipInfos.Length >= 2) {
                return false;
            }

            AnimatorStateInfo currentStateInfo = controller.GetCurrentAnimatorStateInfo();
            if(!currentStateInfo.IsName(animationNameRoot)) {
                return true;
            }

            float normalizedTime = currentStateInfo.normalizedTime;

            return normalizedTime > exitTime;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        public void Update() {

        }

        public void UpdateAlways() {

        }
        #endregion

        private void OnHit(HitType type) {
            isInHit = true;
        }
    }
}