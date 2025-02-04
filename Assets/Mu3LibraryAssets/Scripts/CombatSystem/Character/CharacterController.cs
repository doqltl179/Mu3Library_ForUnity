using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mu3Library.CombatSystem {
    public enum CharacterType {
        GreatSword = 0, 


    }

    [System.Flags]
    public enum CharacterState {
        None = 0, 

        Move = 1 << 0, 
        Jump = 1 << 1, 
        Attack = 1 << 2,
        Hit = 1 << 3, 

    }

    public enum HitType {
        None = 0, 

        Normal, 

    }

    /// <summary>
    /// <br/> Rigidbody를 사용할거면 Animator 옵션에서 "Apply Root Motion"을 끄고 사용하자.
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Animator))]
    public abstract class CharacterController : MonoBehaviour {
        protected CapsuleCollider collider = null;
        protected Rigidbody rigidbody = null;
        protected Animator animator = null;

        public bool IsPlayer {
            get => isPlayer;
            set {
                // Some Actions..

                isPlayer = value;
            }
        }
        private bool isPlayer = false;

        public bool IsAutoPlay => isAutoPlay;
        private bool isAutoPlay = false;

        /// <summary>
        /// Head Tracking을 위한 변수
        /// </summary>
        protected Transform lookTarget = null;
        /// <summary>
        /// <br/> 'lookTarget'을 바라보는 위치값 offset
        /// <br/> offset 값은 'lookTarget'의 로컬 위치값으로 적용한다.
        /// </summary>
        protected Vector3 lookTargetLocalPosOffset = Vector3.zero;
        
        #region Public Get Properties

        public float Radius => collider.radius;
        public float Height => collider.height;

        public Vector3 Position {
            get => transform.position;
            set => transform.position = value;
        }
        public Vector3 EulerAngles {
            get => transform.eulerAngles;
            set => transform.eulerAngles = value;
        }
        public Quaternion Rotation {
            get => transform.rotation;
            set => transform.rotation = value;
        }

        public Vector3 LinearVelocity => rigidbody.linearVelocity;

        public Vector3 Forward => transform.forward;
        public Vector3 Right => transform.right;

        #endregion

        public static LayerMask Layer => layer;
        protected static LayerMask layer = -1;
        protected const string LayerName_Character = "Character";

        protected readonly string AnimParamName_CharacterType = "CharacterType";

        protected readonly string AnimParamName_MoveBlend = "MoveBlend";

        protected readonly string AnimParamName_IsJump = "IsJump";

        protected readonly string AnimParamName_AttackIndex = "AttackIndex";
        protected readonly string AnimParamName_AttackMotionIndex = "AttackMotionIndex";

        protected readonly string AnimParamName_HitTrigger = "HitTrigger";
        protected readonly string AnimParamName_ReturnToFirstAnimation = "ReturnToFirstAnimation";

        public CharacterType Type => type;
        [SerializeField] protected CharacterType type = CharacterType.GreatSword;
        [SerializeField] protected Weapon currentWeapon;

        /// <summary>
        /// 사용할 상태를 체크하는 변수로, 절대로 런타임 중에 변경하지 않는다.
        /// </summary>
        [Space(20)]
        [SerializeField] private CharacterState usingStates;
        private List<ICharacterStateAction> activeStates = new List<ICharacterStateAction>();
        private List<ICharacterStateAction> standbyStates = new List<ICharacterStateAction>();



        /// <summary>
        /// Hit 되었을 때의 동작을 각 CharacterController 별로 정의
        /// </summary>
        public abstract void GetHit(int damage, HitType type);

        /// <summary>
        /// 각 상태별 'ICharacterStateAction' class를 지정해준다.
        /// </summary>
        protected abstract ICharacterStateAction GetDefinedStateAction(CharacterState s);

        /// <summary>
        /// 'CharacterController'에 사용되는 모든 property를 여기서 업데이트 하자.
        /// </summary>
        protected abstract void UpdateProperties();

        /// <summary>
        /// <br/> 'CharacterController.Init' 함수가 불릴 때 같이 불린다.
        /// <br/> 'CharacterController'에 사용되는 모든 property를 여기서 초기화 하자.
        /// </summary>
        protected abstract void InitProperties();

        /// <summary>
        /// <br/> Head Tracking을 위한 함수.
        /// <br/> Animator Controller의 Layer에 'IK Pass'가 체크되어 있어야 한다.
        /// <br/> 매 프레임마다 호출됨
        /// </summary>
        protected virtual void OnAnimatorIK(int layerIndex) {
            if(animator == null) {
                return;
            }

            // Test
            if(lookTarget == null) {
                lookTarget = Camera.main.transform;
                if(lookTarget == null) {
                    return;
                }
            }

            Transform headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            if(headTransform == null) {
                return;
            }

            Vector3 headPosition = headTransform.position;
            //Vector3 targetPosition = lookTarget.position;
            Vector3 targetPosition = lookTarget.TransformPoint(lookTargetLocalPosOffset);
            Vector3 headToTarget = (targetPosition - headPosition);

            // 만약 범위(Radius)도 지정해주고 싶으면 여기에 코드를 작성하자.

            float dot = Vector3.Dot(transform.forward, headToTarget.normalized);
            const float trackingAngleDeg = 90.0f;
            const float trackingAngleRad = trackingAngleDeg * Mathf.Deg2Rad;
            if(dot < Mathf.Cos(trackingAngleRad)) {
                return;
            }

            animator.SetLookAtWeight(dot);
            animator.SetLookAtPosition(lookTarget.position);
        }

        #region Utility
        public void ForceExitState(ICharacterStateAction stateAction) {
            int idx = activeStates.FindIndex(t => t == stateAction);
            if(idx >= 0) {
                stateAction.Exit();
                standbyStates.Add(stateAction);

                standbyStates.RemoveAt(idx);
            }
        }

        public bool CompareWithCurrentWeapon(Weapon w) {
            if(currentWeapon == null) {
                return false;
            }

            return currentWeapon == w;
        }

        public void AddForce(Vector3 force, ForceMode mode) {
            rigidbody.AddForce(force, mode);
        }

        public void SetLookTarget(Transform target, Vector3 localPosOffset) {
            lookTarget = target;
            lookTargetLocalPosOffset = localPosOffset;
        }

        #region Animation Func
        public AnimatorClipInfo[] GetCurrentAnimatorClipInfo(int layer = 0) {
            if(layer >= animator.layerCount || layer < 0) {
                Debug.LogWarning($"Layer out of range. requested layer: {layer}, layerCount: {animator.layerCount}");

                return new AnimatorClipInfo[0];
            }

            return animator.GetCurrentAnimatorClipInfo(layer);
        }

        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layer = 0) {
            if(layer >= animator.layerCount || layer < 0) {
                Debug.LogWarning($"Layer out of range. requested layer: {layer}, layerCount: {animator.layerCount}");

                return new AnimatorStateInfo();
            }

            return animator.GetCurrentAnimatorStateInfo(layer);
        }

        public bool IsAnimatorInTransition(int layer = 0) {
            return animator.IsInTransition(layer);
        }

        public void SetAnimatorParameter_MoveBlend(float value) {
            animator.SetFloat(AnimParamName_MoveBlend, value);
        }

        public void GetAnimatorParameter_IsJump() {
            animator.GetBool(AnimParamName_IsJump);
        }
        public void SetAnimatorParameter_IsJump(bool value) {
            animator.SetBool(AnimParamName_IsJump, value);
        }

        public int GetAnimatorParameter_AttackIndex() {
            return animator.GetInteger(AnimParamName_AttackIndex);
        }
        public void SetAnimatorParameter_AttackIndex(int value) {
            animator.SetInteger(AnimParamName_AttackIndex, value);
        }

        public int GetAnimatorParameter_AttackMotionIndex() {
            return animator.GetInteger(AnimParamName_AttackMotionIndex);
        }
        public void SetAnimatorParameter_AttackMotionIndex(int value) {
            animator.SetInteger(AnimParamName_AttackMotionIndex, value);
        }

        public void SetAnimatorParameter_HitTrigger() {
            animator.SetTrigger(AnimParamName_HitTrigger);
        }

        public void SetAnimatorParameter_ReturnToFirstAnimation() {
            animator.SetTrigger(AnimParamName_ReturnToFirstAnimation);
        }
        #endregion

        #region Animation Event Func

        public abstract void AnimationEventWithCharacterState(CharacterState s);

        #endregion

        /// <summary>
        /// "FixedUpdate"에서 불리는 함수
        /// </summary>
        public virtual void FixedUpdateAction() {
            foreach(ICharacterStateAction stateAction in activeStates) {
                stateAction.FixedUpdate();
            }
        }

        /// <summary>
        /// <br/> "FixedUpdate"에서 불리는 함수
        /// <br/> Update 함수에서 Character의 상태 변환을 시켜준다.
        /// </summary>
        public virtual void UpdateAction() {
            UpdateProperties();

            // UpdateAlways
            foreach(ICharacterStateAction stateAction in activeStates) {
                stateAction.UpdateAlways();
            }
            foreach(ICharacterStateAction stateAction in standbyStates) {
                stateAction.UpdateAlways();
            }

            // EnterCheck
            for(int i = 0; i < standbyStates.Count; i++) {
                if(standbyStates[i].EnterCheck()) {
                    ICharacterStateAction stateAction = standbyStates[i];

                    stateAction.Enter();
                    activeStates.Add(stateAction);

                    standbyStates.RemoveAt(i);
                    i--;
                }
            }

            for(int i = 0; i < activeStates.Count; i++) {
                // ExitCheck
                if(activeStates[i].ExitCheck()) {
                    ICharacterStateAction stateAction = activeStates[i];

                    stateAction.Exit();
                    standbyStates.Add(stateAction);

                    activeStates.RemoveAt(i);
                    i--;
                }
                // Update
                else {
                    activeStates[i].Update();
                }
            }
        }

        /// <summary>
        /// "FixedUpdate"에서 불리는 함수
        /// </summary>
        public virtual void LateUpdateAction() {
            foreach(ICharacterStateAction stateAction in activeStates) {
                stateAction.LateUpdate();
            }
        }

        public void Init(bool isAutoPlay) {
            if(!InitScripts()) {
                enabled = false;

                return;
            }

            this.isAutoPlay = isAutoPlay;

            // 캐릭터 오브젝트의 모든 하위 오브젝트 레이어 변경
            void ChangeLayerAll(Transform t, int l) {
                t.gameObject.layer = l;

                for(int i = 0; i < t.childCount; i++) {
                    ChangeLayerAll(t.GetChild(i), l);
                }
            }
            layer = LayerMask.NameToLayer(LayerName_Character);
            ChangeLayerAll(transform, layer);

            // 프로퍼티 초기화
            InitProperties();
            // 상태 초기화
            InitStates();

            animator.SetInteger(AnimParamName_CharacterType, (int)type);
        }
        #endregion

        protected void InitStates() {
            activeStates.Clear();
            standbyStates.Clear();

            foreach(CharacterState s in System.Enum.GetValues(typeof(CharacterState))) {
                if(s != CharacterState.None && usingStates.HasFlag(s)) {
                    ICharacterStateAction stateAction = GetDefinedStateAction(s);
                    if(stateAction == null) {
                        Debug.LogWarning($"Using State Checked. But, State Action is not defined. character: {this.name}, state: {s}");

                        continue;
                    }

                    standbyStates.Add(stateAction);
                }
            }

            lookTarget = null;
        }

        protected virtual bool InitScripts() {
            if(collider == null) {
                collider = GetComponent<CapsuleCollider>();
                if(collider == null) {
                    Debug.LogError("Collider not found.");

                    return false;
                }
            }

            if(rigidbody == null) {
                rigidbody = GetComponent<Rigidbody>();
                if(rigidbody == null) {
                    Debug.LogError("Rigidbody not found.");

                    return false;
                }
            }

            if(animator == null) {
                animator = GetComponent<Animator>();
                if(animator == null) {
                    Debug.LogError("Animator not found.");

                    return false;
                }
                else if(animator.runtimeAnimatorController == null) {
                    Debug.LogError("Animator Controller not found.");

                    return false;
                }
            }

            return true;
        }
    }
}
