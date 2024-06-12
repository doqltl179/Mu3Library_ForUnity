using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;


#if UNITY_EDITOR
using UnityEditor.Events;
#endif

[RequireComponent(typeof(Button))]
public class AnimationButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IPointerMoveHandler {
    protected RectTransform rectTransform;
    protected Button button;

    public bool IsSelected {
        get => isSelected;
        set {
            if(isSelected != value) {
                OnButtonSelectChanged(value);

                isSelected = value;
            }
        }
    }
    private bool isSelected = false;

    public bool ButtonEnabled {
        get => button.enabled;
        set {
            if(button.enabled != value) {
                OnButtonEnabledChanged(value);

                button.enabled = value;
            }
        }
    }

    public Action OnClickAction;



#if UNITY_EDITOR
    private void Reset() {
        Button btn = GetComponent<Button>();
        if(btn != null) {
            btn.transition = Selectable.Transition.None;

            Navigation nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            Button.ButtonClickedEvent onClickEvent = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(onClickEvent, OnClick);
            btn.onClick = onClickEvent;
        }
    }

    private void AddTriggerEvent(EventTrigger trigger, EventTriggerType eventType, UnityEngine.Events.UnityAction<BaseEventData> eventAction) {
        EventTrigger.Entry entry = new EventTrigger.Entry() { eventID = eventType };
        UnityEventTools.AddPersistentListener(entry.callback, eventAction);
        trigger.triggers.Add(entry);
    }
#endif

    protected virtual void Awake() {
        rectTransform = GetComponent<RectTransform>();
        button = GetComponent<Button>();
    }

    #region Action
    public virtual void OnButtonEnabledChanged(bool value) { Debug.Log($"{nameof(AnimationButton)}. OnButtonEnabledChanged."); }

    public virtual void OnButtonSelectChanged(bool value) { Debug.Log($"{nameof(AnimationButton)}. OnButtonSelectChanged."); }

    public virtual void OnClick() { 
        Debug.Log($"{nameof(AnimationButton)}. OnClick.");

        OnClickAction?.Invoke();
    }
    #endregion

    protected virtual void PointerEnterAnimation(PointerEventData data) { }
    protected virtual void PointerExitAnimation(PointerEventData data) { }
    protected virtual void PointerMoveAnimation(PointerEventData data) { }
    protected virtual void PointerClickAnimation(PointerEventData data) { }

    public void OnPointerEnter(PointerEventData eventData) {
        PointerEnterAnimation(eventData);
    }

    public void OnPointerExit(PointerEventData eventData) {
        PointerExitAnimation(eventData);
    }

    public void OnPointerClick(PointerEventData eventData) {
        PointerClickAnimation(eventData);
    }

    public void OnPointerMove(PointerEventData eventData) {
        PointerMoveAnimation(eventData);
    }
}
