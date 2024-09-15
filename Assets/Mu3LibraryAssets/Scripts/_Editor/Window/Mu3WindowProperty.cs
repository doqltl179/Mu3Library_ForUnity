using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Editor.Window {
    /// <summary>
    /// <br/> 해당 'ScriptableObject'는 에디터의 데이터를 저장하기 위해 사용한다.
    /// <br/> 원래는 'EditorPrefs'를 사용하려 했으나, 관리가 너무 불편함.
    /// </summary>
    public abstract class Mu3WindowProperty : ScriptableObject {
        protected string stateTitle = "";
        protected string stateInfo = "";

        public System.Action<string, string, float> OnStateInfoChanged;



        /// <summary>
        /// When called recompile.
        /// </summary>
        protected virtual void OnEnable() {
            OnStateInfoChanged = null;
            stateTitle = "";
            stateInfo = "";

            Refresh();
        }

        public abstract void Refresh();

        protected void ChangeStateInfo(string title = "", string info = "", float progress = 0) {
            if(stateTitle != title || stateInfo != info) {
                stateTitle = title;
                stateInfo = info;

                OnStateInfoChanged?.Invoke(stateTitle, stateInfo, progress);
            }
        }
    }
}