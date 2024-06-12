using Mu3Library.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class CharacterController : MonoBehaviour {
        [SerializeField] private AnimationController animationController;
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

        public Dictionary<CharacterStateType, CharacterState> states = new Dictionary<CharacterStateType, CharacterState>();
        public CharacterState CurrentState {
            get => currentState;
            set {
                if(currentState != value) {
                    OnStateChanged?.Invoke(currentState, value);

                    currentState = value;
                }
            }
        }
        protected CharacterState currentState = null;
        public Action<CharacterState, CharacterState> OnStateChanged;

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
        [SerializeField, Range(0.1f, 10.0f)] private float moveBoost = 0.4f;
        [SerializeField, Range(0.1f, 50.0f)] private float rotateSpeed = 6;
        public float MoveSpeed => moveSpeed;
        public float MoveBoost => moveBoost;
        public float RotateSpeed => rotateSpeed;
        public float MoveBlend {
            get => animationController.GetValue_MoveBlend();
            set => animationController.SetValue_MoveBlend(value);
        }



        protected virtual void Awake() {
            OnStateChanged += StateChanged;
        }

        protected virtual void OnDestroy() {
            OnStateChanged -= StateChanged;
        }


        protected virtual void Start() {
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveL);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveR);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveF);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_moveB);
            KeyCodeInputCollector.Instance.AddCollectKey(keyCode_run);

            CurrentState = GetState(CharacterStateType.Movement);
        }

        protected virtual void Update() {
            if(currentState != null) {
                currentState.Update();
            }
        }

        protected virtual CharacterState GetState(CharacterStateType type) {
            CharacterState state = null;
            if(states.TryGetValue(type, out state)) {

            }
            else {
                switch(type) {


                }

                state?.Init(this);
            }

            return state;
        }

        #region Utility
        public virtual void Move(Vector3 dir, float strength) {
            rigidbody.position += dir * strength;
        }
        #endregion

        #region Action
        protected virtual void StateChanged(CharacterState from, CharacterState to) {
            from?.Exit();
            to?.Enter();
        }
        #endregion
    }
}