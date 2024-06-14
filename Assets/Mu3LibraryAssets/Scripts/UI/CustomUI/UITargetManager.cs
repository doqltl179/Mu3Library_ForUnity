using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mu3Library.UI.Custom {
    public class UITargetManager : MonoBehaviour {
        private GraphicRaycaster graphicRaycaster;
        private EventSystem eventSystem;

        private PointerEventData pointerEventData;
        private List<RaycastResult> rayResults = new List<RaycastResult>();

        public IUIRaycaster CurrentTarget {
            get => currentTarget;
            set {
                if(currentTarget != value) {
                    if(currentTarget != null) {
                        currentTarget.OnExit(pointerEventData);
                    }

                    if(value != null) {
                        value.OnEnter(pointerEventData);
                    }

                    currentTarget = value;
                }
            }
        }
        private IUIRaycaster currentTarget = null;



        private void Awake() {
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            eventSystem = GetComponentInChildren<EventSystem>();
        }

        private void Start() {
            pointerEventData = new PointerEventData(eventSystem);
        }

        private void Update() {
            pointerEventData.position = Input.mousePosition;

            rayResults.Clear();
            graphicRaycaster.Raycast(pointerEventData, rayResults);

            IUIRaycaster outValue = null;
            if(rayResults.Count > 0) {
                if(rayResults[0].gameObject.TryGetComponent(out outValue)) {

                }

                //StringBuilder logBuilder = new StringBuilder();
                //for(int i = 0; i < rayResults.Count; i++) {
                //    logBuilder.AppendLine($"depth: {rayResults[i].depth}, name: {rayResults[i].gameObject.name}");
                //}
                //Debug.Log(logBuilder.ToString());
            }
            CurrentTarget = outValue;

            currentTarget?.OnMove(pointerEventData);
        }
    }
}