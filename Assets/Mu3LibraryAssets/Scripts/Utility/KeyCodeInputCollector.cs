using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
    public class KeyCodeInputCollector : GenericSingleton<KeyCodeInputCollector> {
        private Dictionary<KeyCode, KeyData> keyInfos = null;



        private void Update() {
            if(keyInfos != null) {
                foreach(KeyCode key in keyInfos.Keys) {
                    keyInfos[key].UpdateKey(Input.GetKeyDown(key), Input.GetKeyUp(key));
                }
            }
        }

        #region Utility
        public void InitCollectKeys() {
            keyInfos = new Dictionary<KeyCode, KeyData>();
        }

        public void AddCollectKeys(KeyCode[] keys) {
            KeyData temp;
            foreach(KeyCode key in keys) {
                if(keyInfos.TryGetValue(key, out temp)) {
                    Debug.Log($"`{key}` already exist in list.");
                }
                else {
                    keyInfos.Add(key, new KeyData(key));
                }
            }
        }

        public void AddCollectKey(KeyCode key) {
            KeyData temp;
            if(keyInfos.TryGetValue(key, out temp)) {
                Debug.Log($"`{key}` already exist in list.");
            }
            else {
                keyInfos.Add(key, new KeyData(key));
            }
        }

        public void RemoveCollectKeys(KeyCode[] keys) {
            foreach(KeyCode key in keys) {
                keyInfos.Remove(key);
            }
        }

        public void RemoveCollectKey(KeyCode key) {
            keyInfos.Remove(key);
        }

        public bool GetKeyDown(KeyCode key) => keyInfos[key].KeyDown;
        public bool GetKeyUp(KeyCode key) => keyInfos[key].KeyUp;
        public bool GetKey(KeyCode key) => keyInfos[key].KeyPressing;

        public bool GetKeyDoubleDown(KeyCode key) => keyInfos[key].DoubleDown;

        public float GetKeyPressingTime(KeyCode key) => keyInfos[key].KeyPressingTime;
        #endregion
    }

    public class KeyData {
        public KeyCode Key { get; private set; }

        public bool KeyDown { get; private set; }
        public bool KeyUp { get; private set; }
        public bool KeyPressing { get; private set; }
        public float KeyPressingTime { get; private set; }

        private const float DoubleDownInterval = 0.25f;
        public bool DoubleDown { get; private set; }

        public System.DateTime KeyDownTime { get; private set; }
        public System.DateTime KeyUpTime { get; private set; }



        public KeyData(KeyCode key) {
            Key = key;

            KeyDown = false;
            KeyUp = false;
            KeyPressing = false;

            DoubleDown = false;

            KeyDownTime = System.DateTime.MinValue;
            KeyUpTime = System.DateTime.MinValue;
        }

        #region Utility
        public void UpdateKey(bool down, bool up) {
            if(down) {
                KeyDown = true;

                if((System.DateTime.Now - KeyDownTime).TotalMilliseconds * 0.001f < DoubleDownInterval) {
                    DoubleDown = true;
                    //Debug.Log($"DoubleDown. Key: {Key}");
                }
                KeyDownTime = System.DateTime.Now;

                KeyPressing = true;
            }
            else {
                KeyDown = false;
                DoubleDown = false;
            }

            if(up) {
                KeyUp = true;

                KeyUpTime = System.DateTime.Now;

                DoubleDown = false;

                KeyPressing = false;
            }
            else {
                KeyUp = false;
            }

            if(KeyPressing) KeyPressingTime = (float)(System.DateTime.Now - KeyDownTime).TotalMilliseconds * 0.001f;
            else KeyPressingTime = 0.0f;
        }
        #endregion
    }
}