using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Log {
    public class LogCapture : MonoBehaviour {
        [SerializeField] private CanvasGroup canvasGroup;
        public float Alpha {
            get => canvasGroup.alpha;
            set => canvasGroup.alpha = value;
        }

        [Space(20)]
        [SerializeField] private RectTransform anchorLB;
        [SerializeField] private RectTransform anchorRT;
        [SerializeField] private RectTransform logParent;

        [Space(20)]
        [SerializeField] private LogMessage logObj;
        private List<LogMessage> logs = new List<LogMessage>();

        [Space(20)]
        [SerializeField] private TextMeshProUGUI logCountText;
        private Dictionary<LogType, int> logCount = new Dictionary<LogType, int>();

        [Space(20)]
        public bool printStackTrace_Log = false;
        public bool printStackTrace_Warning = false;
        public bool printStackTrace_Error = true;
        public bool printStackTrace_Exception = true;



        private void OnEnable() {
            Application.logMessageReceived += LogMessageReceived;
        }

        private void OnDisable() {
            Application.logMessageReceived -= LogMessageReceived;
        }

        private void Start() {
            logCountText.text = "";

            logParent.sizeDelta = new Vector2(anchorRT.anchoredPosition.x - anchorLB.anchoredPosition.x, logParent.sizeDelta.y);
        }

        private void Update() {
            if(Input.GetKeyDown(KeyCode.Alpha1)) {
                canvasGroup.alpha = 0.86f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            else if(Input.GetKeyDown(KeyCode.Alpha0)) {
                canvasGroup.alpha = 0.0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        #region Utility
        public void ClearLog() {
            for(int i = 0; i < logs.Count; i++) {
                UnityObjectPoolManager.Instance.AddObject(logs[i]);
            }
            logs.Clear();

            logCountText.text = "";
            logCount.Clear();
        }
        #endregion

        #region Action
        private void LogMessageReceived(string condition, string stackTrace, LogType type) {
            try {
                LogMessage lm = UnityObjectPoolManager.Instance.GetObject<LogMessage>();
                if(lm == null) {
                    lm = Instantiate(logObj);
                }
                lm.transform.SetParent(logParent);
                lm.transform.localScale = Vector3.one;
                lm.gameObject.SetActive(true);

                string logText = condition;
                switch(type) {
                    case LogType.Log: if(printStackTrace_Log) logText += $"\n\n[Stack]\n{stackTrace}"; break;
                    case LogType.Warning: if(printStackTrace_Warning) logText += $"\n\n[Stack]\n{stackTrace}"; break;
                    case LogType.Error: if(printStackTrace_Error) logText += $"\n\n[Stack]\n{stackTrace}"; break;
                    case LogType.Exception: if(printStackTrace_Exception) logText += $"\n\n[Stack]\n{stackTrace}"; break;
                }
                lm.SetLog(type, logText);

                logs.Add(lm);

                AddLogCount(type);

                StartCoroutine(RefreshLogRect(lm));
            }
            catch {

            }
        }

        private IEnumerator RefreshLogRect(LogMessage log) {
            yield return null;
            log.gameObject.SetActive(false);

            yield return null;
            log.gameObject.SetActive(true);
        }
        #endregion

        StringBuilder logCountBuilder = new StringBuilder();
        private void AddLogCount(LogType type) {
            int count = -1;
            if(logCount.TryGetValue(type, out count)) {
                logCount[type] = count + 1;
            }
            else {
                logCount.Add(type, 1);
            }

            logCountBuilder.Clear();
            int keyCount = logCount.Keys.Count;
            int index = 0;
            foreach(LogType t in logCount.Keys) {
                logCountBuilder.Append($"{t}: {logCount[t]}");

                index++;
                if(index < keyCount) {
                    logCountBuilder.Append(", ");
                }
            }

            logCountText.text = logCountBuilder.ToString();
        }
    }
}