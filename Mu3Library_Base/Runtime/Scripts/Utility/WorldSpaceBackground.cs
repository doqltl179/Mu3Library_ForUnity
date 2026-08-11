using Mu3Library.Attribute;
using Mu3Library.Extensions;
using UnityEngine;

namespace Mu3Library.Utility
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class WorldSpaceBackground : MonoBehaviour
    {
        private SpriteRenderer m_renderer;
        private SpriteRenderer _renderer => m_renderer ??= GetComponent<SpriteRenderer>();

        [Title("Properties")]
        [SerializeField] private bool _fitOnEnable = true;
        [SerializeField] private bool _moveToFrontWhenFit = true;
        [SerializeField, Min(0f)] private float _distance = 10f;

        private bool _fitPending;

        public bool FitOnEnable
        {
            get => _fitOnEnable;
            set => _fitOnEnable = value;
        }

        public bool MoveToFrontWhenFit
        {
            get => _moveToFrontWhenFit;
            set => _moveToFrontWhenFit = value;
        }

        public float Distance
        {
            get => _distance;
            set => _distance = value;
        }

        public Color Color
        {
            get => _renderer.color;
            set => _renderer.color = value;
        }

        public bool FlipX
        {
            get => _renderer.flipX;
            set => _renderer.flipX = value;
        }

        public bool FlipY
        {
            get => _renderer.flipY;
            set => _renderer.flipY = value;
        }

        public string SortingLayerName
        {
            get => _renderer.sortingLayerName;
            set => _renderer.sortingLayerName = value;
        }

        public int SortingOrder
        {
            get => _renderer.sortingOrder;
            set => _renderer.sortingOrder = value;
        }



        private void OnEnable()
        {
            if (_fitOnEnable)
            {
                _fitPending = true;
                TryFitWhenCameraReady();
            }
        }

        private void OnDisable()
        {
            _fitPending = false;
        }

        private void LateUpdate()
        {
            if (_fitPending)
            {
                TryFitWhenCameraReady();
            }
        }

        #region Utility
        [ButtonInvoke("Fit Background", ButtonHeight = 30f)]
        public void Fit()
            => Fit(_renderer.sprite);

        public void Fit(Sprite sprite)
            => Fit(Camera.main, sprite);

        public void Fit(Camera cam, Sprite sprite)
        {
            if (!cam.IsReady() || sprite == null)
            {
                return;
            }

            _renderer.sprite = sprite;

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            float distance = Mathf.Clamp(_distance, cam.nearClipPlane, cam.farClipPlane);
            if (!_moveToFrontWhenFit)
            {
                distance = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
                if (distance < cam.nearClipPlane || distance > cam.farClipPlane)
                {
                    return;
                }
            }

            Vector3 lowerLeft = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distance));
            Vector3 lowerRight = cam.ViewportToWorldPoint(new Vector3(1f, 0f, distance));
            Vector3 upperLeft = cam.ViewportToWorldPoint(new Vector3(0f, 1f, distance));
            float viewportWidth = Vector3.Distance(lowerLeft, lowerRight);
            float viewportHeight = Vector3.Distance(lowerLeft, upperLeft);
            float envelopeScale = Mathf.Max(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);

            Vector3 localScale = transform.localScale;
            transform.localScale = new Vector3(envelopeScale, envelopeScale, localScale.z);

            if (_moveToFrontWhenFit)
            {
                Vector3 viewportCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
                transform.SetPositionAndRotation(viewportCenter, cam.transform.rotation);
                transform.position += viewportCenter - _renderer.bounds.center;
            }
        }

        private void TryFitWhenCameraReady()
        {
            Camera cam = Camera.main;
            if (!cam.IsReady())
            {
                return;
            }

            Fit(cam, _renderer.sprite);
            _fitPending = false;
        }
        #endregion
    }
}
