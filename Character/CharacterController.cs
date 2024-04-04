using Mu3Library.InputHelper;
using Mu3Library.Raycast;
using Mu3Library.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class CharacterController : MonoBehaviour {
        [SerializeField] protected Animator animator;

        [Space(20)]
        [SerializeField] protected Transform cameraTarget;
        public Transform CameraTarget { get => cameraTarget; }

        [Space(20)]
        [SerializeField] protected Rigidbody rigidbody;
        [SerializeField] protected CapsuleCollider collider;
        public float Radius { get => collider.radius; }
        public float Height { get => collider.height; }

        public Vector3 Pos {
            get => transform.position;
            set => transform.position = value;
        }
        public Quaternion Rot {
            get => transform.rotation;
            set => transform.rotation = value;
        }
        public Vector3 Forward {
            get => transform.forward;
            set => transform.forward = value;
        }

        protected CharacterState currentState;
        protected float stateChangeCool = 0.0f;

        [Header("Properties")]
        [SerializeField, Range(0.1f, 10.0f)] protected float moveSpeed = 1.0f;
        [SerializeField, Range(0.1f, 10.0f)] protected float moveBoost = 1.0f;
        [SerializeField, Range(0.1f, 10.0f)] protected float jumpStrength = 1.0f;
        [SerializeField, Range(1.0f, 10.0f)] protected float dashDistance = 2.0f;

        [Space(20)]
        [SerializeField, Range(0.1f, 3.0f)] protected float hitCoolTime = 1.0f;
        protected float hitCool = 0.0f;

        [Space(20)]
        [SerializeField, Range(10, 200)] protected int hpMax = 100;

        public int HP { get; protected set; }

        private FloorContactRayHelper floorContactHelper;
        public float FloorDistance => floorContactHelper.FloorDistance;
        public bool OnFloor => floorContactHelper.OnFloor;

        public CharacterController AttackTarget { get; private set; }

        private Vector3 moveDirection;
        private Vector3 dashDirection;





        protected virtual void Start() {
            int floorContactLayerMask = UtilFunc.GetLayerMask(new string[] { "Player", "Monster" }, true);
            floorContactHelper = new FloorContactRayHelper(transform, 0.1f, 0.1f, floorContactLayerMask);
        }

        protected virtual void Update() {
            if(currentState == null) return;

            if(floorContactHelper != null) {
                floorContactHelper.Raycast();
                if(!floorContactHelper.OnFloor) {

                }
            }

            stateChangeCool = Mathf.Max(stateChangeCool - Time.deltaTime, 0.0f);
            hitCool = Mathf.Max(hitCool - Time.deltaTime, 0.0f);

            // Play With Input
            if(AttackTarget == null) {
                moveDirection = Vector3.zero;
                if(KeyCodeInputCollector.Instance.GetKey(KeyCode.W)) moveDirection += CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKey(KeyCode.A)) moveDirection -= CameraManager.Instance.CameraRightXZ;
                if(KeyCodeInputCollector.Instance.GetKey(KeyCode.S)) moveDirection -= CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKey(KeyCode.D)) moveDirection += CameraManager.Instance.CameraRightXZ;
                moveDirection = moveDirection.normalized;

                dashDirection = Vector3.zero;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(KeyCode.W)) dashDirection += CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(KeyCode.A)) dashDirection -= CameraManager.Instance.CameraRightXZ;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(KeyCode.S)) dashDirection -= CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(KeyCode.D)) dashDirection += CameraManager.Instance.CameraRightXZ;
                dashDirection = dashDirection.normalized;
            }
            // Play Auto
            else {
                moveDirection = transform.forward;
                dashDirection = transform.forward;
            }

            currentState.Update();
        }

        #region Utility
        public virtual void Init() {
            HP = hpMax;
        }

        public virtual void GetHit(int damage, Vector3 attackPoint, float knockbackStrength) {
            
        }

        public virtual void ChangeState(CharacterState state) {
            currentState = state;
        }

        public virtual void SetAttackTarget(CharacterController target) {
            AttackTarget = target;
        }
        #endregion

        #region Action

        #endregion

        #region Animation Func

        #endregion
    }
}