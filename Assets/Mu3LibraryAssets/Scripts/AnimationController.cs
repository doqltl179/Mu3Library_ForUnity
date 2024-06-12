using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class AnimationController : MonoBehaviour {
        [SerializeField] private Animator animator;
        private const string AnimatorPropertyName_MoveBlend = "MoveBlend";



        #region Utility
        public float GetValue_MoveBlend() => animator.GetFloat(AnimatorPropertyName_MoveBlend);
        public void SetValue_MoveBlend(float value) => animator.SetFloat(AnimatorPropertyName_MoveBlend, value);
        #endregion
    }
}