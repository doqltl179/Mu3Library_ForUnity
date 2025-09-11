#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Mu3Library.Attribute {
    [CustomPropertyDrawer(typeof(TitleAttribute))]
    public class TitleDecoratorDrawer : DecoratorDrawer {
        private const int fontSize = 18;
        private const int titleRectHeight = fontSize * 2;



        public override void OnGUI(Rect position) {
            TitleAttribute titleAttribute = (TitleAttribute)attribute;

            // 타이틀 텍스트를 그릴 영역 설정
            Rect titleRect = new Rect(position.x, position.y, position.width, titleRectHeight);
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel) {
                fontSize = fontSize,
                alignment = TextAnchor.LowerLeft,
                normal = { 
                    textColor = titleAttribute.TitleColor // 텍스트 색상 설정
                }
            };

            // 타이틀 텍스트 표시
            EditorGUI.LabelField(titleRect, titleAttribute.TitleText, style);

            // 밑줄을 그릴 영역 설정
            Rect underlineRect = new Rect(position.x, position.y + fontSize * 2, position.width, 1);
            EditorGUI.DrawRect(underlineRect, Color.gray);
        }

        public override float GetHeight() {
            // 타이틀과 밑줄의 높이 반환
            return titleRectHeight + 8;
        }
    }

}

#endif