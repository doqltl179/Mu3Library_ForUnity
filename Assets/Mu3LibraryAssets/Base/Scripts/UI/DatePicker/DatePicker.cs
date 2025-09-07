using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.Events;

using Mu3Library.Editor;
using Mu3Library.Editor.FileUtil;

#endif

namespace Mu3Library.UI {
    public class DatePicker : MonoBehaviour {
        [SerializeField] private Button invisibleBackgroundButton;
        [SerializeField] private RectTransform rect;
        private Vector2 initializeRectSize = Vector2.zero;

        [Space(20)]
        [SerializeField] private bool disableWhenClickOutOfRect = true;

        [Header("YearMonth")]
        [SerializeField] private RectTransform yearMonthRect;
        [SerializeField] private RectTransform leftButtonsRect;
        [SerializeField] private RectTransform yearMonthButtonRect;
        [SerializeField] private RectTransform rightButtonsRect;

        [Space(20)]
        [SerializeField] private Button monthMinusOneButton;
        [SerializeField] private Button yearMinusOneButton;
        [SerializeField] private Button monthPlusOneButton;
        [SerializeField] private Button yearPlusOneButton;

        [Space(20)]
        [SerializeField] private Button yearMonthButton;
        [SerializeField] private TextMeshProUGUI yearMonthText;

        [Header("Dates")]
        [SerializeField] private RectTransform datesRect;
        [SerializeField] private VerticalLayoutGroup datesVerticalLeryoutGroup;
        private Vector2 initializeDatesRectSize = Vector2.zero;

        [Space(20)]
        [SerializeField] private RectTransform dateOfWeekLine;
        [SerializeField] private RectTransform[] dateOfWeekTextRects;
        [SerializeField] private TextMeshProUGUI[] dateOfWeekTexts;

        [Space(20)]
        [SerializeField] private RectTransform[] dateLines;
        [SerializeField] private DatePickerItem datePickerItemObj;
        [SerializeField] private DatePickerItem[] datePickerItems;
        private Vector2 initializeDateLinesSize = Vector2.zero;

        public DateTime CurrentSelectedDate => currentSelectedDate;
        private DateTime currentSelectedDate = DateTime.Now;

        public DateTime CurrentShowingDate => currentShowingDate;
        private DateTime currentShowingDate = DateTime.Now;

        public Action<DatePickerItem> OnClickedDateItem;



#if UNITY_EDITOR
        private void OnValidate() {
            InitComponent();
        }

        private void Reset() {
            InitComponent();
        }

        private void InitComponent() {
            if(invisibleBackgroundButton == null) {
                SetChildButtonProperties("InvisibleBackgroundButton", out invisibleBackgroundButton, transform.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                if(invisibleBackgroundButton != null) {
                    invisibleBackgroundButton.image.color = Vector4.zero;
                    invisibleBackgroundButton.image.rectTransform.anchoredPosition = Vector2.zero;
                    invisibleBackgroundButton.image.rectTransform.sizeDelta = new Vector2(4096, 4096);
                    invisibleBackgroundButton.transform.SetAsFirstSibling();

                    UtilFuncForEditor.RemoveAllListener(ref invisibleBackgroundButton);
                    UnityAction unityAction = new UnityAction(OnClickedInvisibleBackground);
                    UnityEventTools.AddPersistentListener(invisibleBackgroundButton.onClick, unityAction);
                }
            }

            if(rect == null) {
                SetChildRectProperties("Rect", out rect, (RectTransform)transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                if(rect != null) {
                    //rect.sizeDelta = new Vector2(400, 440);
                }
            }

            if(rect != null) {
                if(yearMonthRect == null) SetChildRectProperties("YearMonthRect", out yearMonthRect, rect, Vector2.up, Vector2.one, new Vector2(0.5f, 1));
                if(datesRect == null) SetChildRectProperties("DatesRect", out datesRect, rect, Vector2.up, Vector2.one, new Vector2(0.5f, 1));

                if(yearMonthRect != null) {
                    yearMonthRect.anchoredPosition = Vector2.zero;
                    yearMonthRect.sizeDelta = Vector2.up * 60;
                }
                if(datesRect != null) {
                    datesRect.anchoredPosition = Vector2.down * yearMonthRect.rect.height;
                    datesRect.sizeDelta = Vector2.up * (rect.rect.height - yearMonthRect.rect.height);
                }
            }

            #region YearMonth
            if(yearMonthRect != null) {
                if(leftButtonsRect == null) SetChildRectProperties("LeftButtonsRect", out leftButtonsRect, yearMonthRect, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f));
                if(yearMonthButtonRect == null) SetChildRectProperties("YearMonthButtonRect", out yearMonthButtonRect, yearMonthRect, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f));
                if(rightButtonsRect == null) SetChildRectProperties("RightButtonsRect", out rightButtonsRect, yearMonthRect, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f));

                if(yearMonthButtonRect != null) {
                    yearMonthButtonRect.anchoredPosition = Vector2.zero;
                    yearMonthButtonRect.sizeDelta = Vector2.right * yearMonthRect.rect.width * 0.5f;
                }
                if(leftButtonsRect != null) {
                    leftButtonsRect.anchoredPosition = Vector2.left * yearMonthButtonRect.rect.width * 0.5f;
                    leftButtonsRect.sizeDelta = Vector2.zero;
                }
                if(rightButtonsRect != null) {
                    rightButtonsRect.anchoredPosition = Vector2.right * yearMonthButtonRect.rect.width * 0.5f;
                    rightButtonsRect.sizeDelta = Vector2.zero;
                }
            }

