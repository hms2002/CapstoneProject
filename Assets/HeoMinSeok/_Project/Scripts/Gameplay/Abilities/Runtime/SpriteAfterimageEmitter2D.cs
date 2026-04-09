using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 현재 SpriteRenderer 묶음을 복제해 짧게 남는 2D 잔상 스냅샷을 일정 간격으로 생성한다.
    /// - Rush 같은 능력 전용 지속 연출이 로직과 느슨하게 연결되도록, 시작/중지/강제 정리 API만 제공한다.
    /// </summary>
    public sealed class SpriteAfterimageEmitter2D : MonoBehaviour
    {
        private Transform sourceRoot;
        private float emissionInterval = 0.05f;
        private float lifetime = 0.18f;
        private Color tintColor = new(1f, 1f, 1f, 0.45f);
        private bool isEmitting;
        private float nextEmitTime;
        private readonly List<GameObject> spawnedGhostRoots = new();

        public bool IsEmitting => isEmitting;

        /// <summary>
        /// 책임 :
        /// - 현재 sourceRoot를 기준으로 잔상 생성 설정을 시작한다.
        /// - 이미 동작 중이어도 새 간격/색/수명을 즉시 덮어써 stage 전환 연출을 부드럽게 갱신한다.
        /// </summary>
        public void Begin(Transform sourceRoot, float emissionInterval, float lifetime, Color tintColor)
        {
            this.sourceRoot = sourceRoot != null ? sourceRoot : transform;
            this.emissionInterval = Mathf.Max(0.01f, emissionInterval);
            this.lifetime = Mathf.Max(0.01f, lifetime);
            this.tintColor = tintColor;
            isEmitting = true;
            nextEmitTime = Time.time;
        }

        /// <summary>
        /// 책임 :
        /// - 진행 중인 잔상 방출 간격만 갱신한다.
        /// - Rush 단계 상승처럼 emitter는 유지하되 밀도만 바꾸고 싶은 경우를 지원한다.
        /// </summary>
        public void SetEmissionInterval(float emissionInterval)
        {
            this.emissionInterval = Mathf.Max(0.01f, emissionInterval);
        }

        /// <summary>
        /// 책임 :
        /// - 새 잔상 생성만 멈추고 이미 생성된 잔상은 자연스럽게 사라지게 둔다.
        /// - 일반 종료/취소 시 마지막 잔상을 즉시 없애지 않고 잔향을 남긴다.
        /// </summary>
        public void StopEmission()
        {
            isEmitting = false;
        }

        /// <summary>
        /// 책임 :
        /// - emitter가 만든 잔상 스냅샷을 즉시 제거한다.
        /// - 씬 이동/강제 리셋처럼 화면에 잔상을 남기면 안 되는 정리 경로에서 사용한다.
        /// </summary>
        public void ClearSpawnedGhosts()
        {
            for (int i = spawnedGhostRoots.Count - 1; i >= 0; i--)
            {
                GameObject ghostRoot = spawnedGhostRoots[i];
                if (ghostRoot != null)
                    Destroy(ghostRoot);
            }

            spawnedGhostRoots.Clear();
        }

        private void Update()
        {
            if (!isEmitting || sourceRoot == null)
                return;

            if (Time.time < nextEmitTime)
                return;

            nextEmitTime = Time.time + emissionInterval;
            SpawnSnapshot();
        }

        private void OnDestroy()
        {
            isEmitting = false;
            sourceRoot = null;
            spawnedGhostRoots.Clear();
        }

        private void SpawnSnapshot()
        {
            SpriteRenderer[] renderers = sourceRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            if (renderers == null || renderers.Length == 0)
                return;

            CleanupDestroyedGhostRefs();

            GameObject ghostRoot = new($"{sourceRoot.name}_Afterimage");
            ghostRoot.transform.position = sourceRoot.position;
            ghostRoot.transform.rotation = sourceRoot.rotation;
            ghostRoot.transform.localScale = sourceRoot.lossyScale;

            var fade = ghostRoot.AddComponent<AfterimageSnapshotFade2D>();
            var ghostRenderers = new List<SpriteRenderer>(renderers.Length);

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer source = renderers[i];
                if (source == null || !source.enabled || source.sprite == null)
                    continue;

                GameObject child = new($"{source.gameObject.name}_Ghost");
                child.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                child.transform.localScale = source.transform.lossyScale;
                child.transform.SetParent(ghostRoot.transform, worldPositionStays: true);

                SpriteRenderer ghost = child.AddComponent<SpriteRenderer>();
                ghost.sprite = source.sprite;
                ghost.color = BuildGhostColor(source.color);
                ghost.flipX = source.flipX;
                ghost.flipY = source.flipY;
                ghost.sortingLayerID = source.sortingLayerID;
                ghost.sortingOrder = source.sortingOrder;
                ghost.drawMode = source.drawMode;
                ghost.size = source.size;
                ghost.tileMode = source.tileMode;
                ghost.adaptiveModeThreshold = source.adaptiveModeThreshold;
                ghost.spriteSortPoint = source.spriteSortPoint;
                ghost.maskInteraction = source.maskInteraction;

                ghostRenderers.Add(ghost);
            }

            if (ghostRenderers.Count == 0)
            {
                Destroy(ghostRoot);
                return;
            }

            fade.Initialize(ghostRenderers, lifetime);
            spawnedGhostRoots.Add(ghostRoot);
        }

        private Color BuildGhostColor(Color sourceColor)
        {
            float alpha = tintColor.a * sourceColor.a;
            return new Color(tintColor.r, tintColor.g, tintColor.b, alpha);
        }

        private void CleanupDestroyedGhostRefs()
        {
            for (int i = spawnedGhostRoots.Count - 1; i >= 0; i--)
            {
                if (spawnedGhostRoots[i] == null)
                    spawnedGhostRoots.RemoveAt(i);
            }
        }

        /// <summary>
        /// 책임 :
        /// - 한 번 생성된 잔상 스냅샷을 지정된 시간 동안 서서히 투명하게 만들고 스스로 파괴한다.
        /// - emitter가 종료돼도 이미 남은 잔상은 자연스럽게 사라지게 하는 개별 수명 컴포넌트다.
        /// </summary>
        private sealed class AfterimageSnapshotFade2D : MonoBehaviour
        {
            private SpriteRenderer[] renderers;
            private Color[] initialColors;
            private float lifetime;
            private float elapsed;

            public void Initialize(List<SpriteRenderer> renderers, float lifetime)
            {
                this.renderers = renderers.ToArray();
                initialColors = new Color[this.renderers.Length];
                for (int i = 0; i < this.renderers.Length; i++)
                    initialColors[i] = this.renderers[i] != null ? this.renderers[i].color : Color.clear;

                this.lifetime = Mathf.Max(0.01f, lifetime);
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                float alphaMul = 1f - t;

                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        SpriteRenderer renderer = renderers[i];
                        if (renderer == null)
                            continue;

                        Color color = initialColors[i];
                        color.a *= alphaMul;
                        renderer.color = color;
                    }
                }

                if (t >= 1f)
                    Destroy(gameObject);
            }
        }
    }
}
