using Mu3Library.CameraUtil;
using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class StandardCharacterController : CharacterController, IMove {



        /*-----------------------------------------------------------------------------*/

        [Space(20)]
        [SerializeField] private KeyCode keyCode_moveL = KeyCode.A;
        [SerializeField] private KeyCode keyCode_moveR = KeyCode.D;
        [SerializeField] private KeyCode keyCode_moveF = KeyCode.W;
        [SerializeField] private KeyCode keyCode_moveB = KeyCode.S;
        [SerializeField] private KeyCode keyCode_run = KeyCode.LeftShift;
        public KeyCode KeyCode_MoveL => keyCode_moveL;
        public KeyCode KeyCode_MoveR => keyCode_moveR;
        public KeyCode KeyCode_MoveF => keyCode_moveF;
        public KeyCode KeyCode_MoveB => keyCode_moveB;
        public KeyCode KeyCode_Run => keyCode_run;

        /*-----------------------------------------------------------------------------*/

        [Space(20)]
        [SerializeField, Range(0.1f, 10.0f)] private float moveSpeed = 2;
        [SerializeField, Range(0.1f, 20.0f)] private float moveBoost = 10.0f;
        [SerializeField, Range(0.1f, 50.0f)] private float rotateSpeed = 6;
        public float MoveSpeed => moveSpeed;
        public float MoveBoost => moveBoost;
        public float RotateSpeed => rotateSpeed;

        private float moveStrength = 0.0f;



        protected override void Start() {
            animationController.SetValue_CharacterType((int)CharacterType.Standard);
        }

        protected override CharacterState GetState(CharacterStateType type, object[] param = null) {
            CharacterState state = null;
            switch(type) {
                case CharacterStateType.None: {
                        state = new StandardCharacterState_None();
                    }
                    break;

                case CharacterStateType.Movement: {
                        state = new StandardCharacterState_Movement();
                    }
                    break;

                default: {
                        Debug.Log($"Not defined 'CharacterState'. type: {type}");
                    }
                    break;
            }

            if(state != null) {
                state.Init(this, param);
            }

            return state;
        }

        #region Interface
        public void Move(Vector2 input, bool isRun = false) {
            Vector3 moveDir_f = UtilFunc.GetVec3XZ(CameraManager.Instance.CamForward).normalized;
            Vector3 moveDir_r = UtilFunc.GetVec3XZ(CameraManager.Instance.CamRight).normalized;

            Vector3 rotatedDir = input.y * moveDir_f + input.x * moveDir_r;

            if(rotatedDir.magnitude > 0) {
                // Rotate
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(rotatedDir), Time.fixedDeltaTime * rotateSpeed);

                if(isRun) {
                    moveStrength = Mathf.Lerp(moveStrength, 1.0f, moveBoost * Time.fixedDeltaTime);
                }
                else {
                    moveStrength = Mathf.Lerp(moveStrength, 0.5f, moveBoost * Time.fixedDeltaTime);
                }

                rotatedDir = rotatedDir.normalized;
            }
            else {
                moveStrength = Mathf.Lerp(moveStrength, 0.0f, moveBoost * 5.0f * Time.fixedDeltaTime);
            }

            // Move
            rigidbody.position += rotatedDir * moveSpeed * moveStrength * Time.fixedDeltaTime;

            animationController.SetValue_MoveBlend(moveStrength);
        }
        #endregion

        #region Utility
        public override void Play(CharacterStateType playTo = CharacterStateType.Movement) {
            base.Play(playTo);
        }

        public override void Stop() {
            base.Stop();
        }
        #endregion
    }
}