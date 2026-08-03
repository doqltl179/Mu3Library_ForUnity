using System;
using System.Collections;
using UnityEngine;

namespace Mu3Library.UI.MVP.Animation
{
    public class AnimationHandler : MonoBehaviour
    {
        private IView m_view;
        private IView _view
        {
            get
            {
                if (m_view == null)
                {
                    m_view = GetComponent<View>();
                }

                return m_view;
            }
        }

        private AnimationState _state = AnimationState.None;
        public AnimationState State => _state;

        [SerializeField] private bool _useTransition = true;
        [SerializeField, Range(0.0f, 2.0f)] private float _animationTime = 0.4f;
        private float _timeFactor = 0.0f;

        [Space(20)]
        [SerializeField] private AnimationConfig[] _animationConfigs;

        private IEnumerator _animationCoroutine = null;



        private void OnDisable()
        {
            Stop();
            _timeFactor = 0.0f;
        }

        public void Open()
        {
            AnimationConfig[] animationConfigs = _animationConfigs;
            if (animationConfigs == null)
            {
                throw new ArgumentNullException("source");
            }

            Animation(0.0f, 1.0f, AnimationState.Opening, AnimationState.Opened, animationConfigs, true);
        }

        public void Close()
        {
            AnimationConfig[] animationConfigs = _animationConfigs;
            if (animationConfigs == null)
            {
                throw new ArgumentNullException("source");
            }

            Animation(1.0f, 0.0f, AnimationState.Closing, AnimationState.Closed, animationConfigs, false);
        }

        public void Stop()
        {
            if (_animationCoroutine == null)
            {
                return;
            }

            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;

            switch (_state)
            {
                case AnimationState.Opening:
                    {
                        _state = AnimationState.Opened;

                        if (!_useTransition)
                        {
                            _timeFactor = 1.0f;
                        }
                    }
                    break;

                case AnimationState.Closing:
                    {
                        _state = AnimationState.Closed;

                        if (!_useTransition)
                        {
                            _timeFactor = 0.0f;
                        }
                    }
                    break;
            }
        }

        private void Animation(float factorFrom, float factorTo, AnimationState stayState, AnimationState endState, AnimationConfig[] animationConfigs, bool isOpening)
        {
            if (_useTransition)
            {
                Stop();
            }
            else
            {
                _timeFactor = factorFrom;
            }

            if (_animationCoroutine == null)
            {
                _state = stayState;

                _animationCoroutine = AnimationCoroutine(factorFrom, factorTo, endState, animationConfigs, isOpening);
                StartCoroutine(_animationCoroutine);
            }
            else
            {
                _state = endState;
            }
        }

        private IEnumerator AnimationCoroutine(float factorFrom, float factorTo, AnimationState endState, AnimationConfig[] animationConfigs, bool isOpening)
        {
            if (!HasAnimationConfig(animationConfigs, isOpening))
            {
                _timeFactor = factorTo;
                _state = endState;
                _animationCoroutine = null;
                yield break;
            }

            float timer = Mathf.Lerp(factorFrom, factorTo, _timeFactor) * _animationTime;
            while (timer < _animationTime)
            {
                timer += Time.deltaTime;
                _timeFactor = Mathf.Lerp(factorFrom, factorTo, timer / _animationTime);

                Animate(animationConfigs, isOpening);

                yield return null;
            }

            // 범위를 벗어나거나 while이 적용되지 않는 부분에 대한 후처리
            _timeFactor = factorTo;
            Animate(animationConfigs, isOpening);

            _state = endState;

            _animationCoroutine = null;
        }

        private static bool HasAnimationConfig(AnimationConfig[] animationConfigs, bool isOpening)
        {
            for (int i = 0; i < animationConfigs.Length; i++)
            {
                AnimationConfig animationConfig = animationConfigs[i];
                if (animationConfig == null)
                {
                    continue;
                }

                // Mirror the previous LINQ Any() evaluation, which invoked the first factory.
                GetAnimateFunc(animationConfig, isOpening);
                return true;
            }

            return false;
        }

        private void Animate(AnimationConfig[] animationConfigs, bool isOpening)
        {
            for (int i = 0; i < animationConfigs.Length; i++)
            {
                AnimationConfig animationConfig = animationConfigs[i];
                if (animationConfig == null)
                {
                    continue;
                }

                AnimateFunc animate = GetAnimateFunc(animationConfig, isOpening);
                animate(_view, _timeFactor);
            }
        }

        private static AnimateFunc GetAnimateFunc(AnimationConfig animationConfig, bool isOpening)
            => isOpening ? animationConfig.AnimateOpen() : animationConfig.AnimateClose();
    }
}
