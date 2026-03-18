using Mu3Library.UI.MVP;
using UnityEngine;

namespace Mu3Library.Sample.Template.MVP
{
    public class LoadingScreenView : View
    {
        [SerializeField] private Transform _logo;
        [SerializeField] private AnimationCurve _rotateCurve;
        [SerializeField, Range(0.0f, 2.0f)] private float _rotateTime;

        private float m_timer;
        private float _timer
        {
            get => m_timer;
            set
            {
                m_timer = value;
                _normalizedTime = value / _rotateTime;
            }
        }

        private float _normalizedTime;



        protected override void OpenStart()
        {
            base.OpenStart();

            _timer = 0.0f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer > _rotateTime)
            {
                _timer -= _rotateTime;
            }

            _logo.eulerAngles = Vector3.forward * -360.0f * _rotateCurve.Evaluate(_normalizedTime);
        }
    }
}