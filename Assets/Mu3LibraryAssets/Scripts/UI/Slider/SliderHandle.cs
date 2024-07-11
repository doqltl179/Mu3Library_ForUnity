using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Mu3Library.UI {
    public class SliderHandle : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        [SerializeField] private RectTransform rectTransform;
        public Vector2 AnchorPos {
            get => rectTransform.anchoredPosition;
            set => rectTransform.anchoredPosition = value;
        }
        public float AngleDeg {
            get => rectTransform.eulerAngles.z;
            set => rectTransform.eulerAngles = Vector3.forward * value;
        }
        public Vector2 AnchorMin {
            get => rectTransform.anchorMin;
            set => rectTransform.anchorMin = value;
        }
        public Vector2 AnchorMax {
            get => rectTransform.anchorMax;
            set => rectTransform.anchorMax = value;
        }

        public Action<PointerEventData> OnPointerDownAction;
        public Action<PointerEventData> OnPointerUpAction;



#if UNITY_EDITOR
        private void OnValidate() {
            InitComponent();
        }

        private void Reset() {
            InitComponent();
        }

        private void InitComponent() {
            if(rectTransform == null) rectTransform = GetComponent<RectTransform>();
        }
#endif

        private void Awake() {
            if(rectTransform == null) rectTransform = GetComponent<RectTransform>();
        }

        #region Interface
        public void OnPointerDown(PointerEventData eventData) {
            OnPointerDownAction?.Invoke(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) {
            OnPointerUpAction?.Invoke(eventData);
        }
        #endregion
    }
}