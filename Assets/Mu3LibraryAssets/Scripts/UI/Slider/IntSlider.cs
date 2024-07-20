using Mu3Library.Utility;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mu3Library.UI {
    public class IntSlider : MonoBehaviour {
        [SerializeField] private SliderFillRect fillRect;

        [SerializeField] private Image fillImage;
        [SerializeField] private SliderHandle handle;

        [Space(20)]
        [SerializeField] private int intValueMin = 0;
        [SerializeField] private int intValueMax = 10;
        private float minMaxDiff = 10 - 0;
        private float sliderSplitValue = 1.0f / (10 - 0);

        public int IntValue {
            get => intValue;
            set {
                int clampValue = value;
                if(clampValue < intValueMin) clampValue = intValueMin;
                else if(clampValue > intValueMax) clampValue = intValueMax;

                if(intValue != clampValue) {
                    intValue = clampValue;

                    OnIntValueChanged?.Invoke(clampValue);

                    ChangeSliderValue();
                }
            }
        }
        [SerializeField] private int intValue = 5;

        private float sliderValue = 0.5f;

        private Vector3[] localCorners = new Vector3[4];
        private Vector3 localCornerLB;
        private Vector3 localCornerLT;
        private Vector3 localCornerRT;
        private Vector3 localCornerRB;

        private Vector3[] worldCorners = new Vector3[4];
        private Vector3 worldCornerLB;
        private Vector3 worldCornerLT;
        private Vector3 worldCornerRT;
        private Vector3 worldCornerRB;

        private IEnumerator checkPointerCoroutine;

        [Space(20)]
        public UnityEvent<int> OnIntValueChanged;
        public UnityEvent<float> OnSliderValueChanged;



#if UNITY_EDITOR
        private void OnValidate() {
            InitComponent();
        }

        private void Reset() {
            InitComponent();
        }

        private void InitComponent() {
            if(fillRect == null) fillRect = transform.Find("FillRect")?.GetComponent<SliderFillRect>();
            if(fillRect != null) {
                if(fillImage == null) fillImage = fillRect.transform.Find("FillImage")?.GetComponent<Image>();
                if(handle == null) handle = fillRect.transform.Find("Handle")?.GetComponent<SliderHandle>();

                UpdateCorner();
            }

            minMaxDiff = intValueMax - intValueMin;
            sliderSplitValue = 1.0f / minMaxDiff;

            if(intValue < intValueMin) intValue = intValueMin;
            else if(intValue > intValueMax) intValue = intValueMax;

            ChangeSliderValue();
        }
#endif

        private void Awake() {
            fillRect.OnPointerDownAction += OnPointerDown;
            fillRect.OnPointerUpAction += OnPointerUp;

            handle.OnPointerDownAction += OnPointerDown;
            handle.OnPointerUpAction += OnPointerUp;
        }

        private void OnDestroy() {
            fillRect.OnPointerDownAction -= OnPointerDown;
            fillRect.OnPointerUpAction -= OnPointerUp;

            handle.OnPointerDownAction -= OnPointerDown;
            handle.OnPointerUpAction -= OnPointerUp;
        }

        #region Utility
        public void SetMinMaxValue(int min, int max) {
            SetMinMaxValue(min, max, max);
        }

        public void SetMinMaxValue(int min, int max, int defaultValue) {
            //UpdateCorner();

            intValueMin = min;
            intValueMax = max;
            intValue = defaultValue;

            minMaxDiff = intValueMax - intValueMin;
            sliderSplitValue = 1.0f / minMaxDiff;

            if(intValue < intValueMin) intValue = intValueMin;
            else if(intValue > intValueMax) intValue = intValueMax;

            ChangeSliderValue();
        }
        #endregion

        #region Action
        public void OnPointerDown(PointerEventData eventData) {
            if(checkPointerCoroutine == null) {
                checkPointerCoroutine = CheckPointerCoroutine();
                StartCoroutine(checkPointerCoroutine);
            }
        }

        public void OnPointerUp(PointerEventData eventData) {
            if(checkPointerCoroutine != null) {
                StopCoroutine(checkPointerCoroutine);
                checkPointerCoroutine = null;
            }
        }

        private IEnumerator CheckPointerCoroutine() {
            UpdateCorner();

            float pointerSliderValue;
            while(true) {
                pointerSliderValue = PointerToSliderValue(Input.mousePosition);
                IntValue = SliderValueToIntValue(pointerSliderValue);

                yield return null;
            }

            checkPointerCoroutine = null;
        }
        #endregion

        private int SliderValueToIntValue(float value) {
            return Mathf.FloorToInt((value + sliderSplitValue * 0.5f) / sliderSplitValue);
        }

        private float PointerToSliderValue(Vector3 pointer) {
            return UtilFunc.InverseLerp(worldCornerLB, worldCornerRT, pointer);
        }

        private void ChangeSliderValue() {
            float newValue = (intValue - intValueMin) / minMaxDiff;
            if(sliderValue != newValue) {
                fillImage.fillAmount = newValue;

                handle.AnchorMin = new Vector2(newValue, 0.5f);
                handle.AnchorMax = handle.AnchorMin;

                sliderValue = newValue;

                OnSliderValueChanged?.Invoke(newValue);
            }
        }

        private void UpdateCorner() {
            fillRect.GetLocalCorners(localCorners);
            localCornerLB = localCorners[0];
            localCornerLT = localCorners[1];
            localCornerRT = localCorners[2];
            localCornerRB = localCorners[3];

            fillRect.GetWorldCorners(worldCorners);
            worldCornerLB = worldCorners[0];
            worldCornerLT = worldCorners[1];
            worldCornerRT = worldCorners[2];
            worldCornerRB = worldCorners[3];
        }
    }
}