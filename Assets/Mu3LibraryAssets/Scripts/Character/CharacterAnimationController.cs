using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Character {
    public class CharacterAnimationController : MonoBehaviour {
        [SerializeField] private Animator animator;

        #region Animator Property Name

        #region Int
        private const string AnimatorPropertyName_CharacterType = "CharacterType";
        #endregion

        #region Float
        private const string AnimatorPropertyName_MoveBlend = "MoveBlend";
        #endregion

        #endregion



#if UNITY_EDITOR
        private void OnValidate() {
            FindInspectorComponentProperties();
        }

        private void Reset() {
            FindInspectorComponentProperties();
        }

        private void FindInspectorComponentProperties() {
            if(animator == null) animator = GetComponent<Animator>();
        }
#endif

        #region Utility
        public int GetValue_CharacterType() => GetValue_Int(AnimatorPropertyName_CharacterType);
        public void SetValue_CharacterType(int value) => SetValue_Int(AnimatorPropertyName_CharacterType, value);

        public float GetValue_MoveBlend() => GetValue_Float(AnimatorPropertyName_MoveBlend);
        public void SetValue_MoveBlend(float value) => SetValue_Float(AnimatorPropertyName_MoveBlend, value);
        #endregion

        private int GetValue_Int(string propertyName) => animator.GetInteger(propertyName);
        private void SetValue_Int(string propertyName, int value) => animator.SetInteger(propertyName, value);

        private float GetValue_Float(string propertyName) => animator.GetFloat(propertyName);
        private void SetValue_Float(string propertyName, float value) => animator.SetFloat(propertyName, value);
    }
}