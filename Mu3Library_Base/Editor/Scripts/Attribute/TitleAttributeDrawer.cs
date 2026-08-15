using Mu3Library.Attribute;
using UnityEditor;
using UnityEngine;

namespace Mu3Library.Editor.Attribute {
    [CustomPropertyDrawer(typeof(TitleAttribute))]
    public class TitleDecoratorDrawer : DecoratorDrawer {
        private const int fontSize = 18;
        private const int titleRectHeight = fontSize * 2;

        private GUIStyle _titleStyle;
        private GUISkin _cachedSkin;

        public override void OnGUI(Rect position) {
            TitleAttribute titleAttribute = (TitleAttribute)attribute;

            // 타이틀 텍스트를 그릴 영역 설정
            Rect titleRect = new Rect(position.x, position.y, position.width, titleRectHeight);
            GUIStyle style = GetTitleStyle(titleAttribute.TitleColor);

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

        private GUIStyle GetTitleStyle(Color titleColor) {
            if (_titleStyle == null || _cachedSkin != GUI.skin) {
                _cachedSkin = GUI.skin;
                _titleStyle = new GUIStyle(EditorStyles.boldLabel) {
                    fontSize = fontSize,
                    alignment = TextAnchor.LowerLeft,
                };
            }

            _titleStyle.normal.textColor = titleColor;
            return _titleStyle;
        }
    }

}
