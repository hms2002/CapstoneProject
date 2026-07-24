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
        [SerializeField] private bool createFallbackShadow = true;
        [SerializeField] private Vector3 fallbackShadowLocalScale = new Vector3(0.7f, 0.22f, 1f);
        [SerializeField] private Color fallbackShadowColor = new Color(0f, 0f, 0f, 0.32f);
        [SerializeField] private string fallbackShadowSortingLayerName = "Entity";
        [SerializeField] private int fallbackShadowSortingOrder = -1;

        private const int FallbackShadowTextureWidth = 32;
        private const int FallbackShadowTextureHeight = 12;
        private static Sprite fallbackShadowSprite;

        private Vector3 visualBaseLocalPosition;
        private Vector3 shadowBaseLocalScale;
        private Color shadowBaseColor;
        private float currentVisualHeight;
        private float visualHeightVelocity;

        public Transform VisualRoot => visualRoot;
        public float CurrentVisualHeight => currentVisualHeight;
        public Vector3 VisualBaseLocalPosition => visualBaseLocalPosition;

        /// <summary>현재 CombatHeightState2D 값을 보간 없이 즉시 visual/shadow에 반영합니다.</summary>
        public void SnapToCurrentState()
        {
            ApplyImmediate();
        }

        private void Awake()
        {
            CacheReferences(createFallbackShadow);
            CaptureBaseValues();
            ApplyImmediate();
        }

        private void OnEnable()
        {
            CacheReferences(createFallbackShadow);

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
            fallbackShadowLocalScale.x = Mathf.Max(0.01f, fallbackShadowLocalScale.x);
            fallbackShadowLocalScale.y = Mathf.Max(0.01f, fallbackShadowLocalScale.y);
            CacheReferences(false);
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

        private void CacheReferences(bool allowCreateFallbackShadow)
        {
            if (heightState == null)
                heightState = GetComponent<CombatHeightState2D>();

            if (shadowRenderer == null && allowCreateFallbackShadow && createFallbackShadow)
                EnsureFallbackShadow();
        }

        /// <summary>전용 그림자 참조가 비어 있는 높이 객체에 런타임 기본 타원 그림자를 생성한다.</summary>
        private void EnsureFallbackShadow()
        {
            if (shadowRenderer != null)
                return;

            GameObject shadowObject = new GameObject("HeightShadow");
            shadowObject.transform.SetParent(transform, false);
            shadowObject.transform.localPosition = Vector3.zero;
            shadowObject.transform.localScale = fallbackShadowLocalScale;

            shadowRoot = shadowObject.transform;
            shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = GetFallbackShadowSprite();
            shadowRenderer.color = fallbackShadowColor;
            shadowRenderer.sortingLayerName = fallbackShadowSortingLayerName;
            shadowRenderer.sortingOrder = fallbackShadowSortingOrder;
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

        /// <summary>런타임 기본 그림자에 사용할 작은 픽셀 타원 스프라이트를 한 번만 생성한다.</summary>
        private static Sprite GetFallbackShadowSprite()
        {
            if (fallbackShadowSprite != null)
                return fallbackShadowSprite;

            Texture2D texture = new Texture2D(FallbackShadowTextureWidth, FallbackShadowTextureHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "Runtime_CombatHeightFallbackShadow"
            };

            Color32 transparent = new Color32(255, 255, 255, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            Color32[] pixels = new Color32[FallbackShadowTextureWidth * FallbackShadowTextureHeight];
            Vector2 center = new Vector2((FallbackShadowTextureWidth - 1) * 0.5f, (FallbackShadowTextureHeight - 1) * 0.5f);
            float radiusX = FallbackShadowTextureWidth * 0.48f;
            float radiusY = FallbackShadowTextureHeight * 0.44f;

            for (int y = 0; y < FallbackShadowTextureHeight; y++)
            {
                for (int x = 0; x < FallbackShadowTextureWidth; x++)
                {
                    float normalizedX = (x - center.x) / radiusX;
                    float normalizedY = (y - center.y) / radiusY;
                    bool insideEllipse = normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
                    pixels[y * FallbackShadowTextureWidth + x] = insideEllipse ? white : transparent;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            fallbackShadowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, FallbackShadowTextureWidth, FallbackShadowTextureHeight),
                new Vector2(0.5f, 0.5f),
                32f);
            fallbackShadowSprite.name = "Runtime_CombatHeightFallbackShadowSprite";
            return fallbackShadowSprite;
        }
    }
}
