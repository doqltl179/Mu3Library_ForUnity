using System;
using Mu3Library.Sample.Template.Global;
using UnityEngine;

namespace Mu3Library.Sample.Template.Audio3D {
    public class MouseClickHandler : MonoBehaviour {
        public event Action<Vector3> OnClick;



        private void Update() {
            if(SampleInput.GetMouseButtonDown(0)) {
                OnClick?.Invoke(SampleInput.MousePosition);
            }
        }
    }
}