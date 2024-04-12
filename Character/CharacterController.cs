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

        [Space(20)]
        [SerializeField] protected Transform weaponSlot_L;
        [SerializeField] protected Transform weaponSlot_R;
        public Transform WeaponSlot_L => weaponSlot_L;
        public Transform WeaponSlot_R => weaponSlot_R;

        [SerializeField] protected Mu3Library.Character.Weapon.Weapon currentWeapon;
        public Mu3Library.Character.Weapon.Weapon CurrentWeapon => currentWeapon;

        [Space(20)]
        [SerializeField] private AttackInfo[] attackInfos;
        // 0: Normal Attack
        // else: Skill
        public AttackInfo[] AttackInfos => attackInfos;

        protected CharacterState currentState;
        protected float stateChangeCool = 0.0f;

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

        private float dotAT;
        private float attackTargetDistance;
        private float attackTargetDistanceXZ;
        private Vector3 attackTargetDirection;
        private Vector3 attackTargetDirectionXZ;
        private Quaternion attackTargetLookRotation;
        private Quaternion attackTargetLookRotationXZ;

        public float DotAT => dotAT;
        public float AttackTargetDistance => attackTargetDistance;
        public float AttackTargetDistanceXZ => attackTargetDistanceXZ;
        public Vector3 AttackTargetDirection => attackTargetDirection;
        public Vector3 AttackTargetDirectionXZ => attackTargetDirectionXZ;
        public Quaternion AttackTargetLookRotation => attackTargetLookRotation;
        public Quaternion AttackTargetLookRotationXZ => attackTargetLookRotationXZ;

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

        [HideInInspector] public bool Avoid; //Å¸°Ý È¸ÇÇ
        [HideInInspector] public bool SuperArmour; //³Ë¹é Äµ½½



        protected virtual void Start() {
            animationInfo = new AnimationInfo(animator);
            animator.SetInteger("CharacterType", (int)characterType);
        }

        protected virtual void Update() {
            if(currentState == null) return;

            if(animationInfo != null) animationInfo.UpdateStateInfoAll();

            if(floorContactHelper != null) floorContactHelper.Raycast();

            stateChangeCool = Mathf.Max(stateChangeCool - Time.deltaTime, 0.0f);
            hitCool = Mathf.Max(hitCool - Time.deltaTime, 0.0f);

            if(attackInfos != null) {
                foreach(AttackInfo info in attackInfos) {
                    info.Update();
                }
            }

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

                isInputJump = KeyCodeInputCollector.Instance.GetKeyDown(inputJump);

                isInputAttack = KeyCodeInputCollector.Instance.GetKeyDown(inputAttack);
            }
            else {
                attackTargetDistance = Vector3.Distance(AttackTarget.Pos, transform.position);
                attackTargetDistanceXZ = UtilFunc.GetDistanceXZ(AttackTarget.Pos, transform.position);
                attackTargetDirection = (AttackTarget.Pos - transform.position).normalized;
                attackTargetDirectionXZ = UtilFunc.GetVec3XZ(attackTargetDirection).normalized;
                attackTargetLookRotation = Quaternion.LookRotation(attackTargetDirection);
                attackTargetLookRotationXZ = Quaternion.LookRotation(attackTargetDirectionXZ);
                dotAT = Vector3.Dot(attackTargetDirectionXZ, transform.forward);
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
            LayerMask_ExcludeThis = ~(1 << gameObject.layer);
            LayerMask_ExcludeThisAndFloor = UtilFunc.GetLayerMask(
                new string[] { LayerMask.LayerToName(gameObject.layer), "Floor" }, true);
            LayerMask_OnlyTarget = UtilFunc.GetLayerMask(
                LayerMask.LayerToName(gameObject.layer) == "PlayCharacter" ? "OtherCharacter" : "PlayCharacter");

            floorContactHelper = new FloorContactRayHelper(transform, 0.1f, 0.06f, LayerMask_ExcludeThis);

            if(currentState != null) {
                currentState.Exit();
                currentState = null;
            }
            states = new Dictionary<CharacterStateType, CharacterState>();

            if(attackInfos != null && attackInfos.Length > 0) {
                foreach(AttackInfo info in attackInfos) {
                    info.Init();
                }

                if(currentWeapon != null) {
                    currentWeapon.Init(AttackInfos[0], LayerMask_OnlyTarget);
                    currentWeapon.AttackPointType = AttackPointType.HitEachCharacterOnce;
                }
            }

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
                Vector3 toAttackPoint = (attackPoint - transform.position).normalized;
                Vector3 toAttackPointXZ = UtilFunc.GetVec3XZ(toAttackPoint).normalized;
                float angle = Vector3.Angle(transform.forward, toAttackPointXZ);
                bool isRight = UtilFunc.IsTargetOnRight(transform.forward, toAttackPointXZ);
                if(angle < 45) animator.SetInteger("HitDirection", 1); //Forward
                else if(angle > 135) animator.SetInteger("HitDirection", 0); //Back
                else if(isRight) animator.SetInteger("HitDirection", 3); //Right
                else animator.SetInteger("HitDirection", 2); //Left

                if(!SuperArmour && knockbackStrength > 0 && floorContactHelper.OnFloor) {
                    Knockback(
                        UtilFunc.GetVec3XZ(transform.position - attackPoint).normalized,
                        knockbackStrength,
                        0.2f,
                        LayerMask_ExcludeThis);

                    ChangeState(CharacterStateType.Hit, true);
                }

                hitCool = hitCoolTime;
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
        }

        protected abstract CharacterState GetNewState(CharacterStateType type);

        public void SetStateChangeCoolTime(float time) => stateChangeCool = time;

        protected void InitAttackInfo(int index) {
            if(index < 0 || index >= attackInfos.Length) {
                Debug.LogWarning($"AttackInfo index out of range. index: {index}");

                return;
            }

            attackInfos[index].Init();
        }

        public int GetActivatableAttackIndex() {
            if(attackInfos != null && attackInfos.Length > 1) {
                for(int i = attackInfos.Length - 1; i >= 0; i--) {
                    if(CheckSkillActivatable(i)) return i;
                }
            }
            else {
                return -1;
            }

            return -1;
        }

        protected abstract bool CheckSkillActivatable(int index);
        public abstract void ActivateSkill(int index);



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

        public bool IsClipTransitioning(int layer = 0) {
            return animationInfo.IsClipTransitioning(layer);
        }

        public void SetTrigger(string parameter) => animator.SetTrigger(parameter);
        public void ResetTrigger(string parameter) => animator.ResetTrigger(parameter);

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
            rigidbody.velocity = UtilFunc.GetVec3XZ(moveDirection * moveSpeed * moveSpeedOffset, rigidbody.velocity.y);
        }

        public void Move(Vector3 direction, float offset = 1.0f) {
            rigidbody.velocity = UtilFunc.GetVec3XZ(direction * moveSpeed * moveSpeedOffset * offset, rigidbody.velocity.y);
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

            Vector3 directionToTarget = (target.Pos - transform.position).normalized;
            Vector3 directionToTargetXZ = UtilFunc.GetVec3XZ(directionToTarget).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTargetXZ);
            if(angle > info.AngleDeg * 0.5f) return false;

            float heightDiff = target.Pos.y - transform.position.y;
            if(heightDiff < info.HeightMin || info.HeightMax < heightDiff) return false;

            return true;
        }

        public T GetInstantiateObject<T>(T obj) where T : Object {
            return Instantiate(obj);
        }
        #endregion
    }
}