using Mu3Library.Raycast;
using Mu3Library.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public abstract class AttackPoint : MonoBehaviour {
        protected AttackInfo attackInfo;
        protected RayHelper rayHelper;

        [Space(20)]
        [SerializeField, Range(0.01f, 100.0f)] protected float radius;
        [SerializeField, Range(0.01f, 100.0f)] protected float height;

        private List<CharacterController> hitCharacterList = new List<CharacterController>();
        private bool firstHit;

        protected float rayDistance = 0;
        public float RayDistance {
            get => rayDistance;
            set {
                rayHelper.ChangeDistance(value);

                rayDistance = value;
            }
        }

        public AttackPointType Type;

        public bool CheckRange = false;

        public Action OnHit;



        CharacterController tempController;
        protected void Update() {
            if(rayHelper == null) return;

            if(rayHelper.Raycast()) {
                switch(Type) {
                    case AttackPointType.HitAnything: {
                            foreach(RaycastHit hit in rayHelper.Hits) {
                                if(hit.rigidbody != null) {
                                    tempController = hit.rigidbody.GetComponent<CharacterController>();
                                    if(tempController != null && TargetInRange(attackInfo, tempController)) {
                                        tempController.GetHit(attackInfo.Damage, transform.position, attackInfo.KnockbackStrength);
                                    }
                                }

                                OnHit?.Invoke();
                            }
                        }
                        break;
                    case AttackPointType.HitOnlyOneObject: {
                            if(firstHit) return;

                            foreach(RaycastHit hit in rayHelper.Hits) {
                                if(hit.rigidbody != null) {
                                    tempController = hit.rigidbody.GetComponent<CharacterController>();
                                    if(tempController != null && TargetInRange(attackInfo, tempController)) {
                                        tempController.GetHit(attackInfo.Damage, transform.position, attackInfo.KnockbackStrength);
                                    }
                                }

                                firstHit = true;

                                OnHit?.Invoke();

                                break;
                            }
                        }
                        break;
                    case AttackPointType.HitEachCharacterOnce: {
                            foreach(RaycastHit hit in rayHelper.Hits) {
                                if(hit.rigidbody != null) {
                                    CharacterController controller = hit.rigidbody.GetComponent<CharacterController>();
                                    if(controller != null && hitCharacterList.FindIndex(t => t == controller) < 0 && 
                                        TargetInRange(attackInfo, controller)) {
                                        controller.GetHit(attackInfo.Damage, transform.position, attackInfo.KnockbackStrength);

                                        hitCharacterList.Add(controller);

                                        OnHit?.Invoke();
                                    }
                                }
                            }
                        }
                        break;
                    case AttackPointType.HitEachCharacter: {
                            foreach(RaycastHit hit in rayHelper.Hits) {
                                if(hit.rigidbody != null) {
                                    tempController = hit.rigidbody.GetComponent<CharacterController>();
                                    if(tempController != null && TargetInRange(attackInfo, tempController)) {
                                        tempController.GetHit(attackInfo.Damage, transform.position, attackInfo.KnockbackStrength);

                                        OnHit?.Invoke();
                                    }
                                }
                            }
                        }
                        break;
                    case AttackPointType.HitOnlyOneChatacter: {
                            if(firstHit) return;

                            foreach(RaycastHit hit in rayHelper.Hits) {
                                if(hit.rigidbody != null) {
                                    tempController = hit.rigidbody.GetComponent<CharacterController>();
                                    if(tempController != null && TargetInRange(attackInfo, tempController)) {
                                        tempController.GetHit(attackInfo.Damage, transform.position, attackInfo.KnockbackStrength);

                                        firstHit = true;

                                        OnHit?.Invoke();

                                        break;
                                    }
                                }
                            }
                        }
                        break;
                }
            }
        }

        #region Utility
        public abstract void Init(AttackInfo info, int layerMask = -1);

        public void ClearProperties() {
            hitCharacterList.Clear();
            tempController = null;

            firstHit = false;

            OnHit = null;
        }

        public void ChangeLayerMask(int layerMask) => rayHelper.ChangeLayerMask(layerMask);

        protected bool TargetInRange(AttackInfo info, CharacterController target) {
            if(!CheckRange) return true;

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
        #endregion
    }
}