using DG.Tweening;
using Mu3Library.Character.Attack;
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

        [Space(20)]
        [SerializeField] protected Transform weaponSlot;
        [SerializeField] protected Mu3Library.Character.Weapon.Weapon currentWeapon;

        protected CharacterState currentState;
        protected float stateChangeCool = 0.0f;
        protected const float StandardStateChangeCoolTime = 0.1f;

        [Header("Properties")]
        [SerializeField, Range(0.1f, 10.0f)] protected float moveSpeed = 1.0f;
        [SerializeField, Range(0.1f, 10.0f)] protected float moveBoost = 1.0f;
        [SerializeField, Range(0.1f, 10.0f)] protected float rotateSpeed = 1.0f;
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
        [SerializeField, Range(10, 20000)] protected int hpMax = 100;
        public int HP { get; protected set; }

        private FloorContactRayHelper floorContactHelper;
        public float FloorDistance => floorContactHelper.FloorDistance;
        public bool OnFloor => floorContactHelper.OnFloor;

        public CharacterController AttackTarget { get; private set; }

        private Vector3 attackTargetDirection;
        private Vector3 attackTargetDirectionXZ;

        public Vector3 AttackTargetDirection => attackTargetDirection;
        public Vector3 AttackTargetDirectionXZ => attackTargetDirectionXZ;

        private struct KnockbackParameters {
            public Vector3 KnockbackDirection;
            public Vector3 KnockbackDirectionXZ;
            public float Strength;
        }
        private KnockbackParameters knockbackParams;

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

        private RaycastHit dashRayHit;
        private RaycastHit knockbackRayHit;

        [HideInInspector] public bool Avoid;



        protected virtual void Start() {
            animationInfo = new AnimationInfo(animator);

            int layerMask = UtilFunc.GetLayerMask("Player", true);
            floorContactHelper = new FloorContactRayHelper(transform, 0.1f, 0.1f, layerMask);
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
            else {
                attackTargetDirection = (AttackTarget.Pos - transform.position).normalized;
                attackTargetDirectionXZ = UtilFunc.GetVec3XZ(attackTargetDirection).normalized;
            }

            currentState.Update();
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
            if(hitCool > 0.0f || Avoid) {
                return;
            }

            HP -= damage;
            Debug.Log($"Get Hit! name: {transform.name}, damage: {damage}, currentHP: {HP}");
            if(HP <= 0) {

            }
            else {
                knockbackParams = new KnockbackParameters {
                    KnockbackDirection = (transform.position - attackPoint).normalized,
                    KnockbackDirectionXZ = UtilFunc.GetVec3XZ(transform.position - attackPoint).normalized, 
                    Strength = knockbackStrength
                };

                Vector3 toAttackPoint = (attackPoint - transform.position).normalized;
                Vector3 toAttackPointXZ = UtilFunc.GetVec3XZ(toAttackPoint).normalized;
                float angle = Vector3.Angle(transform.forward, toAttackPointXZ);
                bool isRight = UtilFunc.IsTargetOnRight(transform.forward, toAttackPointXZ);
                if(angle < 45) animator.SetInteger("HitDirection", 1); //Forward
                else if(angle > 135) animator.SetInteger("HitDirection", 0); //Back
                else if(isRight) animator.SetInteger("HitDirection", 3); //Right
                else animator.SetInteger("HitDirection", 2); //Left

                ChangeState(CharacterStateType.Hit);
            }
        }

        public virtual void SetAttackTarget(CharacterController target) {
            AttackTarget = target;
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

        public void ChangeState(CharacterStateType type, bool ignorCool = false) {
            if(!ignorCool && stateChangeCool > 0) return;

            if(currentState != null) currentState.Exit();

            if(states.TryGetValue(type, out currentState)) {

            }
            else {
                CharacterState changedState = GetNewState(type);
                states.Add(type, changedState);

                currentState = changedState;
            }

            Debug.Log($"{transform.name} || State Change To `{type}`");
            currentState.Enter();

            stateChangeCool = StandardStateChangeCoolTime;
        }

        protected abstract CharacterState GetNewState(CharacterStateType type);



        public float GetCurrentAnimationNormalizedTime(int layer = 0) {
            return animationInfo.GetNormalizedTime(layer);
        }

        public float GetCurrentAnimationNormalizedTimeClamp01(int layer = 0) {
            return animationInfo.GetNormalizedTimeClamp01(layer);
        }

        public int GetPlayingClipCount(int layer = 0) {
            return animationInfo.GetPlayingClipCount(layer);
        }

        public bool IsPlayingAnimationClipWithName(string name) {
            return animationInfo.IsPlayingAnimationClipWithName(name);
        }

        public void SetBool(string parameter, bool value) => animator.SetBool(parameter, value);
        public bool GetBool(string parameter) => animator.GetBool(parameter);

        public void SetFloat(string parameter, float value) => animator.SetFloat(parameter, value);
        public float GetFloat(string parameter) => animator.GetFloat(parameter);

        public void SetInteger(string parameter, int value) => animator.SetInteger(parameter, value);
        public int GetInteger(string parameter) => animator.GetInteger(parameter);



        public void IncreaseMoveSpeedOffset(float max = 1.0f) {
            moveSpeedOffset = Mathf.Lerp(moveSpeedOffset, max, Time.deltaTime * moveBoost);
            animator.SetFloat("MoveBlend", moveSpeedOffset);
        }

        public void DecreaseMoveSpeedOffset() {
            moveSpeedOffset = Mathf.Lerp(moveSpeedOffset, 0.0f, Time.deltaTime * moveBoost);
            animator.SetFloat("MoveBlend", moveSpeedOffset);
        }

        public void UpdateVelocityToMoveSpeedOffset() {
            rigidbody.velocity = UtilFunc.GetVec3XZ(rigidbody.velocity.normalized * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Rotate() {
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(moveDirection), Time.deltaTime * rotateSpeed);
        }

        public void Rotate(Quaternion rotation) {
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * rotateSpeed);
        }

        public void RotateImmediately(Quaternion rotation, float time = 0.0f, Ease ease = Ease.Linear) {
            if(time <= 0.0f) transform.rotation = rotation;
            else transform.DORotateQuaternion(rotation, time).SetEase(ease);
        }

        public void Move() {
            rigidbody.velocity = UtilFunc.GetVec3XZ(moveDirection * moveSpeed * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Move(Vector3 direction) {
            rigidbody.velocity = UtilFunc.GetVec3XZ(direction * moveSpeed * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Jump() {
            rigidbody.velocity += Vector3.up * jumpStrength;
        }

        public void Dash() {
            Dash(dashDirection, dashDistance, 0.3f, UtilFunc.GetLayerMask(new string[] { "Monster", "Props" }));
        }

        public void Dash(Vector3 direction, float distance, float moveTime, int mask, Ease ease = Ease.OutQuad) {
            Vector3 dashEndPos = transform.position + direction * distance;

            Vector3 p1 = dashEndPos;
            Vector3 p2 = p1 + Vector3.up * Height;
            if(Physics.CapsuleCast(p1, p2, Radius, Vector3.forward, out dashRayHit, 0.0f, mask)) {
                p1 = transform.position;
                p2 = p1 + Vector3.up * Height;
                if(Physics.CapsuleCast(p1, p2, Radius, direction, out dashRayHit, distance, mask)) {
                    float newDashDistance = UtilFunc.GetDistanceXZ(transform.position, dashRayHit.point);
                    dashEndPos = transform.position + direction * newDashDistance;
                }
            }

            transform.DOMove(dashEndPos, moveTime).SetEase(Ease.OutQuad);
            transform.DORotateQuaternion(Quaternion.LookRotation(direction), moveTime * 0.33f).SetEase(ease);
        }

        public void Knockback() {
            Knockback(
                knockbackParams.KnockbackDirectionXZ,
                knockbackParams.Strength, 
                0.2f, 
                UtilFunc.GetLayerMask(new string[] { "Monster", "Props" }));
        }

        public void Knockback(Vector3 direction, float strength, float time, int mask, Ease ease = Ease.OutQuad) {
            Vector3 knockbackEndPos = transform.position + direction * strength;

            Vector3 p1 = knockbackEndPos;
            Vector3 p2 = p1 + Vector3.up * Height;
            if(Physics.CapsuleCast(p1, p1, Radius, Vector3.forward, out knockbackRayHit, 0.0f, mask)) {
                p1 = transform.position;
                p2 = p1 + Vector3.up * Height;
                if(Physics.CapsuleCast(p1, p2, Radius, direction, out knockbackRayHit, strength, mask)) {
                    float newDashDistance = UtilFunc.GetDistanceXZ(transform.position, dashRayHit.point);
                    knockbackEndPos = transform.position + direction * newDashDistance;
                }
            }

            transform.DOMove(knockbackEndPos, time).SetEase(ease);
        }

        public bool TargetInRange(AttackInfo info, CharacterController target) {
            float dist = UtilFunc.GetDistanceXZ(transform.position, target.Pos);
            if(dist < info.RangeMin || info.RangeMax < dist) return false;
            Debug.Log($"dist: {dist}, min: {info.RangeMin}, max: {info.RangeMax}");

            Vector3 directionToTarget = (target.Pos - transform.position).normalized;
            Vector3 directionToTargetXZ = UtilFunc.GetVec3XZ(directionToTarget).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTargetXZ);
            if(angle > info.AngleDeg * 0.5f) return false;

            float heightDiff = target.Pos.y - transform.position.y;
            if(heightDiff < info.HeightMin || info.HeightMax < heightDiff) return false;

            return true;
        }
        #endregion

            #region Animation Func
        public void ActivateWeaponAttackPoint() {
            if(currentWeapon != null) currentWeapon.ActivateAttackPoint();
        }

        public void DeactivateWeaponAttackPoint() {
            if(currentWeapon != null) currentWeapon.DeactivateAttackPoint();
        }
        #endregion
    }
}