using Mu3Library.Utility;
using Mu3Library.CameraUtil;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class CharacterController : MonoBehaviour, IMove {
        [SerializeField] private CharacterAnimationController animationController;
        [SerializeField] private Rigidbody rigidbody;
        [SerializeField] private CapsuleCollider collider;

        public bool IsKinematic {
            get => rigidbody.isKinematic;
            set => rigidbody.isKinematic = value;
        }

        public float Radius => collider.radius;
        public float Height => collider.height;

        public Vector3 Pos {
            get => transform.position;
            set => transform.position = value;
        }
        public Vector3 Euler {
            get => transform.eulerAngles;
            set => transform.eulerAngles = value;
        }
        public Vector3 Forward {
            get => transform.forward;
            set => transform.forward = value;
        }
        public Vector3 Right {
            get => transform.right;
            set => transform.right = value;
        }
        public Vector3 Up {
            get => transform.up;
            set => transform.up = value;
        }
        public Quaternion Rot {
            get => transform.rotation;
            set => transform.rotation = value;
        }

        /*-----------------------------------------------------------------------------*/

        [Space(20)]
        [SerializeField] private CharacterType type = CharacterType.None;
        public CharacterType Type => type;

        /*-----------------------------------------------------------------------------*/

        public CharacterStateType CurrentStateType {
            get => currentStateType;
            protected set {
                if(currentStateType != value) {
                    if(currentState != null) {
                        currentState.Exit();
                    }

                    CharacterState newState = GetState(value);
                    if(newState != null) {
                        newState.Enter();
                    }

                    OnStateChanged?.Invoke(currentStateType, value);

                    currentState = newState;
                    currentStateType = value;
                }
            }
        }
        protected CharacterStateType currentStateType = CharacterStateType.None;

        protected CharacterState currentState = null;
        public Action<CharacterStateType, CharacterStateType> OnStateChanged;

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

        private Vector3 moveDirection;
        private float moveStrength = 0.0f;



#if UNITY_EDITOR
        private void OnValidate() {
            FindInspectorComponentProperties();
        }

        private void Reset() {
            FindInspectorComponentProperties();
        }

        private void FindInspectorComponentProperties() {
            if(rigidbody == null) rigidbody = GetComponent<Rigidbody>();
            if(collider == null) collider = GetComponentInChildren<CapsuleCollider>();
            if(animationController == null) animationController = GetComponentInChildren<CharacterAnimationController>();
        }
#endif

        protected virtual void Start() {
            animationController.SetValue_CharacterType((int)type);
        }

        private void FixedUpdate() {
            if(currentState != null) {
                currentState.FixedUpdate();
            }
        }

        protected virtual void Update() {
            if(currentState != null) {
                currentState.Update();
            }
        }

        protected virtual CharacterState GetState(CharacterStateType type, object[] param = null) {
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
        public virtual void Move() {
            Vector3 moveDir_f = UtilFunc.GetVec3XZ(CameraManager.Instance.CamForward).normalized;
            Vector3 moveDir_r = UtilFunc.GetVec3XZ(CameraManager.Instance.CamRight).normalized;

            moveDirection = Vector3.zero;
            if(KeyCodeInputCollector.Instance.GetKey(keyCode_moveF)) moveDirection += moveDir_f;
            if(KeyCodeInputCollector.Instance.GetKey(keyCode_moveB)) moveDirection -= moveDir_f;
            if(KeyCodeInputCollector.Instance.GetKey(keyCode_moveL)) moveDirection -= moveDir_r;
            if(KeyCodeInputCollector.Instance.GetKey(keyCode_moveR)) moveDirection += moveDir_r;

            if(moveDirection.magnitude > 0) {
                // Rotate
                //transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(moveDirection), Time.deltaTime * rotateSpeed);
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(moveDirection), Time.fixedDeltaTime * rotateSpeed);

                if(KeyCodeInputCollector.Instance.GetKey(keyCode_run)) {
                    //moveStrength = Mathf.Lerp(moveStrength, 1.0f, moveBoost * Time.deltaTime);
                    moveStrength = Mathf.Lerp(moveStrength, 1.0f, moveBoost * Time.fixedDeltaTime);
                }
                else {
                    //moveStrength = Mathf.Lerp(moveStrength, 0.5f, moveBoost * Time.deltaTime);
                    moveStrength = Mathf.Lerp(moveStrength, 0.5f, moveBoost * Time.fixedDeltaTime);
                }

                moveDirection = moveDirection.normalized;
            }
            else {
                //moveStrength = Mathf.Lerp(moveStrength, 0.0f, moveBoost * 5.0f * Time.deltaTime);
                moveStrength = Mathf.Lerp(moveStrength, 0.0f, moveBoost * 5.0f * Time.fixedDeltaTime);
            }

            // Move
            //rigidbody.position += moveDirection * moveSpeed * moveStrength * Time.deltaTime;
            rigidbody.position += moveDirection * moveSpeed * moveStrength * Time.fixedDeltaTime;

            animationController.SetValue_MoveBlend(moveStrength);
        }
        #endregion

        #region Utility
        public virtual void Play(CharacterStateType playTo = CharacterStateType.Movement) {
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveL);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveR);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveF);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveB);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_run);

            CurrentStateType = playTo;
        }

        public virtual void Stop() {
            KeyCodeInputCollector.Instance.RemoveCollectKey(keyCode_moveL);
            KeyCodeInputCollector.Instance.RemoveCollectKey(keyCode_moveR);
            KeyCodeInputCollector.Instance.RemoveCollectKey(keyCode_moveF);
            KeyCodeInputCollector.Instance.RemoveCollectKey(keyCode_moveB);
            KeyCodeInputCollector.Instance.RemoveCollectKey(keyCode_run);

            CurrentStateType = CharacterStateType.None;
        }
        #endregion

        public enum CharacterType {
            None = 0, 

            Standard = 1, 
        }

        public enum CharacterStateType {
            None,

            Movement,
        }
    }
}