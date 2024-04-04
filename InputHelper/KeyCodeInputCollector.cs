using Mu3Library.Singleton;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.InputHelper {
    public class KeyCodeInputCollector : GenericSingleton<KeyCodeInputCollector> {
        private Dictionary<KeyCode, KeyData> keyInfos = null;
        private KeyCode[] keyArray = null;



        private void Update() {
            if(keyInfos != null) {
                foreach(KeyCode key in keyArray) {
                    keyInfos[key].UpdateKey(Input.GetKeyDown(key), Input.GetKeyUp(key));
                }
            }
        }

        #region Utility
        public void SetCollectKeys(KeyCode[] keys) {
            keyInfos = new Dictionary<KeyCode, KeyData>();
            KeyData temp;
            foreach(KeyCode key in keys) {
                if(keyInfos.TryGetValue(key, out temp)) {

                }
                else {
                    keyInfos.Add(key, new KeyData(key));
                }
            }

            keyArray = keys;
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
                    Debug.Log($"DoubleDown. Key: {Key.ToString()}");
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