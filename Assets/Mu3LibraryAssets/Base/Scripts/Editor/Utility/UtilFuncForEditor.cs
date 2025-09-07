#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Editor {
    public static class UtilFuncForEditor {




        #region Other
        public static void RemoveAllListener(ref Button btn) {
            SerializedObject serializedButton = new SerializedObject(btn);
            SerializedProperty onClickProperty = serializedButton.FindProperty("m_OnClick");
            onClickProperty.FindPropertyRelative("m_PersistentCalls.m_Calls").ClearArray();
            serializedButton.ApplyModifiedProperties();

            // 버튼 변경 사항을 적용하여 유니티 에디터에 반영
            EditorUtility.SetDirty(btn);
        }
        #endregion
    }
}
#endif