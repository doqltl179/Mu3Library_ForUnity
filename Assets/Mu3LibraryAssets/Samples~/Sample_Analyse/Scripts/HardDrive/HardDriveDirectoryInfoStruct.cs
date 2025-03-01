using System.IO;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Mu3Library.Demo.Analyse {
    public class HardDriveDirectoryInfoStruct : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI header;
        [SerializeField] private TextMeshProUGUI info;
        [SerializeField] private TextMeshProUGUI amount;
        [SerializeField] private Image capacityImage;

        public string HeaderText {
            get => header.text;
            set => header.text = value;
        }
        public string InfoText {
            get => info.text;
            set => info.text = value;
        }
        public float Amount {
            get => capacityImage.fillAmount;
            set {
                capacityImage.fillAmount = value;

                amount.text = $"{value * 100:F2}%";
            }
        }

        public DirInfo CurrentInfo => currentInfo;
        private DirInfo currentInfo;

        private Action<DirInfo> onClick = null;



        #region Utility
        public void SetInfo(DirInfo info, Action<DirInfo> onClick) {
            currentInfo = info;
            this.onClick = onClick;
        }
        #endregion

        public void OnClick() {
            onClick?.Invoke(currentInfo);
        }
    }
}