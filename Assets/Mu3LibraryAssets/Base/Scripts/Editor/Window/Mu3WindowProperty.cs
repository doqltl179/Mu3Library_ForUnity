using Mu3Library.Attribute;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Editor.Window {
    /// <summary>
    /// <br/> 해당 'ScriptableObject'는 에디터의 데이터를 저장하기 위해 사용한다.
    /// <br/> 원래는 'EditorPrefs'를 사용하려 했으나, 관리가 너무 불편함.
    /// </summary>
    public abstract class Mu3WindowProperty : ScriptableObject {
        public bool Foldout_Debug {
            get => foldout_debug;
            set => foldout_debug = value;
        }
        [Title("Debug Properties")]
        [SerializeField] private bool foldout_debug = true;



        /// <summary>
        /// When called recompile.
        /// </summary>
        protected virtual void OnEnable() {
            Refresh();
        }

        public abstract void Refresh();
    }
}