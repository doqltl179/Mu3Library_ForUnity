using Mu3Library.Attribute;
using Mu3Library.CombatSystem;
using System.Collections.Generic;
using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

namespace Mu3Library.Demo.CombatSystem {
    [System.Serializable]
    public class StandardCharacterProperties {
        private StandardCharacter controller;

        private const string InputAxes_Horizontal = "Horizontal";
        private const string InputAxes_Vertical = "Vertical";
        private const string InputAxes_Jump = "Jump";

        /// <summary>
        /// 캐릭터가 바닥에 닿아있는지 확인하는 변수
        /// </summary>
        public bool IsGrounded => isGrounded;
        private bool isGrounded = false;
        private int groundCheckLayerMask;
        private Ray groundCheckRay;
        private RaycastHit groundCheckHit;

        public float MoveSpeed => moveSpeed;
        [Title("State - Move")]
        [SerializeField, Range(0.1f, 10.0f)] private float moveSpeed = 2.0f;

        public float RotateSpeed => rotateSpeed;
        [SerializeField, Range(0.1f, 50.0f)] private float rotateSpeed = 10.0f;

        public float AccelerationLevel => accelerationLevel;
        [SerializeField, Range(0.1f, 10.0f)] private float accelerationLevel = 3.0f;

        public Vector2 MoveAxis => moveAxis;
        private Vector2 moveAxis = Vector2.zero;
        public Vector3 MoveDir => moveDir;
        private Vector3 moveDir = Vector3.zero;

        public float JumpForce => jumpForce;
        [Title("State - Jump")]
        [SerializeField, Range(1.0f, 10.0f)] private float jumpForce = 5.0f;

        /// <summary>
        /// 점프 버튼이 눌렸는지 확인하는 변수
        /// </summary>
        public bool JumpInput => jumpInput;
        private bool jumpInput = false;



        #region Utility
        public void Update() {
            const float groundCastOffsetY = 0.05f;
            groundCheckRay.origin = controller.Position + Vector3.up * groundCastOffsetY;
            if(Physics.Raycast(groundCheckRay, out groundCheckHit, 100.0f, groundCheckLayerMask)) {
                isGrounded = (groundCheckHit.point - controller.Position).magnitude < groundCastOffsetY * 2.0f;
            }
            else {
                isGrounded = false;
            }

            #region State Move

            moveAxis.x = Input.GetAxisRaw(InputAxes_Horizontal);
            moveAxis.y = Input.GetAxisRaw(InputAxes_Vertical);
            moveAxis = moveAxis.normalized;

            // 메인 카메라를 기준으로 움직인다.
            Vector3 mainCameraForward = CombatSystemManager.Instance.MainCameraForward;
            Vector3 forward = new Vector3(mainCameraForward.x, 0, mainCameraForward.z).normalized;

            Vector3 mainCameraRight = CombatSystemManager.Instance.MainCameraRight;
            Vector3 right = new Vector3(mainCameraRight.x, 0, mainCameraRight.z).normalized;

            moveDir = (forward * moveAxis.y + right * moveAxis.x).normalized;

            #endregion

            #region State Jump

            jumpInput = Input.GetButtonDown(InputAxes_Jump);

            #endregion
        }

        public void Init(StandardCharacter controller) {
            moveAxis = Vector2.zero;
            moveDir = Vector3.zero;

            groundCheckLayerMask = ~(1 << controller.Layer);
            groundCheckRay.direction = Vector3.down;

            this.controller = controller;
        }
        #endregion
    }

    public class StandardCharacter : CharacterController {
        public StandardCharacterProperties Properties => properties;
        [SerializeField] private StandardCharacterProperties properties;



        protected override ICharacterStateAction GetDefinedStateAction(CharacterState s) {
            switch(s) {
                case CharacterState.Move: return new StandardMove(this);
                case CharacterState.Jump: return new StandardJump(this);

                default: return null;
            }
        }

        protected override void InitProperties() {
            properties.Init(this);
        }

        protected override void UpdateProperties() {
            properties.Update();
        }
    }
}