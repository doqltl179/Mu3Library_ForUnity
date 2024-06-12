using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Scene {
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class SceneUILayer : MonoBehaviour {
        protected RectTransform rectTransform;
        public bool SetActive {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        protected CanvasGroup canvasGroup;
        public float Alpha {
            get => canvasGroup.alpha;
            set => canvasGroup.alpha = value;
        }
        public bool Interactable {
            get => canvasGroup.interactable;
            set => canvasGroup.interactable = value;
        }
        public bool BlocksRaycasts {
            get => canvasGroup.blocksRaycasts;
            set => canvasGroup.blocksRaycasts = value;
        }
        public bool IgnoreParentGroups {
            get => canvasGroup.ignoreParentGroups;
            set => canvasGroup.ignoreParentGroups = value;
        }



        protected virtual void Awake() {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        public virtual void OnActivate() { }
        public virtual void OnDeActivate() { }
    }
}