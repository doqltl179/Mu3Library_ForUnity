using Mu3Library.Character.Attack;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Weapon {
    public class Weapon : MonoBehaviour {
        [SerializeField] private AttackPoint attackPoint;




        protected virtual void OnEnable() {
            attackPoint.enabled = false;
        }

        #region Utility
        public virtual void ActivateAttackPoint() {
            attackPoint.enabled = true;
        }

        public virtual void DeactivateAttackPoint() {
            attackPoint.enabled = false;
        }

        public void SetDamage(int damage, float knockbackStrength = 0.0f) => attackPoint.SetDamage(damage, knockbackStrength);
        #endregion
    }
}