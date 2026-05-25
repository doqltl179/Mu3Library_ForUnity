using System;
using UnityEngine;

namespace Mu3Library.Attribute {
    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class ButtonInvokeAttribute : PropertyAttribute {
        public string MethodName => methodName;
        private string methodName = string.Empty;

        public string ButtonLabel => buttonLabel;
        private string buttonLabel = string.Empty;

        public bool DrawProperty => drawProperty;
        private bool drawProperty = true;



        public ButtonInvokeAttribute(string methodName) {
            this.methodName = methodName;
            buttonLabel = methodName;
        }

        public ButtonInvokeAttribute(string methodName, string buttonLabel) {
            this.methodName = methodName;
            this.buttonLabel = string.IsNullOrEmpty(buttonLabel) ? methodName : buttonLabel;
        }

        public ButtonInvokeAttribute(string methodName, string buttonLabel, bool drawProperty) {
            this.methodName = methodName;
            this.buttonLabel = string.IsNullOrEmpty(buttonLabel) ? methodName : buttonLabel;
            this.drawProperty = drawProperty;
        }
    }
}