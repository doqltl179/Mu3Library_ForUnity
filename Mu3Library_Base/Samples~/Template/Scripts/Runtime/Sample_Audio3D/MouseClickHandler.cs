using System;
using UnityEngine;

namespace Mu3Library.Sample.Template.Audio3D {
    public class MouseClickHandler : MonoBehaviour {
        public event Action<Vector3> OnClick;



        private void Update() {
            if(Input.GetMouseButtonDown(0)) {
                OnClick?.Invoke(Input.mousePosition);
            }
        }
    }
}