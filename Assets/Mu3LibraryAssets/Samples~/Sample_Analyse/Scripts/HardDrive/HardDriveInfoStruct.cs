using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Analyse {
    public class HardDriveInfoStruct : MonoBehaviour {
        [SerializeField] private TextMeshProUGUI header;
        [SerializeField] private TextMeshProUGUI body;
        [SerializeField] private Image capacityImage;

        public DriveInfo CurrentInfo => currentInfo;
        private DriveInfo currentInfo;

        private Action<DriveInfo> onClick = null;



        #region Utility
        public void SetInfo(DriveInfo info, Action<DriveInfo> onClick) {
            header.text = info.Name;

            if(info.IsReady) {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Type: {info.DriveType}");
                sb.AppendLine($"Format: {info.DriveFormat}");

                float totalSize = info.TotalSize / Mathf.Pow(1024, 3);
                sb.AppendLine($"Size: {totalSize:F2}GB");

                float totalFreeSpace = info.TotalFreeSpace / Mathf.Pow(1024, 3);
                sb.AppendLine($"TotalFreeSpace: {totalFreeSpace:F2}GB");

                float availableFreeSpace = info.AvailableFreeSpace / Mathf.Pow(1024, 3);
                sb.AppendLine($"AvailableFreeSpace: {availableFreeSpace:F2}GB");

                capacityImage.fillAmount = availableFreeSpace / totalSize;

                body.text = sb.ToString();
            }
            else {
                body.text = "This Drive not Readable.";

                capacityImage.fillAmount = 0.0f;
            }

            currentInfo = info;
            this.onClick = onClick;
        }
        #endregion

        public void OnClick() {
            onClick?.Invoke(currentInfo);
        }
    }
}