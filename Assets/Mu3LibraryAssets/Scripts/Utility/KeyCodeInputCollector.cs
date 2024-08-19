using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Utility {
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
        public void UpdateKey() {
            KeyDown = Input.GetKeyDown(Key);
            KeyUp = Input.GetKeyUp(Key);

            if(KeyDown) {
                if((System.DateTime.Now - KeyDownTime).TotalMilliseconds * 0.001f < DoubleDownInterval) {
                    DoubleDown = true;
                    //Debug.Log($"DoubleDown. Key: {Key}");
                }
                KeyDownTime = System.DateTime.Now;

                KeyPressing = true;
            }

            if(KeyUp) {
                KeyUpTime = System.DateTime.Now;

                DoubleDown = false;

                KeyPressing = false;
            }

            if(KeyPressing) KeyPressingTime = (float)(System.DateTime.Now - KeyDownTime).TotalMilliseconds * 0.001f;
            else KeyPressingTime = 0.0f;
        }
        #endregion
    }
}