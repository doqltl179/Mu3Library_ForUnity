using Mu3Library.InputHelper;
using Mu3Library.Raycast;
using Mu3Library.Utility;
using System.Collections.Generic;
using UnityEngine;

using AnimationInfo = Mu3Library.Animation.AnimationInfo;

namespace Mu3Library.Character {
    public abstract class CharacterController : MonoBehaviour {
        protected Dictionary<CharacterStateType, CharacterState> states;

        protected KeyCode inputForward = KeyCode.W;
        protected KeyCode inputLeft = KeyCode.A;
        protected KeyCode inputBack = KeyCode.S;
        protected KeyCode inputRight = KeyCode.D;
        protected KeyCode inputRun = KeyCode.LeftShift;
        protected KeyCode inputJump = KeyCode.Space;
        protected KeyCode inputAttack = KeyCode.Mouse0;

        [SerializeField] protected Animator animator;
        public Animator Animator => animator;
        protected AnimationInfo animationInfo;

        [Space(20)]
        [SerializeField] protected Transform cameraTarget;
        public Transform CameraTarget => cameraTarget;

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
        public Vector3 Velocity {
            get => rigidbody.velocity;
            set => rigidbody.velocity = value;
        }

        public bool IsPlaying { get; protected set; }
        public bool PlayAuto { get; protected set; }

        protected CharacterState currentState;
        protected float stateChangeCool = 0.0f;

        [Header("Properties")]
        [SerializeField, Range(0.1f, 10.0f)] protected float moveSpeed = 1.0f;
        [SerializeField, Range(0.1f, 10.0f)] protected float moveBoost = 1.0f;
        [SerializeField, Range(0.1f, 10.0f)] protected float jumpStrength = 1.0f;
        [SerializeField, Range(1.0f, 10.0f)] protected float dashDistance = 2.0f;
        public float MoveSpeed => moveSpeed;
        public float MoveBoost => moveBoost;
        public float JumpStrength => jumpStrength;
        public float DashDistance => dashDistance;
        public float moveSpeedOffset { get; protected set; }

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
        private bool isInputRun;
        private bool isInputJump;
        private bool isInputAttack;

        public Vector3 MoveDirection => moveDirection;
        public Vector3 DashDirection => dashDirection;
        public bool IsInputRun => isInputRun;
        public bool IsInputJump => isInputJump;
        public bool IsInputAttack => isInputAttack;



        protected virtual void Start() {
            animationInfo = new AnimationInfo(animator);

            int floorContactLayerMask = UtilFunc.GetLayerMask(new string[] { "Player", "Monster" }, true);
            floorContactHelper = new FloorContactRayHelper(transform, 0.1f, 0.1f, floorContactLayerMask);
        }

        protected virtual void Update() {
            if(currentState == null) return;

            if(animationInfo != null) animationInfo.UpdateStateInfoAll();

            if(floorContactHelper != null) floorContactHelper.Raycast();

            stateChangeCool = Mathf.Max(stateChangeCool - Time.deltaTime, 0.0f);
            hitCool = Mathf.Max(hitCool - Time.deltaTime, 0.0f);

            // Play With Input
            if(AttackTarget == null) {
                moveDirection = Vector3.zero;
                if(KeyCodeInputCollector.Instance.GetKey(inputForward)) moveDirection += CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKey(inputLeft)) moveDirection -= CameraManager.Instance.CameraRightXZ;
                if(KeyCodeInputCollector.Instance.GetKey(inputBack)) moveDirection -= CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKey(inputRight)) moveDirection += CameraManager.Instance.CameraRightXZ;
                moveDirection = moveDirection.normalized;

                dashDirection = Vector3.zero;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(inputForward)) dashDirection += CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(inputLeft)) dashDirection -= CameraManager.Instance.CameraRightXZ;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(inputBack)) dashDirection -= CameraManager.Instance.CameraForwardXZ;
                if(KeyCodeInputCollector.Instance.GetKeyDoubleDown(inputRight)) dashDirection += CameraManager.Instance.CameraRightXZ;
                dashDirection = dashDirection.normalized;

                isInputRun = KeyCodeInputCollector.Instance.GetKey(inputRun);

                isInputJump = KeyCodeInputCollector.Instance.GetKey(inputJump);

                isInputAttack = KeyCodeInputCollector.Instance.GetKey(inputAttack);
            }

            currentState.Update();

            if(moveDirection.magnitude <= 0) {
                rigidbody.velocity = UtilFunc.GetVec3XZ(rigidbody.velocity.normalized * moveSpeedOffset, rigidbody.velocity.y);
            }
        }

        #region Utility
        public virtual void Play(bool playAuto) {
            IsPlaying = true;
            PlayAuto = playAuto;

            ChangeState(CharacterStateType.Idle);
        }

        public virtual void Init() {
            if(currentState != null) {
                currentState.Exit();
                currentState = null;
            }
            states = new Dictionary<CharacterStateType, CharacterState>();

            HP = hpMax;
        }

        public virtual void GetHit(int damage, Vector3 attackPoint, float knockbackStrength) {
            
        }

        public void SetInput(
            KeyCode forward, 
            KeyCode left, 
            KeyCode back, 
            KeyCode right, 
            KeyCode run, 
            KeyCode jump, 
            KeyCode attack) {
            inputForward = forward;
            inputLeft = left;
            inputBack = back;
            inputRight = right;
            inputRun = run;
            inputJump = jump;
            inputAttack = attack;
        }

        public void ChangeState(CharacterStateType type) {
            if(currentState != null) currentState.Exit();

            if(states.TryGetValue(type, out currentState)) {

            }
            else {
                CharacterState changedState = GetState(type);
                states.Add(type, changedState);

                currentState = changedState;
            }

            currentState.Enter();
        }

        protected abstract CharacterState GetState(CharacterStateType type);

        public virtual void SetAttackTarget(CharacterController target) {
            AttackTarget = target;
        }

        public void IncreaseMoveSpeedOffset(float max = 1.0f) {
            moveSpeedOffset = Mathf.Lerp(moveSpeedOffset, max, Time.deltaTime * moveBoost);
            animator.SetFloat("MoveBlend", moveSpeedOffset);
        }

        public void DecreaseMoveSpeedOffset() {
            moveSpeedOffset = Mathf.Lerp(moveSpeedOffset, 0.0f, Time.deltaTime * moveBoost);
            animator.SetFloat("MoveBlend", moveSpeedOffset);
        }

        public void Rotate() {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(moveDirection), Time.deltaTime * 30.0f);
        }

        public void Move() {
            rigidbody.velocity = UtilFunc.GetVec3XZ(moveDirection * moveSpeed * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Jump() {
            rigidbody.velocity += Vector3.up * jumpStrength;
        }

        public void Dash() {

        }

        public bool CanDash() {
            return true;
        }
        #endregion
    }
}