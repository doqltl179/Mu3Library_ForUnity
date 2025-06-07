using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Sample.Analyse {
    public class LayerHardDriveInfos : AnalyseLayer {
        [SerializeField] private TextMeshProUGUI header;

        [Space(20)]
        [SerializeField] private HardDriveDirectoryInfoStruct directoryInfoObj;
        private List<HardDriveDirectoryInfoStruct> directoryInfos = new List<HardDriveDirectoryInfoStruct>();
        private Queue<HardDriveDirectoryInfoStruct> diQueue = new Queue<HardDriveDirectoryInfoStruct>();

        [Space(20)]
        [SerializeField] private Button backButton;

        public DriveMap CurrentDrive => currentDrive;
        private DriveMap currentDrive;

        private DirInfo rootDirectory = null;



        private void Start() {
            directoryInfoObj.gameObject.SetActive(false);
        }

        private void OnDisable() {
            if(currentDrive != null) {
                currentDrive.CancelLoad();
            }
        }

        #region UI Event
        public void OnClickRefresh() {
            UILoading.Instance.FadeHelper.FadeIn(0.6f, EasingFunction.Ease.EaseOutCubic, () => {
                SetData(currentDrive.CurrentDrive, () => {
                    UILoading.Instance.FadeHelper.FadeOut(0.6f, EasingFunction.Ease.EaseOutCubic);
                });
            });
        }

        public void OnClickBack() {
            if(rootDirectory.Parent == null) {
                return;
            }

            rootDirectory = rootDirectory.Parent;

            SetData();
        }

        public void OpenRootWithExplorer() {
            if(rootDirectory == null) {
                return;
            }

            System.Diagnostics.Process.Start("explorer.exe", rootDirectory.CurrentDirectory.FullName);
        }
        #endregion

        #region Utility
        public async void SetData(DriveInfo drive, Action callback = null) {
            currentDrive = new DriveMap(drive);

            await currentDrive.LoadDirectories();
            rootDirectory = currentDrive.CurrentDriveInfo;

            SetData();

            callback?.Invoke();
        }
        #endregion

        private void SetData() {
            if(directoryInfos.Count > 0) {
                foreach(HardDriveDirectoryInfoStruct obj in directoryInfos) {
                    obj.gameObject.SetActive(false);
                    diQueue.Enqueue(obj);
                }
                directoryInfos.Clear();
            }

            if(rootDirectory == null) {
                Debug.LogWarning("Root Directory is NULL.");

                header.text = "-";

                return;
            }

            foreach(DirInfo directory in rootDirectory.Dirs) {
                if(directory == null) {
                    continue;
                }

                HardDriveDirectoryInfoStruct obj = diQueue.Count > 0 ? 
                    diQueue.Dequeue() :
                    Instantiate(directoryInfoObj, directoryInfoObj.transform.parent);
                obj.gameObject.SetActive(true);

                obj.SetInfo(directory, (info) => {
                    rootDirectory = info;

                    SetData();
                });

                obj.HeaderText = $"{directory.CurrentDirectory.Name}";
                obj.InfoText = $"Directories: {directory.Dirs.Length}, Files: {directory.Files.Length}";
                obj.Amount = (float)(directory.CompressedSize / (double)rootDirectory.CompressedSize);

                directoryInfos.Add(obj);
            }

            // Sort
            for(int i = 0; i < directoryInfos.Count - 1; i++) {
                HardDriveDirectoryInfoStruct current = directoryInfos[i];
                for(int j = i + 1; j < directoryInfos.Count; j++) {
                    HardDriveDirectoryInfoStruct compare = directoryInfos[j];
                    if(current.Amount < compare.Amount) {
                        directoryInfos[j] = current;
                        directoryInfos[i] = compare;
                        current = compare;
                    }
                }

                current.transform.SetSiblingIndex(i);
            }

            header.text = $"({rootDirectory.CompressedSize / Mathf.Pow(1024, 3):F2}GB) {rootDirectory.CurrentDirectory.FullName}";

            backButton.gameObject.SetActive(rootDirectory.Parent != null);
        }
    }
}