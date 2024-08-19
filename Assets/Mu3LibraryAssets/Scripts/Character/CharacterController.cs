using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public abstract class CharacterController : MonoBehaviour {
        [SerializeField] protected CharacterAnimationController animationController;
        [SerializeField] protected Rigidbody rigidbody;
        [SerializeField] protected CapsuleCollider collider;

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
        [SerializeField] private CharacterType currentType = CharacterType.None;
        public CharacterType CurrentType => currentType;

        /*-----------------------------------------------------------------------------*/

        public CharacterStateType CurrentStateType => currentStateType;
        protected CharacterStateType currentStateType = CharacterStateType.None;

        protected CharacterState currentState = null;
        public Action<CharacterStateType, CharacterStateType> OnStateChanged;



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
            animationController.SetValue_CharacterType((int)currentType);
        }

        protected virtual void FixedUpdate() {
            if(currentState != null) {
                currentState.FixedUpdate();
            }
        }

        protected virtual void Update() {
            if(currentState != null) {
                currentState.Update();
            }
        }

        protected virtual void LateUpdate() {
            if(currentState != null) {
                currentState.LateUpdate();
            }
        }

        protected abstract CharacterState GetState(CharacterStateType type, object[] param = null);

        #region Utility
        public virtual void ChangeCharacterType(CharacterType type) {
            if(type != currentType) {
                animationController.SetValue_CharacterType((int)type);

                currentType = type;
            }
        }

        public virtual void Play(CharacterStateType playTo = CharacterStateType.Movement) {
            ChangeCharacterStateType(playTo);
        }

        public virtual void Stop() {
            ChangeCharacterStateType(CharacterStateType.None);
        }
        #endregion

        private void ChangeCharacterStateType(CharacterStateType changeTo) {
            if(currentStateType != changeTo) {
                if(currentState != null) {
                    currentState.Exit();
                }

                CharacterState newState = GetState(changeTo);
                if(newState != null) {
                    newState.Enter();
                }

                OnStateChanged?.Invoke(currentStateType, changeTo);

                currentState = newState;
                currentStateType = changeTo;
            }
        }

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