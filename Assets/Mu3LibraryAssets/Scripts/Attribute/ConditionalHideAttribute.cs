using UnityEngine;
using System;

//Original version of the ConditionalHideAttribute created by Brecht Lecluyse (www.brechtos.com)
//Modified by: Sebastian Lague

namespace Mu3Library.Attribute {
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property |
        AttributeTargets.Class | AttributeTargets.Struct, Inherited = true)]
    public class ConditionalHideAttribute : PropertyAttribute {
        public string conditionalSourceField;
        public bool showIfTrue;
        public int enumIndex;



        public ConditionalHideAttribute(string boolVariableName, bool showIfTrue) {
            conditionalSourceField = boolVariableName;
            this.showIfTrue = showIfTrue;
        }

        public ConditionalHideAttribute(string enumVariableName, int enumIndex) {
            conditionalSourceField = enumVariableName;
            this.enumIndex = enumIndex;
        }

    }
}