using System;
using System.Collections;
using UnityEngine;

namespace Mu3Library.Sample.Template.Splash
{
    [RequireComponent(typeof(CanvasGroup))]
    public class SplashUIAnimation : MonoBehaviour
    {
        private CanvasGroup m_canvasGroup;
        private CanvasGroup _canvasGroup
        {
            get
            {
                if (m_canvasGroup == null)
                {
                    m_canvasGroup = GetComponent<CanvasGroup>();
                }

                return m_canvasGroup;
            }
        }

        [SerializeField] private bool _playOnStart = true;
        [SerializeField, Range(0.1f, 5.0f)] private float _animationTime = 3.0f;

        private bool _isAnimationEnded = false;
        public bool IsAnimationEnded => _isAnimationEnded;

        private IEnumerator _animationCoroutine;

        public event Action OnAnimationEnd;



        private void Start()
        {
            if (_playOnStart)
            {
                PlayAnimation();
            }
        }

        #region Utility
        public void StopAnimation()
        {
            if(_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
                _animationCoroutine = null;
            }
        }

        public void PlayAnimation()
        {
            if (_animationCoroutine == null)
            {
                _isAnimationEnded = false;

                _animationCoroutine = AnimationCoroutine();
                StartCoroutine(_animationCoroutine);
            }
        }
        #endregion

        private IEnumerator AnimationCoroutine()
        {
            const float fadeRatio = 0.8f;
            float fadeTime = _animationTime * fadeRatio;
            float waitTime = _animationTime - fadeTime;

            float timer = 0.0f;
            while (timer < fadeTime)
            {
                timer += Time.deltaTime;
                float alpha = Mathf.Clamp01(timer / fadeTime);
                _canvasGroup.alpha = alpha;

                yield return null;
            }
            _canvasGroup.alpha = 1.0f;

            yield return new WaitForSeconds(waitTime);

            _isAnimationEnded = true;

            _animationCoroutine = null;

            OnAnimationEnd?.Invoke();
        }
    }
}