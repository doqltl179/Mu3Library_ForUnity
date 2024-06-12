#if UNITY_EDITOR
using UnityEngine;


namespace Mu3Library.Editor.Gizmo {
    public static class Draw {
        private static Vector3 m_p1, m_p2;
        private static float m_angle1, m_angle2;
        private static Vector3 m_bottomSphereOrigin, m_topSphereOrigin;



        public static void WireCapsule(Transform orogin, float radius, float height, int quality = 16) {
            WireCapsule(orogin.position, radius, height, orogin.forward, orogin.right, orogin.up, quality);
        }

        public static void WireCapsule(Vector3 position, float radius, float height,
            Vector3 forward, Vector3 right, Vector3 up, int quality = 16) {
            m_bottomSphereOrigin = position + up * radius;
            m_topSphereOrigin = position + up * (height - radius);

            if(radius * 2 > height) {
                Gizmos.DrawWireSphere(position + up * height * 0.5f, radius);
            }
            else {
                for(int i = 0; i < quality; i++) {
                    m_angle1 = Mathf.InverseLerp(0.0f, quality, i) * 180;
                    m_angle2 = Mathf.InverseLerp(0.0f, quality, i + 1) * 180;

                    m_p1 = m_topSphereOrigin + (Quaternion.AngleAxis(-m_angle1, right) * forward) * radius;
                    m_p2 = m_topSphereOrigin + (Quaternion.AngleAxis(-m_angle2, right) * forward) * radius;
                    Gizmos.DrawLine(m_p1, m_p2);

                    m_p1 = m_topSphereOrigin + (Quaternion.AngleAxis(m_angle1, forward) * right) * radius;
                    m_p2 = m_topSphereOrigin + (Quaternion.AngleAxis(m_angle2, forward) * right) * radius;
                    Gizmos.DrawLine(m_p1, m_p2);

                    m_p1 = m_bottomSphereOrigin + (Quaternion.AngleAxis(m_angle1, right) * forward) * radius;
                    m_p2 = m_bottomSphereOrigin + (Quaternion.AngleAxis(m_angle2, right) * forward) * radius;
                    Gizmos.DrawLine(m_p1, m_p2);

                    m_p1 = m_bottomSphereOrigin + (Quaternion.AngleAxis(-m_angle1, forward) * right) * radius;
                    m_p2 = m_bottomSphereOrigin + (Quaternion.AngleAxis(-m_angle2, forward) * right) * radius;
                    Gizmos.DrawLine(m_p1, m_p2);
                }

                Gizmos.DrawLine(m_topSphereOrigin + forward * radius, m_bottomSphereOrigin + forward * radius);
                Gizmos.DrawLine(m_topSphereOrigin + -forward * radius, m_bottomSphereOrigin + -forward * radius);
                Gizmos.DrawLine(m_topSphereOrigin + right * radius, m_bottomSphereOrigin + right * radius);
                Gizmos.DrawLine(m_topSphereOrigin + -right * radius, m_bottomSphereOrigin + -right * radius);
            }
        }
    }
}
#endif