using Mu3Library.Raycast;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public abstract class AttackPoint : MonoBehaviour {
        [SerializeField] protected bool hitOnce = true;

        [Space(20)]
        [SerializeField, Range(0.01f, 100.0f)] protected float radius;
        [SerializeField, Range(0.01f, 100.0f)] protected float height;

        [Space(20)]
        [SerializeField] protected LayerMask AttackTargetLayer;

        protected HitPointWithComponent<CharacterController>[] hits;
        protected List<CharacterController> hitCharacterList = new List<CharacterController>();

        protected int m_damage;
        protected float m_knockbackStrength;



        protected void OnEnable() {
            hitCharacterList.Clear();
        }

        protected void Update() {
            UpdateHit();

            if(hits != null) {
                foreach(HitPointWithComponent<CharacterController> c in hits) {
                    if(IsInHitRange(c.Component)) {
                        if(hitOnce) {
                            if(!hitCharacterList.Contains(c.Component)) {
                                c.Component.GetHit(m_damage, c.HitPoint, m_knockbackStrength);

                                hitCharacterList.Add(c.Component);
                            }
                        }
                        else {
                            c.Component.GetHit(m_damage, c.HitPoint, m_knockbackStrength);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Raycast 범위안에 있는 모든 CharacterController를 가져옴
        /// 정밀 체크 X
        /// </summary>
        protected abstract void UpdateHit();
        /// <summary>
        /// parameter로 설정한 범위 안에 있는지를 체크
        /// 정밀 체크 O
        /// </summary>
        protected abstract bool IsInHitRange(CharacterController character);

        #region Utility
        public void SetDamage(int damage, float knockbackStrength = 0.0f) {
            m_damage = damage;
            m_knockbackStrength = knockbackStrength;
        }
        #endregion
    }
}