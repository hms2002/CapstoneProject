using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - CombatHeightState2D의 가짜 높이 값을 본체 visual y offset과 그림자 연출로 표현한다.
    /// - root/collider는 바닥 좌표에 고정하고, 그림자를 제외한 본체 렌더만 위로 띄운다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHeightPresentation2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private CombatHeightState2D heightState;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform shadowRoot;
        [SerializeField] private SpriteRenderer shadowRenderer;

        [Header("Motion")]
        [SerializeField, Min(0.01f)] private float dampTimeSeconds = 0.08f;

        [Header("Shadow")]
        [SerializeField, Range(0f, 1f)] private float airborneShadowAlphaScale = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float airborneShadowScale = 0.75f;

        private Vector3 visualBaseLocalPosition;
        private Vector3 shadowBaseLocalScale;
        private Color shadowBaseColor;
        private float currentVisualHeight;
        private float visualHeightVelocity;

        private void Awake()
        {
            CacheReferences();
            CaptureBaseValues();
            ApplyImmediate();
        }

        private void OnEnable()
        {
            CacheReferences();

            if (heightState != null)
                heightState.Changed += HandleHeightChanged;

            ApplyImmediate();
        }

        private void OnDisable()
        {
            if (heightState != null)
                heightState.Changed -= HandleHeightChanged;
        }

        private void OnValidate()
        {
            dampTimeSeconds = Mathf.Max(0.01f, dampTimeSeconds);
            CacheReferences();
        }

        private void LateUpdate()
        {
            if (heightState == null || visualRoot == null)
                return;

            float targetHeight = heightState.VisualHeight;
            currentVisualHeight = Mathf.SmoothDamp(
                currentVisualHeight,
                targetHeight,
                ref visualHeightVelocity,
                dampTimeSeconds);

            ApplyVisualHeight(currentVisualHeight);
            ApplyShadow(heightState.IsAirborne);
        }

        private void CacheReferences()
        {
            if (heightState == null)
                heightState = GetComponent<CombatHeightState2D>();
        }

        private void CaptureBaseValues()
        {
            if (visualRoot != null)
                visualBaseLocalPosition = visualRoot.localPosition;

            if (shadowRoot != null)
                shadowBaseLocalScale = shadowRoot.localScale;

            if (shadowRenderer != null)
                shadowBaseColor = shadowRenderer.color;
        }

        private void ApplyImmediate()
        {
            if (heightState == null)
                return;

            currentVisualHeight = heightState.VisualHeight;
            visualHeightVelocity = 0f;
            ApplyVisualHeight(currentVisualHeight);
            ApplyShadow(heightState.IsAirborne);
        }

        private void ApplyVisualHeight(float height)
        {
            if (visualRoot == null)
                return;

            Vector3 nextPosition = visualBaseLocalPosition;
            nextPosition.y += Mathf.Max(0f, height);
            visualRoot.localPosition = nextPosition;
        }

        private void ApplyShadow(bool isAirborne)
        {
            if (shadowRoot != null)
            {
                float scale = isAirborne ? airborneShadowScale : 1f;
                shadowRoot.localScale = shadowBaseLocalScale * scale;
            }

            if (shadowRenderer != null)
            {
                Color color = shadowBaseColor;
                color.a *= isAirborne ? airborneShadowAlphaScale : 1f;
                shadowRenderer.color = color;
            }
        }

        private void HandleHeightChanged(CombatHeightState2D _)
        {
            if (!isActiveAndEnabled)
                ApplyImmediate();
        }
    }
}
