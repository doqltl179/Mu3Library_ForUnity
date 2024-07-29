using Mu3Library.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mu3Library.UI {
    [RequireComponent(typeof(Image))]
    public class DatePickerItem : MonoBehaviour, IPointerClickHandler {
        [SerializeField] private RectTransform rectTransform;
        public Vector2 AnchorPos {
            get => rectTransform.anchoredPosition;
            set => rectTransform.anchoredPosition = value;
        }
        public Vector2 AnchorMin {
            get => rectTransform.anchorMin;
            set => rectTransform.anchorMin = value;
        }
        public Vector2 AnchorMax {
            get => rectTransform.anchorMax;
            set => rectTransform.anchorMax = value;
        }
        public Vector2 Pivot {
            get => rectTransform.pivot;
            set => rectTransform.pivot = value;
        }
        public Vector2 Size {
            get => rectTransform.rect.size;
            set => rectTransform.sizeDelta = value;
        }

        [Space(20)]
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI dateText;
        public Action<DatePickerItem> OnClicked;

        [Header("Disable")]
        [SerializeField] private Color disabledBackgroundColor = UtilFunc.GetChangedAlphaColor(Color.gray * 0.4f, 1.0f);
        [SerializeField] private Color disabledTextColor = Color.gray;

        [Header("Enable")]
        [SerializeField] private Color enabledBackgroundColor = Color.white;
        [SerializeField] private Color enabledTextColor = Color.black;

        [Header("Select")]
        [SerializeField] private Color selectedBackgroundColor = UtilFunc.GetChangedAlphaColor(Color.blue + (Color.yellow * 0.2f), 1.0f);
        [SerializeField] private Color selectedTextColor = Color.green;

        public bool IsSelected {
            get => isSelected;
            set {
                if(isSelected != value) {
                    if(value) {
                        background.color = selectedBackgroundColor;
                        dateText.color = selectedTextColor;
                    }
                    else {
                        if(enabled) {
                            background.color = enabledBackgroundColor;
                            dateText.color = enabledTextColor;
                        }
                        else {
                            background.color = disabledBackgroundColor;
                            dateText.color = disabledTextColor;
                        }
                    }

                    isSelected = value;
                }
            }
        }
        private bool isSelected = false;

        public DateTime Date {
            get => date;
            set {
                dateText.text = value.Day.ToString();

                date = value;
            }
        }
        private DateTime date = DateTime.Now;



        private void OnEnable() {
            if(isSelected) {
                background.color = selectedBackgroundColor;
                dateText.color = selectedTextColor;
            }
            else {
                background.color = enabledBackgroundColor;
                dateText.color = enabledTextColor;
            }
        }

        private void OnDisable() {
            background.color = disabledBackgroundColor;
            dateText.color = disabledTextColor;
        }

#if UNITY_EDITOR
        private void OnValidate() {
            InitComponent();
        }

        private void Reset() {
            InitComponent();
        }

        private void InitComponent() {
            if(rectTransform == null) rectTransform = GetComponent<RectTransform>();

            if(background == null) background = GetComponent<Image>();
            if(dateText == null) {
                Transform t = transform.Find("DateText");
                if(t == null) {
                    t = new GameObject("DateText").transform;
                    t.SetParent(transform);

                    t.localScale = Vector3.one;
                }

                dateText = t.GetComponent<TextMeshProUGUI>();
                if(dateText == null) {
                    dateText = t.gameObject.AddComponent<TextMeshProUGUI>();
                    dateText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    dateText.verticalAlignment = VerticalAlignmentOptions.Middle;

                    dateText.rectTransform.anchorMin = Vector2.zero;
                    dateText.rectTransform.anchorMax = Vector2.one;
                    dateText.rectTransform.pivot = new Vector2(0.5f, 0.5f);

                    dateText.rectTransform.offsetMin = Vector2.one * 4;
                    dateText.rectTransform.offsetMax = Vector2.one * 4 * -1;
                }
            }
        }
#endif

        #region Utility
        public void SetFontSizeToNormalSize() {
            dateText.fontSize = rectTransform.rect.height * 0.45f;
        }
        #endregion

        #region Interface
        public void OnPointerClick(PointerEventData eventData) {
            OnClicked?.Invoke(this);
        }
        #endregion
    }
}