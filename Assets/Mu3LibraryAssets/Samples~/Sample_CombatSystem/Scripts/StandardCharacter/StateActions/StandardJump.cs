using Mu3Library.CombatSystem;
using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

namespace Mu3Library.Demo.CombatSystem {
    public class StandardJump : ICharacterStateAction {
        private StandardCharacter controller;

        private const string InputAxes_Jump = "Jump";

        /// <summary>
        /// 점프 버튼이 눌렸는지 확인하는 변수
        /// </summary>
        private bool jumpInput = false;
        /// <summary>
        /// 점프를 했는지 확인하는 변수
        /// </summary>
        private bool isJump = false;

        /// <summary>
        /// 캐릭터가 바닥에 닿아있는지 확인하는 변수
        /// </summary>
        private bool isGrounded = false;
        private int jumpCheckLayerMask;
        private Ray jumpCheckRay;
        private RaycastHit jumpCheckHit;



        public StandardJump(StandardCharacter controller) {
            jumpCheckLayerMask = ~(1 << controller.Layer);
            jumpCheckRay.direction = Vector3.down;

            this.controller = controller;
        }

        public void Enter() {
            controller.AddForce(Vector3.up * 5, ForceMode.Impulse);

            isJump = true;
        }

        public bool EnterCheck() {
            return jumpInput && !isJump && isGrounded;
        }

        public void Exit() {


            isJump = false;
        }

        public bool ExitCheck() {
            return isJump && isGrounded;
        }

        public void FixedUpdate() {

        }

        public void LateUpdate() {

        }

        public void Update() {

        }

        public void UpdateAlways() {
            const float posOffsetY = 0.05f;
            jumpCheckRay.origin = controller.transform.position + Vector3.up * posOffsetY;

            isGrounded = Physics.Raycast(jumpCheckRay, out jumpCheckHit, posOffsetY * 2.0f, jumpCheckLayerMask);

            jumpInput = Input.GetButtonDown(InputAxes_Jump);
        }
    }
}