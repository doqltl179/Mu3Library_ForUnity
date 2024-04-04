#if UNITY_EDITOR
using UnityEngine;


namespace Mu3Library.Editor.Gizmo {
    public static class Draw {
        public static void WireCapsule(Transform orogin, float radius, float height, int quality = 16) {
            WireCapsule(orogin.position, radius, height, orogin.forward, orogin.right, orogin.up, quality);
        }

        public static void WireCapsule(Vector3 position, float radius, float height,
            Vector3 forward, Vector3 right, Vector3 up, int quality = 16) {
            Vector3 bottomSphereOrigin = position + up * radius;
            Vector3 topSphereOrigin = position + up * (height - radius);

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
}
#endif