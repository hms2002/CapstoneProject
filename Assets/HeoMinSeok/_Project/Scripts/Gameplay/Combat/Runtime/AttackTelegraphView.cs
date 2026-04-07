using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공통 공격 예고 사각형/원형의 위치, 회전, 크기, 진행도 색상 변화를 렌더링한다.
    /// - 실제 공격 판정 로직은 모르고, 전달받은 Spec과 Style만 시각적으로 표현한다.
    /// </summary>
    public sealed class AttackTelegraphView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Transform fillRoot;
        [SerializeField] private Transform borderRoot;
        [SerializeField] private SpriteRenderer fillRenderer;
        [SerializeField] private SpriteRenderer borderRenderer;

        private bool baseScaleCaptured;
        private Vector3 fillBaseScale = Vector3.one;
        private Vector3 borderBaseScale = Vector3.one;

        private AttackTelegraphStyle activeStyle;
        private float startTime;
        private float duration;
        private bool isVisible;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            if (fillRoot == null && fillRenderer != null)
                fillRoot = fillRenderer.transform;
            if (borderRoot == null && borderRenderer != null)
                borderRoot = borderRenderer.transform;

            CaptureBaseScaleIfNeeded();
            HideImmediate();
        }

        private void Update()
        {
            if (!isVisible)
                return;

            float normalized = duration <= 0f
                ? 1f
                : Mathf.Clamp01((Time.time - startTime) / duration);

            ApplyStyle(normalized);
        }

        /// <summary>
        /// 책임 :
        /// - 공격 예고 도형의 위치, 크기, 회전과 진행도 시작 상태를 초기화한다.
        /// </summary>
        public void Show(AttackTelegraphSpec spec, AttackTelegraphStyle fallbackStyle = null)
        {
            CaptureBaseScaleIfNeeded();

            activeStyle = spec.style != null ? spec.style : fallbackStyle;
            duration = Mathf.Max(0f, spec.duration);
            startTime = Time.time;
            isVisible = true;

            gameObject.SetActive(true);
            transform.position = spec.center;
            transform.rotation = Quaternion.Euler(0f, 0f, spec.rotationDeg);

            ApplyShapeScale(spec.shape, spec.size);
            ApplyStyle(0f);
        }

        /// <summary>
        /// 책임 :
        /// - 공격 예고 연출을 즉시 숨기고 렌더러를 비활성화한다.
        /// </summary>
        public void HideImmediate()
        {
            isVisible = false;

            if (fillRenderer != null)
                fillRenderer.enabled = false;
            if (borderRenderer != null)
                borderRenderer.enabled = false;
        }

        private void CaptureBaseScaleIfNeeded()
        {
            if (baseScaleCaptured)
                return;

            baseScaleCaptured = true;
            fillBaseScale = fillRoot != null ? fillRoot.localScale : Vector3.one;
            borderBaseScale = borderRoot != null ? borderRoot.localScale : Vector3.one;
        }

        private void ApplyShapeScale(AttackTelegraphShape shape, Vector2 size)
        {
            Vector2 safeSize = new Vector2(Mathf.Max(0.0001f, size.x), Mathf.Max(0.0001f, size.y));

            switch (shape)
            {
                case AttackTelegraphShape.Circle:
                {
                    float diameter = Mathf.Max(safeSize.x, safeSize.y);
                    safeSize = new Vector2(diameter, diameter);
                    break;
                }
            }

            if (fillRoot != null)
            {
                fillRoot.localScale = new Vector3(
                    fillBaseScale.x * safeSize.x,
                    fillBaseScale.y * safeSize.y,
                    fillBaseScale.z);
            }

            if (borderRoot != null)
            {
                borderRoot.localScale = new Vector3(
                    borderBaseScale.x * safeSize.x,
                    borderBaseScale.y * safeSize.y,
                    borderBaseScale.z);
            }
        }

        private void ApplyStyle(float normalized)
        {
            if (fillRenderer == null && borderRenderer == null)
                return;

            float curved = activeStyle != null && activeStyle.progressCurve != null
                ? Mathf.Clamp01(activeStyle.progressCurve.Evaluate(normalized))
                : normalized;

            float blinkMultiplier = 1f;
            if (activeStyle != null &&
                normalized >= activeStyle.blinkStartNormalized &&
                activeStyle.blinkFrequency > 0f)
            {
                float blinkWave = Mathf.Sin(Time.time * activeStyle.blinkFrequency * Mathf.PI * 2f);
                blinkMultiplier = Mathf.Lerp(activeStyle.blinkAlphaMin, 1f, (blinkWave + 1f) * 0.5f);
            }

            if (fillRenderer != null)
            {
                Color fillColor = activeStyle != null
                    ? Color.Lerp(activeStyle.fillColorStart, activeStyle.fillColorEnd, curved)
                    : new Color(1f, 0.85f, 0.2f, 0.2f);

                fillColor.a *= blinkMultiplier;
                fillRenderer.color = fillColor;
                fillRenderer.enabled = true;
            }

            if (borderRenderer != null)
            {
                Color borderColor = activeStyle != null
                    ? Color.Lerp(activeStyle.borderColorStart, activeStyle.borderColorEnd, curved)
                    : new Color(1f, 0.9f, 0.4f, 1f);

                borderColor.a *= blinkMultiplier;
                borderRenderer.color = borderColor;
                borderRenderer.enabled = true;
            }
        }
    }
}
