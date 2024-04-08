using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mu3Library.Animation {
    public class AnimationInfo {
        private Animator m_animator;
        private AnimatorStateInfo[] m_stateInfos;
        private AnimatorClipInfo[][] m_clipInfos;



        public AnimationInfo(Animator animator) {
            m_animator = animator;
            m_stateInfos = new AnimatorStateInfo[animator.layerCount];
            m_clipInfos = new AnimatorClipInfo[animator.layerCount][];
        }

        public void UpdateStateInfo(int layer) {
            if(IsLayerOutOfRange(layer)) {
                Debug.LogWarning($"Layer Index Out Of Range. layer: ${layer}");

                return;
            }

            m_stateInfos[layer] = m_animator.GetCurrentAnimatorStateInfo(layer);
        }

        public void UpdateStateInfoAll() {
            for(int i = 0; i < m_animator.layerCount; i++) {
                m_stateInfos[i] = m_animator.GetCurrentAnimatorStateInfo(i);
                m_clipInfos[i] = m_animator.GetCurrentAnimatorClipInfo(i);
            }
        }

        public void PlayAnimation(string name, int layer, float normalizedTime) {
            m_animator.Play(name, layer, normalizedTime);
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

        public int GetPlayingClipCount(int layer) {
            return m_clipInfos[layer].Length;
        }

        public bool IsPlayingAnimationClipWithName(string name) {
            for(int i = 0; i < m_clipInfos.Length; i++) {
                for(int j = 0; j < m_clipInfos[i].Length; j++) {
                    if(m_clipInfos[i][j].clip.name.Contains(name)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsClipTransitioning(int layer) {
            return m_clipInfos[layer].Length > 1;
        }
    }
}