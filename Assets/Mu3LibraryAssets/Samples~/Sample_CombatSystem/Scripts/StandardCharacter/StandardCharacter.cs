using Mu3Library.Attribute;
using Mu3Library.CombatSystem;
using System.Collections;
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
        private const string InputAxes_Fire1 = "Fire1";

        public int HpMax => hpMax;
        [SerializeField, Range(1, 1000)] private int hpMax = 100;

        public int CurrentHp => currentHp;
        private int currentHp = -1;

        public bool IsDead => currentHp <= 0;

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

        public bool AttackInput => attackInput;
        private bool attackInput = false;

        public bool IsHit => isHit;
        private bool isHit = false;

        public System.Action<HitType> OnHit = null;



        #region Utility
        public void ForceChangeHitToFalse() {
            isHit = false;
        }

        public void GetHit(int damage, HitType type) {
            if(currentHp <= 0) {
                return;
            }

            Debug.Log($"Get Hit! damage: {damage}, type: {type}");

            currentHp -= damage;
            currentHp = Mathf.Clamp(currentHp, 0, hpMax);

            isHit = true;

            OnHit?.Invoke(type);
        }

        public void Update() {
            const float groundCastOffsetY = 0.05f;
            groundCheckRay.origin = controller.Position + Vector3.up * groundCastOffsetY;
            if(Physics.Raycast(groundCheckRay, out groundCheckHit, 100.0f, groundCheckLayerMask)) {
                isGrounded = (groundCheckHit.point - controller.Position).magnitude < groundCastOffsetY * 2.0f;
            }
            else {
                isGrounded = false;
            }

            if(controller.IsAutoPlay) {
                #region State Move

                CharacterController near = CombatSystemManager.Instance.GetMostNearCharacter(controller);
                if(near != null) {

                }

                #endregion

                #region State Jump



                #endregion

                #region State Attack

                attackInput = true;

                #endregion
            }
            else {
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

                #region State Attack

                attackInput = Input.GetButtonDown(InputAxes_Fire1);

                #endregion
            }
        }

        public void Init(StandardCharacter controller) {
            moveAxis = Vector2.zero;
            moveDir = Vector3.zero;

            groundCheckLayerMask = ~(1 << CharacterController.Layer);
            groundCheckRay.direction = Vector3.down;

            currentHp = hpMax;

            isHit = false;

            OnHit = null;

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
                case CharacterState.Attack: {
                        return new StandardAttack_Slash(this);
                    }
                case CharacterState.Hit: return new StandardHit(this);

                default: return null;
            }
        }

        protected override void InitProperties() {
            properties.Init(this);
        }

        protected override void UpdateProperties() {
            properties.Update();
        }

        public override void GetHit(int damage, HitType type) {
            properties.GetHit(damage, type);
        }

        #region Animation Event Func

        public override void AnimationEventWithCharacterState(CharacterState s) {
            switch(s) {
                case CharacterState.Attack: {
                        int attackIndex = GetAnimatorParameter_AttackIndex();
                        if(attackIndex == 0) {
                            currentWeapon.Attack(HitType.Normal);
                        }
                        else {
                            Debug.Log($"Undefined Animation Event. state: {s}, attackIndex: {attackIndex}");
                        }
                    }
                    break;

                default: {
                        Debug.Log($"Undefined Animation Event. state: {s}");
                    }
                    break;
            }
        }

        #endregion
    }
}