            if(leftButtonsRect != null) {
                if(monthMinusOneButton == null) {
                    SetChildButtonProperties("MonthMinusOneButton", out monthMinusOneButton, leftButtonsRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));

                    UtilFuncForEditor.RemoveAllListener(ref monthMinusOneButton);
                    UnityAction<int> unityAction = new UnityAction<int>(MoveMonth);
                    UnityEventTools.AddIntPersistentListener(monthMinusOneButton.onClick, unityAction, -1);

                }
                if(yearMinusOneButton == null) {
                    SetChildButtonProperties("YearMinusOneButton", out yearMinusOneButton, leftButtonsRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));

                    UtilFuncForEditor.RemoveAllListener(ref yearMinusOneButton);
                    UnityAction<int> unityAction = new UnityAction<int>(MoveMonth);
                    UnityEventTools.AddIntPersistentListener(yearMinusOneButton.onClick, unityAction, -12);
                }

                float normalButtonSize = leftButtonsRect.rect.height * 0.55f;

                if(monthMinusOneButton != null) {
                    monthMinusOneButton.image.rectTransform.anchoredPosition = Vector3.zero;
                    monthMinusOneButton.image.rectTransform.sizeDelta = Vector2.one * normalButtonSize;
                }
                if(yearMinusOneButton != null) {
                    yearMinusOneButton.image.rectTransform.anchoredPosition = Vector2.left * normalButtonSize * 1.1f;
                    yearMinusOneButton.image.rectTransform.sizeDelta = new Vector2(normalButtonSize * 1.3f, normalButtonSize);
                }
            }

            if(rightButtonsRect != null) {
                if(monthPlusOneButton == null) {
                    SetChildButtonProperties("MonthPlusOneButton", out monthPlusOneButton, rightButtonsRect, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));

                    UtilFuncForEditor.RemoveAllListener(ref monthPlusOneButton);
                    UnityAction<int> unityAction = new UnityAction<int>(MoveMonth);
                    UnityEventTools.AddIntPersistentListener(monthPlusOneButton.onClick, unityAction, 1);
                }
                if(yearPlusOneButton == null) {
                    SetChildButtonProperties("YearPlusOneButton", out yearPlusOneButton, rightButtonsRect, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f));

                    UtilFuncForEditor.RemoveAllListener(ref yearPlusOneButton);
                    UnityAction<int> unityAction = new UnityAction<int>(MoveMonth);
                    UnityEventTools.AddIntPersistentListener(yearPlusOneButton.onClick, unityAction, 12);
                }

                float normalButtonSize = rightButtonsRect.rect.height * 0.55f;

                if(monthPlusOneButton != null) {
                    monthPlusOneButton.image.rectTransform.anchoredPosition = Vector3.zero;
                    monthPlusOneButton.image.rectTransform.sizeDelta = Vector2.one * normalButtonSize;
                }
                if(yearPlusOneButton != null) {
                    yearPlusOneButton.image.rectTransform.anchoredPosition = Vector2.right * normalButtonSize * 1.1f;
                    yearPlusOneButton.image.rectTransform.sizeDelta = new Vector2(normalButtonSize * 1.3f, normalButtonSize);
                }
            }

            if(yearMonthButtonRect != null) {
                if(yearMonthButton == null) SetChildButtonProperties("YearMonthButton", out yearMonthButton, yearMonthButtonRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                if(yearMonthText == null) {
                    SetChildTextProperties("YearMonthText", out yearMonthText, yearMonthButton.image.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                    if(yearMonthText != null) {
                        yearMonthText.text = "0000.00";
                        yearMonthText.color = Color.black;
                        yearMonthText.enableAutoSizing = true;

                        yearMonthText.rectTransform.offsetMin = Vector2.one * 4;
                        yearMonthText.rectTransform.offsetMax = Vector2.one * 4 * -1;
                    }
                }

                float normalButtonSize = rightButtonsRect.rect.height * 0.55f;

                if(yearMonthButton != null) {
                    yearMonthButton.image.rectTransform.anchoredPosition = Vector2.zero;
                    yearMonthButton.image.rectTransform.sizeDelta = new Vector2(yearMonthButtonRect.rect.width * 0.8f, normalButtonSize);
                }
            }
            #endregion

            #region Dates
            if(datesRect != null) {
                if(datesVerticalLeryoutGroup == null) {
                    datesVerticalLeryoutGroup = datesRect.GetComponent<VerticalLayoutGroup>();
                    if(datesVerticalLeryoutGroup == null) datesVerticalLeryoutGroup = datesRect.gameObject.AddComponent<VerticalLayoutGroup>();

                    datesVerticalLeryoutGroup.spacing = 3;
                    datesVerticalLeryoutGroup.childControlWidth = true;
                    datesVerticalLeryoutGroup.childControlHeight = true;
                    datesVerticalLeryoutGroup.childForceExpandWidth = true;
                    datesVerticalLeryoutGroup.childForceExpandHeight = true;
                }

                if(dateOfWeekLine == null) SetChildRectProperties("DateOfWeekLine", out dateOfWeekLine, datesRect, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                if(dateLines == null || dateLines.Length == 0) {
                    dateLines = new RectTransform[6];
                    for(int i = 0; i < dateLines.Length; i++) {
                        SetChildRectProperties($"DateLines_{i}", out dateLines[i], datesRect, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
                    }
                }

                datesVerticalLeryoutGroup.enabled = false;
                datesVerticalLeryoutGroup.enabled = true;

                Canvas.ForceUpdateCanvases();
            }

            if(dateOfWeekLine != null) {
                if(dateOfWeekTextRects == null || dateOfWeekTextRects.Length == 0) {
                    dateOfWeekTextRects = new RectTransform[7];
                    for(int i = 0; i < dateOfWeekTextRects.Length; i++) {
                        SetChildRectProperties($"DateOfWeekTextRect_{i}", out dateOfWeekTextRects[i], dateOfWeekLine, Vector2.zero, Vector2.up, new Vector2(0, 0.5f));

                        dateOfWeekTextRects[i].anchoredPosition = Vector2.right * dateOfWeekLine.rect.width * ((float)i / dateOfWeekTextRects.Length);
                        dateOfWeekTextRects[i].sizeDelta = Vector2.right * dateOfWeekLine.rect.width * (1.0f / dateOfWeekTextRects.Length);
                    }
                }

                if(dateOfWeekTexts == null || dateOfWeekTexts.Length == 0) {
                    dateOfWeekTexts = new TextMeshProUGUI[dateOfWeekTextRects.Length];
                    for(int i = 0; i < dateOfWeekTexts.Length; i++) {
                        SetChildTextProperties($"dateOfWeekText_{i}", out dateOfWeekTexts[i], dateOfWeekTextRects[i], Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
                        switch(i) {
                            case 0: dateOfWeekTexts[i].text = "SUN"; break;
                            case 1: dateOfWeekTexts[i].text = "MON"; break;
                            case 2: dateOfWeekTexts[i].text = "TUE"; break;
                            case 3: dateOfWeekTexts[i].text = "WED"; break;
                            case 4: dateOfWeekTexts[i].text = "THU"; break;
                            case 5: dateOfWeekTexts[i].text = "FRI"; break;
                            case 6: dateOfWeekTexts[i].text = "SAT"; break;
                        }
                        dateOfWeekTexts[i].fontSize = dateOfWeekTextRects[i].rect.height * 0.45f;
                        dateOfWeekTexts[i].color = Color.white;

                        dateOfWeekTexts[i].rectTransform.offsetMin = Vector2.one * 4;
                        dateOfWeekTexts[i].rectTransform.offsetMax = Vector2.one * 4 * -1;
                    }
                }
            }

            if(dateLines != null && dateLines.Length > 0) {
                if(datePickerItemObj == null) {
                    datePickerItemObj = FileFinder.FindPrefab<DatePickerItem>("", "", "");
                }

                if(datePickerItemObj != null) {
                    if(datePickerItems == null || datePickerItems.Length == 0) {
                        datePickerItems = new DatePickerItem[dateLines.Length * 7];

                        int lineIndex;
                        int itemIndex;
                        Transform t;
                        for(int i = 0; i < dateLines.Length; i++) {
                            lineIndex = i % 7;

                            for(int j = 0; j < 7; j++) {
                                itemIndex = i * 7 + j;

                                t = dateLines[lineIndex].Find($"DatePickerItem_{j}");
                                if(t != null) {
                                    datePickerItems[itemIndex] = t.GetComponent<DatePickerItem>();
                                }

                                if(datePickerItems[itemIndex] == null) {
                                    DatePickerItem item = (DatePickerItem)PrefabUtility.InstantiatePrefab(datePickerItemObj, dateLines[lineIndex]);
                                    item.name = $"DatePickerItem_{j}";
                                    item.transform.localScale = Vector3.one;

                                    datePickerItems[itemIndex] = item;
                                }

                                datePickerItems[itemIndex].AnchorMin = new Vector2(0, 0.5f);
                                datePickerItems[itemIndex].AnchorMax = new Vector2(0, 0.5f);
                                datePickerItems[itemIndex].Pivot = new Vector2(0, 0.5f);

                                datePickerItems[itemIndex].AnchorPos = Vector2.right * dateLines[lineIndex].rect.width * (j / 7.0f);
                                datePickerItems[itemIndex].Size = new Vector2(dateLines[lineIndex].rect.width * (1 / 7.0f), dateLines[lineIndex].rect.height);

                                datePickerItems[itemIndex].SetFontSizeToNormalSize();
                            }
                        }
                    }
                }
            }
            #endregion
        }

        /// <summary>
        /// Used On Editor Only.
        /// </summary>
        private void SetChildTextProperties(string name, out TextMeshProUGUI result, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot) {
            RectTransform textRect = null;
            SetChildRectProperties("YearMonthText", out textRect, parent, anchorMin, anchorMax, pivot);

            result = textRect.GetComponent<TextMeshProUGUI>();
            if(result == null) {
                result = textRect.gameObject.AddComponent<TextMeshProUGUI>();

                result.horizontalAlignment = HorizontalAlignmentOptions.Center;
                result.verticalAlignment = VerticalAlignmentOptions.Middle;
            }
        }

        /// <summary>
        /// Used On Editor Only.
        /// </summary>
        private void SetChildButtonProperties(string name, out Button result, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot) {
            RectTransform buttonRect = null;
            SetChildRectProperties(name, out buttonRect, parent, anchorMin, anchorMax, pivot);

            if(buttonRect == null) {
                result = null;

                return;
            }

            result = buttonRect.GetComponent<Button>();
            if(result == null) {
                buttonRect.gameObject.AddComponent<Image>();
                result = buttonRect.gameObject.AddComponent<Button>();
            }
        }

        /// <summary>
        /// Used On Editor Only.
        /// </summary>
        private void SetChildRectProperties(string name, out RectTransform result, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot) {
            if(parent == null) {
                Debug.LogError($"Parent is NULL. childName: {name}");
                result = null;

                return;
            }

            Transform t = parent.Find(name);
            if(t == null) {
                t = new GameObject(name).AddComponent<RectTransform>();
                t.SetParent(parent);

                t.localScale = Vector3.one;
            }

            result = t.GetComponent<RectTransform>();
            result.anchorMin = anchorMin;
            result.anchorMax = anchorMax;
            result.pivot = pivot;
        }
#endif

        private void Awake() {
            initializeRectSize = rect.rect.size;
            initializeDatesRectSize = datesRect.rect.size;
            initializeDateLinesSize = dateLines[0].rect.size;

            for(int i = 0; i < datePickerItems.Length; i++) {
                datePickerItems[i].OnClicked += OnClickedDateItemAction;
            }
        }

        private void OnEnable() {
            if(invisibleBackgroundButton != null) {
                invisibleBackgroundButton.gameObject.SetActive(disableWhenClickOutOfRect);
            }
        }

        #region Utility
        public void SetDateToNow() {
            currentSelectedDate = DateTime.Now;

            SetDate(DateTime.Now);
        }

        public void ChangeDate(DateTime date) {
            SetDate(date);
        }
        #endregion

        #region UI Event
        public void OnClickedInvisibleBackground() {
            gameObject.SetActive(false);
        }

        public void MoveMonth(int month) {
            SetDate(currentShowingDate.AddMonths(month));
        }
        #endregion

        #region Action
        private void OnClickedDateItemAction(DatePickerItem item) {
            ChangeCurrentSelectedDate(item);
        }
        #endregion

        private void ChangeCurrentSelectedDate(DatePickerItem item) {
            for(int i = 0; i < datePickerItems.Length; i++) {
                if(datePickerItems[i].IsSelected) {
                    datePickerItems[i].IsSelected = false;

                    break;
                }
            }

            item.IsSelected = true;

            currentSelectedDate = item.Date;
        }

        private void SetDate(DateTime date) {
            yearMonthText.text = date.ToString("yyyy.MM");

            DateTime firstDateOfMonth = new DateTime(date.Year, date.Month, 1);
            DateTime lastDateOfMonth = new DateTime(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

            int firstDateIndex = -1;
            switch(firstDateOfMonth.DayOfWeek) {
                case DayOfWeek.Sunday: firstDateIndex = 0; break;
                case DayOfWeek.Monday: firstDateIndex = 1; break;
                case DayOfWeek.Tuesday: firstDateIndex = 2; break;
                case DayOfWeek.Wednesday: firstDateIndex = 3; break;
                case DayOfWeek.Thursday: firstDateIndex = 4; break;
                case DayOfWeek.Friday: firstDateIndex = 5; break;
                case DayOfWeek.Saturday: firstDateIndex = 6; break;
            }

            int weekCount = (lastDateOfMonth.Day + firstDateIndex) / 7;
            if((lastDateOfMonth.Day + firstDateIndex) % 7 != 0) weekCount++;

            for(int i = 0; i < dateLines.Length; i++) {
                dateLines[i].gameObject.SetActive(i < weekCount);
            }
            rect.sizeDelta = new Vector2(initializeRectSize.x, initializeRectSize.y - initializeDateLinesSize.y * (dateLines.Length - weekCount));
            datesRect.sizeDelta = Vector2.up * (initializeDatesRectSize.y - initializeDateLinesSize.y * (dateLines.Length - weekCount));

            DateTime lastDateOfPrevMonth = date.AddMonths(-1);
            lastDateOfPrevMonth = new DateTime(lastDateOfPrevMonth.Year, lastDateOfPrevMonth.Month, DateTime.DaysInMonth(lastDateOfPrevMonth.Year, lastDateOfPrevMonth.Month));
            int itemIndex = 0;
            for(int i = 0; i < firstDateIndex; i++) {
                itemIndex = firstDateIndex - i - 1;

                datePickerItems[itemIndex].Date = lastDateOfPrevMonth.AddDays(-i);
                datePickerItems[itemIndex].enabled = false;
            }

            int dateNum = 0;
            itemIndex = firstDateIndex;
            for(int i = 0; i < lastDateOfMonth.Day; i++) {
                datePickerItems[itemIndex].Date = firstDateOfMonth.AddDays(dateNum);
                datePickerItems[itemIndex].enabled = true;

                datePickerItems[itemIndex].IsSelected =
                    datePickerItems[itemIndex].Date.Day == currentSelectedDate.Day &&
                    datePickerItems[itemIndex].Date.Month == currentSelectedDate.Month && 
                    datePickerItems[itemIndex].Date.Year == currentSelectedDate.Year;

                dateNum++;
                itemIndex++;
            }

            DateTime firstDateOfNextMonth = date.AddMonths(1);
            firstDateOfNextMonth = new DateTime(firstDateOfNextMonth.Year, firstDateOfNextMonth.Month, 1);
            dateNum = 0;
            for(; itemIndex < datePickerItems.Length; itemIndex++) {
                datePickerItems[itemIndex].Date = firstDateOfNextMonth.AddDays(dateNum);
                datePickerItems[itemIndex].enabled = false;

                dateNum++;
            }

            currentShowingDate = date;
        }
    }
}