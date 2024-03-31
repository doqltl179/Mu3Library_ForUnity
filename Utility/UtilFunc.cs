using UnityEngine;

namespace Mu3Library.Utility {
    public static class UtilFunc {




        #region Integer
        public static int GetLayerMask(string layerName, bool exclude = false) {
            int mask = (1 << LayerMask.NameToLayer(layerName));
            if(exclude) mask = ~mask;
            return mask;
        }

        public static int GetLayerMask(string[] layerNames, bool exclude = false) {
            int mask = 0;
            foreach(string name in layerNames) {
                mask |= (1 << LayerMask.NameToLayer(name));
            }

            if(exclude) mask = ~mask;
            return mask;
        }
        #endregion

        #region Float
        public static float GetDistanceXZ(Vector3 from, Vector3 to) => Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
        #endregion

        #region Boolean
        public static bool IsInAngleRange(Vector3 forward, Vector3 directionToTarget, float angleDeg) {
            return Vector3.Angle(forward, directionToTarget) < angleDeg * 0.5f;
        }
        #endregion

        #region Vector3
        public static Vector3 GetVec3XZ(Vector3 vec3, float y = 0.0f) => new Vector3(vec3.x, y, vec3.z);
        public static Vector3 GetVec3Y(Vector3 vec3, float x = 0.0f, float z = 0.0f) => new Vector3(x, vec3.y, z);
        #endregion
    }
}