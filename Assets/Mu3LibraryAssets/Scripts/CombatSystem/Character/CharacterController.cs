using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.CombatSystem {
    public enum CharacterType {
        Standard = 0, 


    }

    [System.Flags]
    public enum CharacterState {
        None = 0, 

        Move = 1 << 0, 
        Jump = 1 << 1, 

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

        public Vector3 Forward => transform.forward;
        public Vector3 Right => transform.right;

        #endregion

        public LayerMask Layer => layer;
        protected LayerMask layer = 0;
        protected const string LayerName_Character = "Character";

        protected readonly string AnimParamName_CharacterType = "CharacterType";

        protected readonly string AnimParamName_MoveBlend = "MoveBlend";

        public CharacterType Type => type;
        [SerializeField] protected CharacterType type = CharacterType.Standard;

        public CharacterState State => state;
        protected CharacterState state = CharacterState.None;

        /// <summary>
        /// 사용할 상태를 체크하는 변수로, 절대로 런타임 중에 변경하지 않는다.
        /// </summary>
        [SerializeField] private CharacterState usingStates;
        private List<ICharacterStateAction> activeStates = new List<ICharacterStateAction>();
        private List<ICharacterStateAction> standbyStates = new List<ICharacterStateAction>();




        protected abstract ICharacterStateAction GetDefinedStateAction(CharacterState s);

        #region Utility
        public void AddForce(Vector3 force, ForceMode mode) {
            rigidbody.AddForce(force, mode);
        }

        #region Animation Func
        public void ChangeAnimatorParameter_MoveBlend(float value) {
            animator.SetFloat(AnimParamName_MoveBlend, value);
        }
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

        public void Init() {
            if(!InitScripts()) {
                enabled = false;

                return;
            }
            
            // 캐릭터 오브젝트의 모든 하위 오브젝트 레이어 변경
            void ChangeLayerAll(Transform t, int l) {
                t.gameObject.layer = l;

                for(int i = 0; i < t.childCount; i++) {
                    ChangeLayerAll(t.GetChild(i), l);
                }
            }
            layer = LayerMask.NameToLayer(LayerName_Character);
            ChangeLayerAll(transform, layer);

            // 상태 초기화
            InitStates();

            animator.SetInteger(AnimParamName_CharacterType, (int)type);
        }
        #endregion

        protected void InitStates() {
            activeStates.Clear();
            standbyStates.Clear();

            foreach(CharacterState s in System.Enum.GetValues(typeof(CharacterState))) {
                if(usingStates.HasFlag(s)) {
                    ICharacterStateAction stateAction = GetDefinedStateAction(s);
                    if(stateAction == null) {
                        Debug.LogWarning($"Using State Checked. But, State Action is not defined. character: {this.name}, state: {s}");

                        continue;
                    }

                    standbyStates.Add(stateAction);
                }
            }
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
