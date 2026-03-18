using System;
using UnityEngine;

namespace Mu3Library.Attribute {
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class TitleAttribute : PropertyAttribute {
        public string TitleText => titleText;
        private string titleText = "";

        public Color TitleColor => titleColor;
        private Color titleColor = Color.white;




        public TitleAttribute(string text) {
            titleText = text;
            titleColor = Color.white;
        }

        public TitleAttribute(string text, float r, float g, float b) {
            titleText = text;
            titleColor = new Color(r, g, b);
        }

        public TitleAttribute(string text, string hexColor) {
            titleText = text;

            if(string.IsNullOrEmpty(hexColor)) {

            }
            else if(hexColor[0] != '#') {
                hexColor = "#" + hexColor;
            }

            if(ColorUtility.TryParseHtmlString(hexColor, out var color)) {
                titleColor = color;
            }
        }
    }
}