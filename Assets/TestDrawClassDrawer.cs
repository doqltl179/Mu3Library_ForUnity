using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TestDrawClass))]
public class TestDrawClassDrawer : PropertyDrawer {



    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();

        // SerializedProperty를 통해 TestDrawClass의 필드 접근
        SerializedProperty testStringProp = property.FindPropertyRelative("testString");
        SerializedProperty testIntProp = property.FindPropertyRelative("testInt");
        SerializedProperty testFloatListProp = property.FindPropertyRelative("testFloatList");

        // 라벨 표시
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // 필드 높이 계산
        Rect stringRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Rect intRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
        Rect listRect = new Rect(position.x, position.y + 2 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing), position.width, EditorGUI.GetPropertyHeight(testFloatListProp));

        // 각각의 필드를 그리기
        EditorGUI.PropertyField(stringRect, testStringProp);
        EditorGUI.PropertyField(intRect, testIntProp);
        EditorGUI.PropertyField(listRect, testFloatListProp, true);  // 리스트 필드 표시

        if(EditorGUI.EndChangeCheck()) {
            property.serializedObject.ApplyModifiedProperties();
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
        SerializedProperty testFloatListProp = property.FindPropertyRelative("testFloatList");

        // 전체 높이 계산
        float totalHeight = EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing * 2; // string과 int 필드 높이
        totalHeight += EditorGUI.GetPropertyHeight(testFloatListProp) + EditorGUIUtility.standardVerticalSpacing; // 리스트 필드 높이

        return totalHeight;
    }
}
