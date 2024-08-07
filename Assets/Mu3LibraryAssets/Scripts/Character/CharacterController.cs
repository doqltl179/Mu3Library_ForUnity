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

        public Dictionary<CharacterStateType, CharacterState> states = new Dictionary<CharacterStateType, CharacterState>();

        public CharacterStateType CurrentStateType {
            get => currentStateType;
            protected set {
                if(currentStateType != value) {
                    currentState?.Exit();

                    currentState = GetState(value);
                    currentState.Enter();

                    OnStateChanged?.Invoke(currentStateType, value);

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
        [SerializeField, Range(0.1f, 10.0f)] private float moveBoost = 0.6f;
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

        protected virtual void Update() {
            currentState?.Update();
        }

        protected virtual CharacterState GetState(CharacterStateType type) {
            if(!states.ContainsKey(type)) {
                CharacterState state = null;
                switch(type) {
                    case CharacterStateType.Movement: {
                            state = new StandardCharacterState_Movement();
                            state.Init(this);
                        }
                        break;

                    default: {
                            state = new StandardCharacterState_None();
                            state.Init(this);
                        }
                        break;
                }

                states.Add(type, state);
            }

            return states[type];
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
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(moveDirection), Time.deltaTime * rotateSpeed);

                if(KeyCodeInputCollector.Instance.GetKey(keyCode_run)) {
                    moveStrength = Mathf.Clamp01(moveStrength + moveBoost * Time.deltaTime);
                }
                else {
                    moveStrength = Mathf.Clamp(moveStrength + moveBoost * Time.deltaTime, 0.0f, 0.5f);
                }

                moveDirection = moveDirection.normalized;
            }
            else {
                moveStrength = Mathf.Clamp01(moveStrength - moveBoost * 5.0f * Time.deltaTime);
            }

            // Move
            rigidbody.position += moveDirection * moveSpeed * moveStrength * Time.deltaTime;

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