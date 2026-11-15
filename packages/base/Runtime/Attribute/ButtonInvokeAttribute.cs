using System;

namespace Mu3Library.Attribute {
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class ButtonInvokeAttribute : System.Attribute {
        public string ButtonLabel => buttonLabel;
        private string buttonLabel = string.Empty;

        public float ButtonHeight { get; set; }



        public ButtonInvokeAttribute() {
        }

        public ButtonInvokeAttribute(string buttonLabel) {
            this.buttonLabel = buttonLabel;
        }

        public ButtonInvokeAttribute(string buttonLabel, float buttonHeight) : this(buttonLabel) {
            ButtonHeight = buttonHeight;
        }
    }
}
