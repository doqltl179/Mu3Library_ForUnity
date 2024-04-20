using System;
using DG.Tweening;
using Mu3Library.InputHelper;
using Mu3Library.Raycast;
using Mu3Library.Utility;
using System.Collections.Generic;
using UnityEngine;

using AnimationInfo = Mu3Library.Animation.AnimationInfo;

namespace Mu3Library.Character {
    [RequireComponent(typeof(Rigidbody))]
    public abstract class CharacterController : MonoBehaviour {
        protected Dictionary<CharacterStateType, CharacterState> states;
        public CharacterState CurrentState { get; protected set; }

        protected KeyCode inputForward = KeyCode.W;
        protected KeyCode inputLeft = KeyCode.A;
        protected KeyCode inputBack = KeyCode.S;
        protected KeyCode inputRight = KeyCode.D;
        protected KeyCode inputRun = KeyCode.LeftShift;
        protected KeyCode inputJump = KeyCode.Space;
        protected KeyCode inputAttack = KeyCode.Mouse0;

        [SerializeField] protected Animator animator;
        protected AnimationInfo animationInfo;

        [SerializeField] protected CharacterType characterType;
        public string CharacterTypeString => characterType.ToString();

        public int Layer => gameObject.layer;
        public string LayerName => LayerMask.LayerToName(gameObject.layer);

        [Space(20)]
        [SerializeField] protected Transform cameraTarget;
        public Transform CameraTarget => cameraTarget;

        [Space(20)]
        [SerializeField] protected Rigidbody rigidbody;
        [SerializeField] protected CapsuleCollider collider;
        public Rigidbody Rigidbody => rigidbody;
        public CapsuleCollider Collider => collider;
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
        public Vector3 MiddlePos => transform.position + transform.up * Height;

        public bool IsPlaying { get; protected set; }
        public bool PlayAuto { get; protected set; }

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

        public CharacterController FollowTarget { get; private set; }

        private float dotAT;
        private float followTargetAngleDeg;
        private float followTargetDistance;
        private float followTargetDistanceXZ;
        private Vector3 followTargetDirection;
        private Vector3 followTargetDirectionXZ;
        private Quaternion followTargetLookRotation;
        private Quaternion followTargetLookRotationXZ;

        public float DotAT => dotAT;
        public float FollowTargetAngleDeg => followTargetAngleDeg;
        public float FollowTargetDistance => followTargetDistance;
        public float FollowTargetDistanceXZ => followTargetDistanceXZ;
        public Vector3 FollowTargetDirection => followTargetDirection;
        public Vector3 FollowTargetDirectionXZ => followTargetDirectionXZ;
        public Quaternion FollowTargetLookRotation => followTargetLookRotation;
        public Quaternion FollowTargetLookRotationXZ => followTargetLookRotationXZ;

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
        public int LayerMask_ExcludeThis { get; protected set; }
        public int LayerMask_ExcludeThisAndFloor { get; protected set; }
        public int LayerMask_OnlyTarget { get; protected set; }
        public int LayerMask_ExcludeCharacter { get; protected set; }

        [HideInInspector] public bool Avoid; //Å¸°Ý È¸ÇÇ
        [HideInInspector] public bool SuperArmour; //³Ë¹é Äµ½½



        protected virtual void Start() {
            animationInfo = new AnimationInfo(animator);
            animator.SetInteger("CharacterType", (int)characterType);
        }

