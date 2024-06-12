using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class CameraManager : GenericSingleton<CameraManager> {
        private Camera cam = null;

        public Vector3 CamPos {
            get => cam.transform.position;
            set => cam.transform.position = value;
        }
        public Vector3 CamEuler {
            get => cam.transform.eulerAngles;
            set => cam.transform.eulerAngles = value;
        }
        public Vector3 CamForward {
            get => cam.transform.forward;
            set => cam.transform.forward = value;
        }
        public Vector3 CamRight {
            get => cam.transform.right;
            set => cam.transform.right = value;
        }
        public Vector3 CamUp {
            get => cam.transform.up;
            set => cam.transform.up = value;
        }
        public Quaternion CamRot {
            get => cam.transform.rotation;
            set => cam.transform.rotation = value;
        }



        #region Utility
        public void InitCamera() {
            if(cam == null) {
                cam = Camera.main;
                cam.transform.SetParent(transform);
            }
            else {
                var cameras = FindObjectsOfType<Camera>();
                for(int i = 0; i < cameras.Length; i++) {
                    if(cameras[i] != cam && cameras[i].CompareTag("MainCamera")) {
                        Destroy(cameras[i].gameObject);
                    }
                }
            }
        }
        #endregion
    }
}