#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Demo.UtilWindow {
    [CustomEditor(typeof(UtilWindowProperty))]
    public class UtilWindowPropertyObjectEditor : UnityEditor.Editor {




        public override void OnInspectorGUI() {
            GUI.enabled = false; // Inspector를 비활성화
            base.OnInspectorGUI();
            GUI.enabled = true; // 다시 활성화
        }
    }
}

#endif