        protected virtual void Update() {
            if(CurrentState == null) return;

            if(animationInfo != null) animationInfo.UpdateStateInfoAll();

            if(floorContactHelper != null) floorContactHelper.Raycast();

            hitCool = Mathf.Max(hitCool - Time.deltaTime, 0.0f);

            // Play With Input
            if(FollowTarget == null) {
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

                isInputJump = KeyCodeInputCollector.Instance.GetKeyDown(inputJump);

                isInputAttack = KeyCodeInputCollector.Instance.GetKeyDown(inputAttack);
            }
            else {
                followTargetDistance = Vector3.Distance(FollowTarget.Pos, transform.position);
                followTargetDistanceXZ = UtilFunc.GetDistanceXZ(FollowTarget.Pos, transform.position);
                followTargetDirection = (FollowTarget.Pos - transform.position).normalized;
                followTargetDirectionXZ = UtilFunc.GetVec3XZ(followTargetDirection).normalized;
                followTargetLookRotation = Quaternion.LookRotation(followTargetDirection);
                followTargetLookRotationXZ = Quaternion.LookRotation(followTargetDirectionXZ);
                dotAT = Vector3.Dot(followTargetDirectionXZ, transform.forward);
                followTargetAngleDeg = Mathf.Max(Mathf.Acos(dotAT) * Mathf.Rad2Deg, 0.0f);
            }

            CurrentState.Update();
        }

        #region Utility
        public virtual void Play(bool playAuto) {
            IsPlaying = true;
            PlayAuto = playAuto;

            ChangeState(CharacterStateType.Idle);
        }

        public virtual void Init() {
            LayerMask_OnlyTarget = characterType == CharacterType.PlayerCharacter ?
                UtilFunc.GetLayerMask(CharacterType.PlayerCharacter.ToString()) :
                UtilFunc.GetLayerMask(CharacterType.OtherCharacter.ToString());
            LayerMask_ExcludeThis = ~LayerMask_OnlyTarget;
            LayerMask_ExcludeThisAndFloor = ~(LayerMask_OnlyTarget | (1 << LayerMask.NameToLayer("Floor")));
            LayerMask_ExcludeCharacter = UtilFunc.GetLayerMask(
                new string[] {
                    CharacterType.PlayerCharacter.ToString(),
                    CharacterType.OtherCharacter.ToString()
                }, true);

            floorContactHelper = new FloorContactRayHelper(transform, 0.1f, 0.05f, LayerMask_ExcludeThis);

            if(CurrentState != null) {
                CurrentState.Exit();
                CurrentState = null;
            }
            states = new Dictionary<CharacterStateType, CharacterState>();

            HP = hpMax;
        }

        public void SetLayer(string layerName) {
            SetLayer(LayerMask.NameToLayer(layerName));
        }

        public void SetLayer(int layer) {
            UtilFunc.SetLayerWithChildren(transform, layer);
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
                if(!SuperArmour && knockbackStrength > 0 && floorContactHelper.OnFloor) {
                    Vector3 toAttackPoint = (attackPoint - transform.position).normalized;
                    Vector3 toAttackPointXZ = UtilFunc.GetVec3XZ(toAttackPoint).normalized;
                    float angle = Vector3.Angle(transform.forward, toAttackPointXZ);
                    bool isRight = UtilFunc.IsTargetOnRight(transform.forward, toAttackPointXZ);
                    if(angle < 45) animator.SetInteger("HitDirection", 1); //Forward
                    else if(angle > 135) animator.SetInteger("HitDirection", 0); //Back
                    else if(isRight) animator.SetInteger("HitDirection", 3); //Right
                    else animator.SetInteger("HitDirection", 2); //Left

                    Knockback(
                        UtilFunc.GetVec3XZ(transform.position - attackPoint).normalized,
                        knockbackStrength,
                        0.2f,
                        LayerMask_ExcludeThis);

                    ChangeState(CharacterStateType.Hit);
                }

                hitCool = hitCoolTime;
            }
        }

        public virtual void SetFollowTarget(CharacterController target) {
            FollowTarget = target;
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
            CharacterState changeTo = null;
            if(states.TryGetValue(type, out changeTo)) {

            }
            else {
                CharacterState changedState = GetNewState(type);
                states.Add(type, changedState);

                changeTo = changedState;
            }

            if(changeTo != CurrentState) {
                if(CurrentState != null) CurrentState.Exit();
                changeTo.Enter();
            }
            else {
                changeTo.ReEnter();
            }

            Debug.Log($"{transform.name} || State Change To `{type}`");
            CurrentState = changeTo;
        }

        protected abstract CharacterState GetNewState(CharacterStateType type);



