using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mu3Library.Sample.Analyse {
    public class LayerHardDrives : AnalyseLayer {
        [SerializeField] private HardDriveInfoStruct hdInfoObj;
        private List<HardDriveInfoStruct> hdInfos = new List<HardDriveInfoStruct>();



        private void Start() {
            hdInfoObj.gameObject.SetActive(false);
        }

        public void SetData(Action<DriveInfo> onClick) {
            if(hdInfos.Count > 0) {
                foreach(HardDriveInfoStruct obj in hdInfos) {
                    Destroy(obj.gameObject);
                }
                hdInfos.Clear();
            }

            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach(DriveInfo drive in drives) {
                HardDriveInfoStruct obj = Instantiate(hdInfoObj, hdInfoObj.transform.parent);
                obj.gameObject.SetActive(true);

                obj.SetInfo(drive, onClick);

                hdInfos.Add(obj);
            }
        }
    }
}