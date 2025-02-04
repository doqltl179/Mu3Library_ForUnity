using Mu3Library.CombatSystem;
using UnityEngine;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardJump : ICharacterStateAction {
        private StandardCharacter controller;
        private StandardCharacterProperties properties;

        /// <summary>
        /// 점프를 했는지 확인하는 변수
        /// </summary>
        private bool isJump = false;

        private bool isJumpingUp = false;



        public StandardJump(StandardCharacter controller) {
            this.controller = controller;
            properties = controller.Properties;
        }

        public void Enter() {
            Vector3 currentVelo = controller.LinearVelocity;
            controller.AddForce(Vector3.up * properties.JumpForce, ForceMode.Impulse);
            controller.AddForce(-new Vector3(currentVelo.x, 0, currentVelo.z), ForceMode.Impulse);

            controller.SetAnimatorParameter_IsJump(true);

            isJump = true;
            isJumpingUp = true;
        }

        public bool EnterCheck() {
            return properties.JumpInput && 
                !isJump && 
                !isJumpingUp && 
                properties.IsGrounded && 
                controller.GetAnimatorParameter_AttackMotionIndex() < 0 && 
                !controller.IsAnimatorInTransition();
        }

        public void Exit() {
            Vector3 currentVelo = controller.LinearVelocity;
            controller.AddForce(-new Vector3(currentVelo.x, 0, currentVelo.z), ForceMode.Impulse);

            controller.SetAnimatorParameter_IsJump(false);

            isJump = false;
        }

        public bool ExitCheck() {
            return isJump && !isJumpingUp && properties.IsGrounded;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        public void Update() {
            if(isJumpingUp) {
                if(controller.LinearVelocity.y < -0.0001f) {
                    isJumpingUp = false;
                }
            }
        }

        public void UpdateAlways() {

        }
    }
}