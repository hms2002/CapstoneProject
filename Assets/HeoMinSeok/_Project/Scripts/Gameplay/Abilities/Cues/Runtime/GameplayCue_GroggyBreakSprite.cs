using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 그로기 진입 cue가 실행될 때 대상 보스의 대표 SpriteRenderer를 복제해 공통 깨짐 연출용 스프라이트를 구성한다.
    /// - Cue prefab이 가진 머티리얼/VFX는 유지하고, 대상 쪽에서는 스프라이트/색/정렬 정보만 받아 재사용 가능하게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayCue_GroggyBreakSprite : GameplayCueNotify
    {
        private const string PreferredRendererLayerName = "TEMP_Enemy_LAYER";

        [Header("Visual Binding")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private SpriteRenderer cueSpriteRenderer;

        [Header("Copy Options")]
        [SerializeField] private bool useLargestTargetRenderer = true;
        [SerializeField] private bool copySourceColor = true;
        [SerializeField] private bool copyFlipState = true;
        [SerializeField] private bool copySortingLayer = true;
        [SerializeField] private int sortingOrderOffset = 1;
        [SerializeField] private bool snapToRendererCenter = true;
        [SerializeField] private bool matchRendererRotation = true;
        [SerializeField] private bool matchRendererScale = true;

        private void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform;

            if (cueSpriteRenderer == null)
                cueSpriteRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        }

        public override void OnExecute(GameplayCueParams p)
        {
            ApplyTargetSpriteState(p);
        }

        public override void OnAdd(GameplayCueParams p)
        {
            ApplyTargetSpriteState(p);
        }

        /// <summary>
        /// 책임 :
        /// - 대상 보스에서 복제 기준이 될 SpriteRenderer를 찾고, cue prefab의 스프라이트 렌더러에 필요한 상태를 복사한다.
        /// - 공통 cue가 보스별 구조를 몰라도 대표 스프라이트 한 장으로 깨짐 연출을 재생할 수 있게 만든다.
        /// </summary>
        private void ApplyTargetSpriteState(GameplayCueParams p)
        {
            if (cueSpriteRenderer == null || p.Target == null)
                return;

            SpriteRenderer sourceRenderer = ResolveSourceRenderer(p.Target);
            if (sourceRenderer == null || sourceRenderer.sprite == null)
                return;

            cueSpriteRenderer.sprite = sourceRenderer.sprite;

            if (copySourceColor)
                cueSpriteRenderer.color = sourceRenderer.color;

            if (copyFlipState)
            {
                cueSpriteRenderer.flipX = sourceRenderer.flipX;
                cueSpriteRenderer.flipY = sourceRenderer.flipY;
            }

            if (copySortingLayer)
            {
                cueSpriteRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                cueSpriteRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
            }

            Transform root = visualRoot != null ? visualRoot : transform;
            root.position = snapToRendererCenter ? sourceRenderer.bounds.center : sourceRenderer.transform.position;

            if (matchRendererRotation)
                root.rotation = sourceRenderer.transform.rotation;

            if (matchRendererScale)
                root.localScale = sourceRenderer.transform.lossyScale;
        }

        /// <summary>
        /// 책임 :
        /// - 대상 오브젝트에서 대표 스프라이트 렌더러를 고른다.
        /// - 여러 파츠형 보스도 가장 큰 renderer를 기준으로 먼저 복제해 1차 공통 연출에 사용할 수 있게 한다.
        /// </summary>
        private SpriteRenderer ResolveSourceRenderer(GameObject target)
        {
            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
            if (renderers == null || renderers.Length == 0)
                return null;

            int preferredLayer = LayerMask.NameToLayer(PreferredRendererLayerName);
            if (preferredLayer >= 0)
            {
                SpriteRenderer preferred = ResolveBestRendererOnLayer(renderers, preferredLayer);
                if (preferred != null)
                    return preferred;
            }

            if (!useLargestTargetRenderer)
                return renderers[0];

            return ResolveLargestRenderer(renderers);
        }

        /// <summary>
        /// 책임 :
        /// - 지정한 레이어에 속한 SpriteRenderer 중 가장 대표성이 큰 렌더러를 반환한다.
        /// - 그로기 깨짐 연출이 보스 본체 전용 렌더러만 복제하도록 우선 탐색 범위를 좁힌다.
        /// </summary>
        private SpriteRenderer ResolveBestRendererOnLayer(SpriteRenderer[] renderers, int layer)
        {
            SpriteRenderer best = null;
            float bestArea = float.MinValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer candidate = renderers[i];
                if (candidate == null || candidate.sprite == null)
                    continue;

                if (candidate.gameObject.layer != layer)
                    continue;

                Vector3 size = candidate.bounds.size;
                float area = size.x * size.y;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                best = candidate;
            }

            return best;
        }

        /// <summary>
        /// 책임 :
        /// - 레이어 필터를 통과한 후보가 없을 때 전체 렌더러 중 가장 큰 스프라이트를 fallback으로 고른다.
        /// - 1차 공통 cue가 여러 파츠형 보스에서도 최소한 대표 body를 복제할 수 있게 한다.
        /// </summary>
        private SpriteRenderer ResolveLargestRenderer(SpriteRenderer[] renderers)
        {
            SpriteRenderer best = null;
            float bestArea = float.MinValue;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer candidate = renderers[i];
                if (candidate == null || candidate.sprite == null)
                    continue;

                Vector3 size = candidate.bounds.size;
                float area = size.x * size.y;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                best = candidate;
            }

            return best != null ? best : renderers[0];
        }
    }
}
