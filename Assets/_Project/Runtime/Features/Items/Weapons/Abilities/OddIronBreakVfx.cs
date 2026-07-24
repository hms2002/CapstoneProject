using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 투척 파괴 시 정해진 총 조각 SpriteRenderer들을 한 번씩 흩뿌리는 VFX를 재생한다.
    /// - 파티클 랜덤 프레임 대신 실제 조각 오브젝트를 움직여, 각 조각이 반드시 한 번씩 보이게 한다.
    /// </summary>
    public sealed class OddIronBreakVfx : MonoBehaviour
    {
        [Header("Fragments")]
        [SerializeField] private List<SpriteRenderer> fragments = new();
        [SerializeField] private bool collectChildrenOnAwake = true;

        [Header("Motion")]
        [SerializeField] private Vector2 speedRange = new(2.2f, 4.6f);
        [SerializeField] private Vector2 upwardBiasRange = new(0.2f, 1.0f);
        [SerializeField] private Vector2 angularSpeedRange = new(-720f, 720f);
        [SerializeField] private float gravity = 7f;
        [SerializeField] private float scatterAngleJitter = 35f;

        [Header("Lifetime")]
        [SerializeField, Min(0.01f)] private float lifetime = 0.65f;
        [SerializeField, Range(0f, 1f)] private float fadeStartRatio = 0.55f;
        [SerializeField] private bool destroyAfterLifetime = true;

        private readonly List<FragmentState> states = new();

        private struct FragmentState
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public Vector3 StartLocalPosition;
            public Quaternion StartLocalRotation;
            public Color StartColor;
            public Vector3 Velocity;
            public float AngularSpeed;
        }

        private void Awake()
        {
            if (collectChildrenOnAwake)
                CollectChildFragments();
        }

        private void OnEnable()
        {
            Play();
        }

        public void Play()
        {
            BuildStates();
            StopAllCoroutines();
            StartCoroutine(PlayRoutine());
        }

        private void CollectChildFragments()
        {
            fragments.Clear();
            GetComponentsInChildren(includeInactive: true, fragments);
            fragments.RemoveAll(fragment => fragment == null || fragment.transform == transform);
        }

        private void BuildStates()
        {
            states.Clear();

            int count = fragments.Count;
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer fragment = fragments[i];
                if (fragment == null)
                    continue;

                fragment.gameObject.SetActive(true);
                Transform fragmentTransform = fragment.transform;
                float baseAngle = count > 0 ? 360f * i / count : 0f;
                float angle = baseAngle + Random.Range(-scatterAngleJitter, scatterAngleJitter);
                Vector2 planar = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                Vector3 velocity = new(planar.x, planar.y + Random.Range(upwardBiasRange.x, upwardBiasRange.y), 0f);
                velocity = velocity.normalized * Random.Range(speedRange.x, speedRange.y);

                states.Add(new FragmentState
                {
                    Transform = fragmentTransform,
                    Renderer = fragment,
                    StartLocalPosition = fragmentTransform.localPosition,
                    StartLocalRotation = fragmentTransform.localRotation,
                    StartColor = fragment.color,
                    Velocity = velocity,
                    AngularSpeed = Random.Range(angularSpeedRange.x, angularSpeedRange.y)
                });
            }
        }

        private IEnumerator PlayRoutine()
        {
            float elapsed = 0f;
            float safeLifetime = Mathf.Max(0.01f, lifetime);
            float fadeStartTime = safeLifetime * fadeStartRatio;

            while (elapsed < safeLifetime)
            {
                elapsed += Time.deltaTime;
                float alpha = ResolveAlpha(elapsed, fadeStartTime, safeLifetime);

                for (int i = 0; i < states.Count; i++)
                {
                    FragmentState state = states[i];
                    if (state.Transform == null || state.Renderer == null)
                        continue;

                    float t = elapsed;
                    Vector3 gravityOffset = Vector3.down * (0.5f * gravity * t * t);
                    state.Transform.localPosition = state.StartLocalPosition + state.Velocity * t + gravityOffset;
                    state.Transform.localRotation = state.StartLocalRotation * Quaternion.Euler(0f, 0f, state.AngularSpeed * t);

                    Color color = state.StartColor;
                    color.a = state.StartColor.a * alpha;
                    state.Renderer.color = color;
                }

                yield return null;
            }

            if (destroyAfterLifetime)
                Destroy(gameObject);
        }

        private static float ResolveAlpha(float elapsed, float fadeStartTime, float lifetime)
        {
            if (elapsed <= fadeStartTime)
                return 1f;

            float fadeDuration = Mathf.Max(0.001f, lifetime - fadeStartTime);
            return 1f - Mathf.Clamp01((elapsed - fadeStartTime) / fadeDuration);
        }
    }
}
