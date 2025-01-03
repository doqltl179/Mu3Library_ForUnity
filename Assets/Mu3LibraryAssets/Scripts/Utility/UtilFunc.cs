using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Utility {
    public static class UtilFunc {




        #region Integer
        public static int GetLayerMask(string layerName, bool exclude = false) {
            int layer = LayerMask.NameToLayer(layerName);
            int mask = 0;
            if(layer >= 0) {
                mask = 1 << layer;
            }
            else { 
                Debug.LogWarning($"LayerName not found. name: {layerName}"); 
            }

            if(exclude) {
                mask = ~mask;
            }

            return mask;
        }

        public static int GetLayerMask(string[] layerNames, bool exclude = false) {
            int mask = 0;
            int layer;
            for(int i = 0; i < layerNames.Length; i++) {
                layer = LayerMask.NameToLayer(layerNames[i]);
                if(layer >= 0) { 
                    mask |= (1 << layer);
                }
                else { 
                    Debug.LogWarning($"LayerName not found. name: {layerNames[i]}");
                }
            }

            if(exclude) mask = ~mask;
            return mask;
        }
        #endregion

        #region Float
        public static float InverseLerpUnclamped(float from, float to, float a) {
            return (a - from) / (to - from);
        }

        public static float InverseLerp(Vector3 from, Vector3 to, Vector3 a) {
            Vector3 ab = to - from;
            if(ab.magnitude == 0f) {
                return 0f;
            }

            Vector3 av = a - from;
            return Vector3.Dot(av, ab) / (ab.magnitude * ab.magnitude);
        }
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
        public static Vector3 BezierCurve(Vector3 start, Vector3 end, Vector3 upDir, float angleDeg, float lerp) {
            Vector3 posDiff = end - start;

            float angleUpToEndDeg = Vector3.Angle(upDir, posDiff.normalized);
            float angleOffsetDeg = 90 - angleUpToEndDeg;
            float heightOffset = angleDeg * Mathf.Abs(posDiff.y) / Mathf.Abs(angleOffsetDeg);
            if(float.IsNaN(heightOffset)) {
                heightOffset = 0.0f;
            }

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

#if UNITY_EDITOR
        #region Unity Editor Only
        public static void RemoveAllListener(ref Button btn) {
            SerializedObject serializedButton = new SerializedObject(btn);
            SerializedProperty onClickProperty = serializedButton.FindProperty("m_OnClick");
            onClickProperty.FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
            serializedButton.ApplyModifiedProperties();

            // 버튼 변경 사항을 적용하여 유니티 에디터에 반영
            EditorUtility.SetDirty(btn);
        }
        #endregion
#endif
    }
}