        public void PlayAnimation(string name, int layer = 0, float normalizedTime = 0.0f) {
            animationInfo.PlayAnimation(name, layer, normalizedTime);
        }

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

        public bool IsTransitioning(int layer = 0) {
            return animationInfo.IsTransitioning(layer);
        }

        public void SetTrigger(string parameter) => animator.SetTrigger(parameter);
        public void ResetTrigger(string parameter) => animator.ResetTrigger(parameter);

        public void SetBool(string parameter, bool value) => animator.SetBool(parameter, value);
        public bool GetBool(string parameter) => animator.GetBool(parameter);

        public void SetFloat(string parameter, float value) => animator.SetFloat(parameter, value);
        public float GetFloat(string parameter) => animator.GetFloat(parameter);

        public void SetInteger(string parameter, int value) => animator.SetInteger(parameter, value);
        public int GetInteger(string parameter) => animator.GetInteger(parameter);



        public void IncreaseHealthPointWithPercentage(float value) {
            HP += Mathf.FloorToInt(hpMax * (value / 100));
            if(HP > hpMax) HP = hpMax;
        }

        public void IncreaseHealthPoint(int value) {
            HP += value;
            if(HP > hpMax) HP = hpMax;
        }

        public void IncreaseMoveSpeedOffset(float max = 1.0f) {
            moveSpeedOffset = Mathf.Lerp(moveSpeedOffset, max, Time.deltaTime * moveBoost);
            animator.SetFloat("MoveBlend", moveSpeedOffset);
        }

        public void DecreaseMoveSpeedOffset(float min = 0.0f) {
            moveSpeedOffset = Mathf.Lerp(moveSpeedOffset, min, Time.deltaTime * moveBoost);
            animator.SetFloat("MoveBlend", moveSpeedOffset);
        }

        public void UpdateVelocityWithMoveSpeedOffset() {
            rigidbody.velocity = UtilFunc.GetVec3XZ(rigidbody.velocity.normalized * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Rotate() {
            Rotate(Quaternion.LookRotation(moveDirection));
        }

        public void Rotate(Quaternion rotation, float offset = 1.0f) {
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, Time.deltaTime * rotateSpeed * offset);
        }

        public void RotateImmediately(Quaternion rotation, float time = 0.0f, Ease ease = Ease.Linear) {
            if(time <= 0.0f) transform.rotation = rotation;
            else transform.DORotateQuaternion(rotation, time).SetEase(ease);
        }

        public void Move() {
            Vector3 dir = moveDirection;
            if(floorContactHelper.OnFloor) {
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, floorContactHelper.FloorNormal);
                dir = rot * moveDirection;
            }

            rigidbody.velocity = UtilFunc.GetVec3XZ(dir * moveSpeed * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Move(Vector3 direction, float offset = 1.0f) {
            rigidbody.velocity = UtilFunc.GetVec3XZ(direction * moveSpeed * moveSpeedOffset * offset, rigidbody.velocity.y);
        }

        public void AttachOnFloor(float strength) {
            rigidbody.AddForce(Vector3.down * strength);
        }

        public void Jump() {
            //rigidbody.velocity += Vector3.up * jumpStrength;
            rigidbody.velocity = UtilFunc.GetVec3XZ(rigidbody.velocity, jumpStrength);
        }

        public void Dash() {
            Dash(dashDirection, dashDistance, 0.3f, LayerMask_ExcludeThisAndFloor);
        }

        public void Dash(Vector3 direction, float distance, float moveTime, Ease ease = Ease.OutQuad) {
            Vector3 dashEndPos = transform.position + direction * distance;

            transform.DOMove(dashEndPos, moveTime).SetEase(Ease.OutQuad);
            transform.DORotateQuaternion(Quaternion.LookRotation(direction), moveTime * 0.33f).SetEase(ease);
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

        public virtual void Knockback(Vector3 direction, float strength, float time, int mask, Ease ease = Ease.OutQuad) {
            if(direction.magnitude <= 0) direction = transform.forward;

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
        #endregion
    }
}
