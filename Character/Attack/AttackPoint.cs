using Mu3Library.Raycast;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Attack {
    public abstract class AttackPoint : MonoBehaviour {
        [SerializeField, Range(0.01f, 100.0f)] protected float radius;
        [SerializeField, Range(0.01f, 100.0f)] protected float height;

        [Space(20)]
        [SerializeField] protected LayerMask AttackTargetLayer;

        protected HitPointWithComponent<CharacterController>[] hits;

        protected int m_damage;
        protected float m_knockbackStrength;



        protected void Update() {
            UpdateHit();

            if(hits != null && hits.Length > 0) {
                foreach(HitPointWithComponent<CharacterController> c in hits) {
                    if(IsInHitRange(c.Component)) c.Component.GetHit(m_damage, c.HitPoint, m_knockbackStrength);
                }
            }
        }

        protected abstract void UpdateHit();
        protected abstract bool IsInHitRange(CharacterController character);

        #region Utility
        public void SetDamage(int damage, float knockbackStrength = 0.0f) {
            m_damage = damage;
            m_knockbackStrength = knockbackStrength;
        }
        #endregion
    }
}