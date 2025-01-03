#if UNITY_EDITOR
using UnityEngine;

namespace Mu3Library.Editor {
    public static class UtilFuncForEditor {



        #region Utility

        public static Vector3 ColToVec3(Color col) {
            return new Vector3(col.r, col.g, col.b);
        }

        public static Vector4 ColToVec4(Color col) {
            return new Vector4(col.r, col.g, col.b, col.a);
        }

        public static Color VecToCol(Vector3 vec3, float alpha = 1.0f) {
            return new Color(vec3.x, vec3.y, vec3.z, alpha);
        }

        #endregion
    }
}
#endif