using Mu3Library.CombatSystem;
using UnityEngine;

using CharacterController = Mu3Library.CombatSystem.CharacterController;

#if UNITY_EDITOR
using Mu3Library.EditorOnly.Gizmo;
#endif

namespace Mu3Library.Demo.CombatSystem {
    public class GreatSword : Weapon {
        [Space(20)]
        [SerializeField] private Vector3 hitLocalPosOffset;
        [SerializeField] private float attackRange = 1;
        [SerializeField] private float attackRadius = 0.5f;



#if UNITY_EDITOR
        // Hit Bound를 보여주기 위함
        private void OnDrawGizmos() {
            Gizmos.color = Color.red;

            ShapeUtil.DrawCapsule(transform, hitLocalPosOffset, attackRadius, attackRange);
        }
#endif

        public override void Attack(HitType type) {
            Vector3 p1 = transform.position +
                transform.forward * hitLocalPosOffset.z + 
                transform.right * hitLocalPosOffset.x + 
                transform.up * hitLocalPosOffset.y;
            Vector3 p2 = p1 + transform.up * attackRange;
            int layerMask = 1 << CharacterController.Layer;

            RaycastHit[] hits = Physics.CapsuleCastAll(p1, p2, attackRadius, transform.forward, 0.0f, layerMask);
            for(int i = 0; i < hits.Length; i++) {
                CharacterController controller = hits[i].collider.GetComponent<CharacterController>();
                // 자기 자신의 무기로는 데미지를 입지 않도록 한다.
                if(controller != null && !controller.CompareWithCurrentWeapon(this)) {
                    controller.GetHit(attackDamage, type);
                }
            }
        }
    }
}