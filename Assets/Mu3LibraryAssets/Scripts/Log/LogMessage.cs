using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Mu3Library.Log {
    public class LogMessage : MonoBehaviour {
        private RectTransform rectTransform;

        [SerializeField] private TextMeshProUGUI type;
        [SerializeField] private TextMeshProUGUI message;



        private void Awake() {
            if(rectTransform == null) rectTransform = GetComponent<RectTransform>();
        }

        #region Utility
        public void SetLog(LogType logType, string log) {
            type.text = logType.ToString();
            switch(logType) {
                case LogType.Log: type.color = Color.white; break;

                case LogType.Warning: type.color = Color.yellow; break;

                case LogType.Exception:
                case LogType.Error: type.color = Color.red; break;

                default: type.color = Color.magenta; break;
            }

            message.text = log;

            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, message.preferredHeight);
        }
        #endregion
    }
}