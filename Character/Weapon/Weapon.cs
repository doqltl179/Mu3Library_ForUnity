using Mu3Library.Character.Attack;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character.Weapon {
    public class Weapon : MonoBehaviour {
        protected AttackInfo attackInfo;



        #region Utility
        public void SetAttackInfo(AttackInfo info) => attackInfo = info;
        #endregion
    }
}