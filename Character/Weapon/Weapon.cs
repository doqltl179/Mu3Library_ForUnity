using Mu3Library.Character.Attack;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Weapon {
    public class Weapon : MonoBehaviour {
        [SerializeField] protected AttackPoint attackPoint;
        public AttackPointType AttackPointType {
            get => attackPoint.Type;
            set => attackPoint.Type = value;
        }
        public float Scale {
            get => transform.localScale.x;
            set {
                attackPoint.RayScale = value;

                transform.localScale = Vector3.one * value;
            }
        }
        public bool CheckRange {
            get => attackPoint.CheckRange;
            set => attackPoint.CheckRange = value;
        }



        protected virtual void OnEnable() {
            attackPoint.ClearProperties();

            attackPoint.gameObject.SetActive(false);
        }

        #region Utility
        public virtual void ActivateAttackPoint() {
            attackPoint.ClearProperties();

            attackPoint.gameObject.SetActive(true);
        }

        public virtual void DeactivateAttackPoint() {
            attackPoint.gameObject.SetActive(false);
        }

        public void Init(AttackInfo info, int layerMask = -1) {
            attackPoint.Init(info, layerMask);
        }

        public void ChangeLayerMask(int layerMask) => attackPoint.ChangeLayerMask(layerMask);
        #endregion
    }
}