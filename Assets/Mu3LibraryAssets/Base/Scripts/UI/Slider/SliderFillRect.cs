using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mu3Library.UI {
    public class SliderFillRect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image image;

        public float FillAmount {
            get => image.fillAmount;
            set => image.fillAmount = value;
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
            if(image == null) image = GetComponent<Image>();
        }
#endif

        #region Utility
        public void GetLocalCorners(Vector3[] corners) {
            rectTransform.GetLocalCorners(corners);
        }

        public void GetWorldCorners(Vector3[] corners) {
            rectTransform.GetWorldCorners(corners);
        }
        #endregion

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