using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Animation {
    public class AnimationInfo {
        private Animator m_animator;
        private AnimatorStateInfo[] m_stateInfos;



        public AnimationInfo(Animator animator) {
            m_animator = animator;
            m_stateInfos = new AnimatorStateInfo[animator.layerCount];
        }

        public void UpdateStateInfo(int layer) {
            if(IsLayerOutOfRange(layer)) {
                Debug.LogWarning($"Layer Index Out Of Range. layer: ${layer}");

                return;
            }

            m_stateInfos[layer] = m_animator.GetCurrentAnimatorStateInfo(layer);
        }

        public void UpdateStateInfoAll() {
            for(int i = 0; i < m_stateInfos.Length; i++) {
                m_stateInfos[i] = m_animator.GetCurrentAnimatorStateInfo(i);
            }
        }

        public float GetNormalizedTime(int layer) {
            if(IsLayerOutOfRange(layer)) {
                Debug.LogWarning($"Layer Index Out Of Range. layer: ${layer}");

                return 0;
            }

            return m_stateInfos[layer].normalizedTime;
        }

        public float GetNormalizedTimeClamp01(int layer) {
            if(IsLayerOutOfRange(layer)) {
                Debug.LogWarning($"Layer Index Out Of Range. layer: ${layer}");

                return 0;
            }

            return m_stateInfos[layer].normalizedTime % 1;
        }

        public int GetLoopCount(int layer) {
            if(IsLayerOutOfRange(layer)) {
                Debug.LogWarning($"Layer Index Out Of Range. layer: ${layer}");

                return 0;
            }

            return Mathf.FloorToInt(m_stateInfos[layer].normalizedTime);
        }

        private bool IsLayerOutOfRange(int layer) {
            return layer < 0 || m_stateInfos.Length <= layer;
        }
    }
}