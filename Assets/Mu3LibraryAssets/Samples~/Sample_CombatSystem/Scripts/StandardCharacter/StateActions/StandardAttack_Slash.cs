using Mu3Library.CombatSystem;
using UnityEngine;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardAttack_Slash : ICharacterStateAction {
        private StandardCharacter controller;
        private StandardCharacterProperties properties;

        /// <summary>
        /// Slash 애니메이션이 3분할로 되어 있다.
        /// </summary>
        private const int slashCount = 3;
        private const string animationNameRoot = "Slash";

        private const float motionContinueNormalizedTimeMin = 0.8f;
        private const float motionContinueNormalizedTimeMax = 0.96f;

        private int motionIndex = -1;

        private bool motionContinue = true;
        private bool exit = false;



        public StandardAttack_Slash(StandardCharacter controller) {
            this.controller = controller;
            properties = controller.Properties;
        }

        #region Utility
        public void Enter() {
            motionIndex = 0;

            motionContinue = false;
            exit = false;

            controller.SetAnimatorParameter_AttackMotionIndex(motionIndex);
        }

        public bool EnterCheck() {
            return properties.IsGrounded && properties.AttackInput;
        }

        public void Exit() {
            motionIndex = -1;

            controller.SetAnimatorParameter_AttackMotionIndex(motionIndex);
        }

        public bool ExitCheck() {
            return exit;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        public void Update() {
            AnimatorStateInfo currentStateInfo = controller.GetCurrentAnimatorStateInfo();
            if(!currentStateInfo.IsName($"{animationNameRoot}{motionIndex:D2}")) {
                return;
            }

            float normalizedTime = currentStateInfo.normalizedTime;
            if(normalizedTime < motionContinueNormalizedTimeMin) {
                motionContinue = false;
                exit = false;
            }
            else if(normalizedTime < motionContinueNormalizedTimeMax) {
                if(motionIndex < slashCount - 1 && properties.AttackInput) {
                    motionIndex++;

                    motionContinue = true;

                    controller.SetAnimatorParameter_AttackMotionIndex(motionIndex);
                }
            }
            else {
                if(!motionContinue) {
                    exit = true;
                }
            }
        }

        public void UpdateAlways() {

        }
        #endregion
    }
}