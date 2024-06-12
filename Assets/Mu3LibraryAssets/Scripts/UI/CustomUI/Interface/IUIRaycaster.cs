using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public interface IUIRaycaster {
    public void OnEnter(PointerEventData data);
    public void OnExit(PointerEventData data);
    public void OnMove(PointerEventData data);
}
