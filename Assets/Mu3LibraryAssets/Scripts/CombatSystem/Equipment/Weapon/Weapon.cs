using UnityEngine;

namespace Mu3Library.CombatSystem {
    public enum WeaponType {
        None = 0, 

        TwoHandSword = 1,

    }

    public abstract class Weapon : Equipment {
        public WeaponType Type => type;
        [SerializeField] private WeaponType type;

        [SerializeField, Range(1, 100)] protected int attackDamage = 10;



        public abstract void Attack(HitType type);
    }
}
