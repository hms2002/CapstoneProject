using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Simple camera shake without Cinemachine.
    /// Attach to the Camera (usually MainCamera) and call Shake(amplitude).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimpleCameraShake2D : MonoBehaviour
    {
        [SerializeField] private float duration = 0.12f;
        [SerializeField] private float frequency = 25f;
        [SerializeField] Transform target;
        private Vector3 _baseLocalPos => target != null ?  target.position : transform.position;
        private float _timeLeft;
        private float _amplitude;

        private void Awake()
        {
        }

        private void OnDisable()
        {
            transform.localPosition = _baseLocalPos;
            _timeLeft = 0f;
            _amplitude = 0f;
        }

        public void Shake(float amplitude)
        {
            if (amplitude <= 0f) return;
            _amplitude = Mathf.Max(_amplitude, amplitude);
            _timeLeft = Mathf.Max(_timeLeft, duration);
        }

        private void LateUpdate()
        {
            if (_timeLeft <= 0f) return;

            _timeLeft -= Time.deltaTime;
            float t = Mathf.Max(0f, _timeLeft);

            // cheap pseudo-noise
            float x = Mathf.Sin(Time.time * frequency) * _amplitude;
            float y = Mathf.Cos(Time.time * frequency * 0.9f) * _amplitude;

            transform.localPosition = _baseLocalPos + new Vector3(x, y, 0f);

            if (t <= 0f)
            {
                transform.localPosition = _baseLocalPos;
                _amplitude = 0f;
            }
        }
    }
}
