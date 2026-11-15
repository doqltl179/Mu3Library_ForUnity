using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects
{
    public abstract class ScreenPassBase : ScriptableRenderPass, IDisposable
    {
        protected abstract string _shaderPath { get; }

        private Shader m_shader = null;
        private bool m_isShaderMissing = false;
        protected Shader _shader
        {
            get
            {
                if (m_shader == null && !m_isShaderMissing)
                {
                    m_shader = ScreenEffectShaders.Find(_shaderPath);

                    if (m_shader == null)
                    {
                        // A pass looks for its shader once: the material is asked for on every frame,
                        // for every active effect and camera, so a shader loaded later needs a new pass.
                        m_isShaderMissing = true;
                        ScreenEffectShaders.ReportMissing(_shaderPath);
                    }
                }

                return m_shader;
            }
        }

        private Material m_material = null;
        protected Material _material
        {
            get
            {
                if (_shader != null && m_material == null)
                {
                    m_material = CoreUtils.CreateEngineMaterial(_shader);
                }

                return m_material;
            }
        }



        public void Dispose()
        {
            if (m_material != null)
            {
                CoreUtils.Destroy(m_material);
                m_material = null;
            }

            OnDispose();
        }

        protected virtual void OnDispose()
        {

        }
    }
}