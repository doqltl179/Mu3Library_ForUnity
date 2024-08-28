using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor {
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    public class ReadOnlyAttributeDrawer : PropertyDrawer {



        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginProperty(position, label, property);

            // 기존 GUI 상태를 저장
            bool previousGUIState = GUI.enabled;

            // GUI를 비활성화하여 필드를 읽기 전용으로 설정
            GUI.enabled = false;

            // 필드를 그릴 때 includeChildren을 true로 설정하여 자식 요소도 함께 렌더링
            if(property.isExpanded) {
                EditorGUI.PropertyField(position, property, label, true);
            }
            else {
                // 리스트가 펼쳐지지 않는 경우 기본 필드만 그리기
                EditorGUI.PropertyField(position, property, label, false);
            }

            // GUI 상태를 원래대로 복원
            GUI.enabled = previousGUIState;

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            // 기본적으로 전체 높이를 계산하여 반환
            return EditorGUI.GetPropertyHeight(property, label, property.isExpanded);
        }
    }
}