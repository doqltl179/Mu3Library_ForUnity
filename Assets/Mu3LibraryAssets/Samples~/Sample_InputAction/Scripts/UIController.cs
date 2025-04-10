using Mu3Library.Utility;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Demo.InputAction {
    public class UIController : MonoBehaviour {
        [SerializeField] private GameObject menu;
        [SerializeField] private Button menuButtonSrc;

        [Space(20)]
        [SerializeField] private UIKeyBinding page_keyBinding;
        private GameObject[] pages;

        private int currentPageIndex = -1;



        private void Awake() {
            menu.SetActive(false);
            menuButtonSrc.gameObject.SetActive(false);

            pages = new GameObject[] {
                page_keyBinding.gameObject, 
            };
            for(int i = 0; i < pages.Length; i++) {
                int pageIndex = i;
                string buttonName = pages[i].gameObject.name;
                AddMenuButton(pageIndex, buttonName);

                pages[i].SetActive(false);
            }
        }

        private void AddMenuButton(int pageIndex, string buttonName) {
            Button btnObj = Instantiate(menuButtonSrc, menuButtonSrc.transform.parent);
            btnObj.name = buttonName;
            btnObj.gameObject.SetActive(true);

            TextMeshProUGUI text = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if(text != null) {
                text.text = buttonName;
            }

            btnObj.onClick.RemoveAllListeners();

            int idx = pageIndex;
            btnObj.onClick.AddListener(() => {
                for(int i = 0; i < pages.Length; i++) {
                    pages[i].SetActive(i == idx);
                }

                menu.SetActive(false);

                currentPageIndex = idx;
            });
        }

        private void Start() {
            InputSystemHelper.Instance.Controls.UI.Cancel.performed += CancelPerformed;
        }

        private void OnDestroy() {
            InputSystemHelper.Instance.Controls.UI.Cancel.performed -= CancelPerformed;
        }

        #region InputSystem
        private void CancelPerformed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
            if(!menu.activeSelf) {
                foreach(GameObject page in pages) {
                    page.SetActive(false);
                }

                menu.SetActive(true);

                currentPageIndex = -1;
            }
            else {
                menu.SetActive(false);
            }
        }
        #endregion
    }
}