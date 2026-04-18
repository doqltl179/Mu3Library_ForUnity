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
        protected Shader _shader
        {
            get
            {
                if (m_shader == null)
                {
                    m_shader = Shader.Find(_shaderPath);
                }

                if (m_shader == null)
                {
                    Debug.LogError($"Shader not found. path: {_shaderPath}");
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