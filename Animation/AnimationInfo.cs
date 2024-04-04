using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Animation {
    public static class AnimationInfo {
        private static AnimatorStateInfo m_stateInfo;
        public static float GetNormalizedTime(Animator animator, int layer) {
            m_stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            return m_stateInfo.normalizedTime;
        }
    }
}