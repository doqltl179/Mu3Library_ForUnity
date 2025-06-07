#if UNITY_EDITOR
using UnityEngine;


namespace Mu3Library.Editor {
    public static class DebugShape {



        #region Debug
        public static void DebugDrawSphere(Vector3 position, float radius, Vector3 forward, Vector3 right, Vector3 up, 
            Color color, int quality = 16) {
            int loopCount = quality + 1;

            float ratio1, ratio2;
            float angle1, angle2;
            Vector3 p1, p2;

            for(int i = 0; i < loopCount - 1; i++) {
                ratio1 = (float)(i + 0) / quality;
                ratio2 = (float)(i + 1) / quality;

                angle1 = 360 * ratio1;
                angle2 = 360 * ratio2;

                p1 = position + Quaternion.AngleAxis(angle1, up) * right * radius;
                p2 = position + Quaternion.AngleAxis(angle2, up) * right * radius;
                Debug.DrawLine(p1, p2, color, 0, false);

                p1 = position + Quaternion.AngleAxis(angle1, right) * forward * radius;
                p2 = position + Quaternion.AngleAxis(angle2, right) * forward * radius;
                Debug.DrawLine(p1, p2, color, 0, false);

                p1 = position + Quaternion.AngleAxis(angle1, forward) * up * radius;
                p2 = position + Quaternion.AngleAxis(angle2, forward) * up * radius;
                Debug.DrawLine(p1, p2, color, 0, false);
            }
        }
        #endregion

        #region Gizmo
        public static void GizmoDrawCapsule(Transform origin, Vector3 localPosOffset, float radius, float height, int quality = 16) {
            Vector3 position = origin.position +
                origin.forward * localPosOffset.z + origin.right * localPosOffset.x + origin.up * localPosOffset.y;

            GizmoDrawCapsule(position, radius, height, origin.forward, origin.right, origin.up, quality);
        }

        public static void GizmoDrawCapsule(Vector3 position, float radius, float height, 
            Vector3 forward, Vector3 right, Vector3 up, int quality = 16) {
            Vector3 bottomSphereOrigin = position + up * radius;
            Vector3 topSphereOrigin = position + up * (height - radius);

            if(radius * 2 > height) {
                Gizmos.DrawWireSphere(position + up * height * 0.5f, radius);
            }
            else {
                Vector3 p1, p2;
                float angle1, angle2;
                for(int i = 0; i < quality; i++) {
                    angle1 = Mathf.InverseLerp(0.0f, quality, i) * 180;
                    angle2 = Mathf.InverseLerp(0.0f, quality, i + 1) * 180;

                    p1 = topSphereOrigin + (Quaternion.AngleAxis(-angle1, right) * forward) * radius;
                    p2 = topSphereOrigin + (Quaternion.AngleAxis(-angle2, right) * forward) * radius;
                    Gizmos.DrawLine(p1, p2);

                    p1 = topSphereOrigin + (Quaternion.AngleAxis(angle1, forward) * right) * radius;
                    p2 = topSphereOrigin + (Quaternion.AngleAxis(angle2, forward) * right) * radius;
                    Gizmos.DrawLine(p1, p2);

                    p1 = bottomSphereOrigin + (Quaternion.AngleAxis(angle1, right) * forward) * radius;
                    p2 = bottomSphereOrigin + (Quaternion.AngleAxis(angle2, right) * forward) * radius;
                    Gizmos.DrawLine(p1, p2);

                    p1 = bottomSphereOrigin + (Quaternion.AngleAxis(-angle1, forward) * right) * radius;
                    p2 = bottomSphereOrigin + (Quaternion.AngleAxis(-angle2, forward) * right) * radius;
                    Gizmos.DrawLine(p1, p2);
                }

                Gizmos.DrawLine(topSphereOrigin + forward * radius, bottomSphereOrigin + forward * radius);
                Gizmos.DrawLine(topSphereOrigin + -forward * radius, bottomSphereOrigin + -forward * radius);
                Gizmos.DrawLine(topSphereOrigin + right * radius, bottomSphereOrigin + right * radius);
                Gizmos.DrawLine(topSphereOrigin + -right * radius, bottomSphereOrigin + -right * radius);
            }
        }
        #endregion
    }
}
#endif