using UnityEngine;

namespace Mu3Library.Utility {
    public static class UtilFunc {




        #region Integer
        public static int GetLayerMask(string layerName, bool exclude = false) {
            int layer = LayerMask.NameToLayer(layerName);
            int mask = 0;
            if(layer >= 0) mask = 1 << layer;
            else { Debug.LogWarning($"LayerName not found. name: {layerName}"); }

            if(exclude) mask = ~mask;
            return mask;
        }

        public static int GetLayerMask(string[] layerNames, bool exclude = false) {
            int mask = 0;
            int layer;
            foreach(string name in layerNames) {
                layer = LayerMask.NameToLayer(name);
                if(layer >= 0) { mask |= (1 << layer); }
                else { Debug.LogWarning($"LayerName not found. name: {name}"); }
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

        public static bool IsTargetOnRight(Vector3 forward, Vector3 toTarget) {
            return Vector3.Cross(forward, toTarget).y > 0.0f;
        }
        #endregion

        #region Vector3
        public static Vector3 GetVec3XZ(Vector3 vec3, float y = 0.0f) => new Vector3(vec3.x, y, vec3.z);
        public static Vector3 GetVec3Y(Vector3 vec3, float x = 0.0f, float z = 0.0f) => new Vector3(x, vec3.y, z);

        public static Vector3 GetDirectionXZ(Vector3 from, Vector3 to) => (new Vector3(to.x, 0, to.z) - new Vector3(from.x, 0, from.z)).normalized;

        public static Vector3 BezierCurve(Vector3 start, Vector3 end, float angleDeg, float lerp) {
            Vector3 posDiff = end - start;

            // angleOffsetDeg : dist = angleDeg : ??
            float angleUpToEndDeg = Vector3.Angle(Vector3.up, posDiff.normalized);
            float angleOffsetDeg = 90 - angleUpToEndDeg;
            float heightOffset = angleDeg * Mathf.Abs(posDiff.y) / Mathf.Abs(angleOffsetDeg);
            if(float.IsNaN(heightOffset)) heightOffset = 0.0f;

            Vector3 middlePoint = (start + end) * 0.5f;

            Vector3 controlPoint = new Vector3(middlePoint.x, middlePoint.y + heightOffset, middlePoint.z);
            return BezierCurve(start, end, controlPoint, lerp);
        }
        public static Vector3 BezierCurve(Vector3 start, Vector3 end, Vector3 controlPoint, float lerp) {
            Vector3 startLerp = Vector3.LerpUnclamped(start, controlPoint, lerp);
            Vector3 endLerp = Vector3.LerpUnclamped(controlPoint, end, lerp);
            return Vector3.LerpUnclamped(startLerp, endLerp, lerp);
        }
        #endregion

        #region Else
        public static void SetLayerWithChildren(Transform target, string layerName) {
            SetLayerWithChildren(target, LayerMask.NameToLayer(layerName));
        }

        public static void SetLayerWithChildren(Transform target, int layer) {
            void SetLayer(Transform t) {
                t.gameObject.layer = layer;
                if(t.childCount > 0) {
                    for(int i = 0; i < t.childCount; i++) {
                        SetLayer(t.GetChild(i));
                    }
                }
            }
            SetLayer(target);
        }

        public static T GetComponentOnParent<T>(Transform target) where T : MonoBehaviour  {
            T result = null;

            void FindComponent(Transform t) {
                if(t == null) return;

                result = t.GetComponent<T>();
                if(result == null) {
                    FindComponent(t.parent);
                }
            }
            FindComponent(target);

            return result;
        }

        public static bool IsTargetInConeRange(Vector3 origin, Vector3 targetPos, Vector3 direction, float angleDeg, float distance) {
            float targetDistance = Vector3.Distance(origin, targetPos);
            if(targetDistance > distance) return false;

            Vector3 toTarget = (targetPos - origin).normalized;
            float targetAngleDeg = Vector3.Angle(direction, toTarget);
            if(targetAngleDeg * 0.5f > angleDeg) return false;

            return true;
        }
        #endregion
    }
}