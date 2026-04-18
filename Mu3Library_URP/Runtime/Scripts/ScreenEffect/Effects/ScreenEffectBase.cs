using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Mu3Library.URP.ScreenEffect.Effects
{
    public abstract class ScreenEffectBase : IScreenEffect
    {
        public abstract Type PassType { get; }

        private bool m_activeSelf = false;
        protected bool _activeSelf => m_activeSelf;
        public bool ActiveSelf => _activeSelf;

        private bool m_isDisposed = false;
        protected bool _isDisposed => m_isDisposed;
        public bool IsDisposed => _isDisposed;



        public virtual void Dispose()
        {
            m_isDisposed = true;
        }

        public abstract void RequestEnqueuePass(ScriptableRenderer renderer, ScriptableRenderContext context);

        public void SetActive(bool active)
        {
            m_activeSelf = active;

            OnSetActive(active);
        }

        protected virtual void OnSetActive(bool active)
        {

        }

        public abstract void SetRenderPassEvent(RenderPassEvent renderPassEvent);
    }

    public abstract class ScreenEffectBase<TPass> : ScreenEffectBase where TPass : ScreenPassBase, new()
    {
        private TPass m_pass = null;
        protected TPass _pass
        {
            get
            {
                if (m_pass == null)
                {
                    m_pass = new TPass();
                }

                return m_pass;
            }
        }

        private Type m_passType = null;
        protected Type _passType
        {
            get
            {
                if (m_passType == null)
                {
                    m_passType = typeof(TPass);
                }

                return m_passType;
            }
        }
        public sealed override Type PassType => _passType;




        public sealed override void Dispose()
        {
            if (_isDisposed)
            {
                Debug.LogWarning($"** {_passType.Name} ** is already disposed. Dispose() should not be called multiple times.");

                return;
            }

            base.Dispose();

            m_pass?.Dispose();
            m_pass = null;

            SetActive(false);

            OnDispose();
        }

        protected virtual void OnDispose()
        {

        }

        public sealed override void RequestEnqueuePass(ScriptableRenderer renderer, ScriptableRenderContext context)
        {
            if (_isDisposed)
            {
                Debug.LogWarning($"** {_passType.Name} ** is already disposed. RequestEnqueuePass() cannot be called.");

                return;
            }

            OnRequestEnqueuePass(renderer, context);
        }

        protected virtual void OnRequestEnqueuePass(ScriptableRenderer renderer, ScriptableRenderContext context)
        {
            if (_isDisposed || !_activeSelf)
            {
                return;
            }

            renderer.EnqueuePass(_pass);
        }

        public sealed override void SetRenderPassEvent(RenderPassEvent renderPassEvent)
        {
            if (_isDisposed)
            {
                Debug.LogWarning($"** {_passType.Name} ** is already disposed. RenderPassEvent cannot be set.");

                return;
            }

            _pass.renderPassEvent = renderPassEvent;

            OnSetRenderPassEvent(renderPassEvent);
        }

        protected virtual void OnSetRenderPassEvent(RenderPassEvent renderPassEvent)
        {

        }
    }
}