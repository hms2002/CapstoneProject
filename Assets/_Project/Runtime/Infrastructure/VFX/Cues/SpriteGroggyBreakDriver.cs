using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - SpriteGroggyBreak 셰이더의 진행도 값을 시간에 따라 갱신해 공통 깨짐 원샷 연출을 재생한다.
    /// - MaterialPropertyBlock을 사용해 개별 cue 인스턴스마다 셰이더 값을 안전하게 제어한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpriteGroggyBreakDriver : MonoBehaviour
    {
        private static readonly int BreakProgressId = Shader.PropertyToID("_BreakProgress");
        private static readonly int AlphaFadeId = Shader.PropertyToID("_AlphaFade");

        [Header("Binding")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Transform scaleTarget;

        [Header("Timing")]
        [SerializeField] private float duration = 0.55f;
        [SerializeField] private AnimationCurve breakCurve = null;
        [SerializeField] private AnimationCurve alphaCurve = null;

        [Header("Optional Scale")]
        [SerializeField] private bool scaleWhileBreaking = true;
        [SerializeField] private Vector3 endScaleMultiplier = new Vector3(1.08f, 1.08f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private Vector3 initialScale = Vector3.one;
        private float elapsed;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<SpriteRenderer>();

            if (scaleTarget == null)
                scaleTarget = transform;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            if (breakCurve == null || breakCurve.length == 0)
            {
                breakCurve = new AnimationCurve(
                    new Keyframe(0f, 0f, 2.2f, 2.2f),
                    new Keyframe(1f, 1f, 0f, 0f));
            }

            if (alphaCurve == null || alphaCurve.length == 0)
            {
                alphaCurve = new AnimationCurve(
                    new Keyframe(0f, 1f, 0f, 0f),
                    new Keyframe(0.8f, 0.92f, -0.5f, -0.5f),
                    new Keyframe(1f, 0f, -4f, -4f));
            }
        }

        private void OnEnable()
        {
            elapsed = 0f;
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            if (scaleTarget != null)
                initialScale = scaleTarget.localScale;

            ApplyShaderState(0f, 1f);
        }

        private void Update()
        {
            if (targetRenderer == null)
                return;

            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(duration > 0.0001f ? elapsed / duration : 1f);

            float breakProgress = Mathf.Clamp01(breakCurve.Evaluate(normalized));
            float alphaFade = Mathf.Clamp01(alphaCurve.Evaluate(normalized));
            ApplyShaderState(breakProgress, alphaFade);

            if (scaleWhileBreaking && scaleTarget != null)
                scaleTarget.localScale = Vector3.LerpUnclamped(initialScale, Vector3.Scale(initialScale, endScaleMultiplier), normalized);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 인스턴스의 SpriteRenderer에만 깨짐 진행도와 알파 감쇠 값을 반영한다.
        /// - 공유 머티리얼을 오염시키지 않도록 MaterialPropertyBlock 경로만 사용한다.
        /// </summary>
        private void ApplyShaderState(float breakProgress, float alphaFade)
        {
            if (targetRenderer == null)
                return;

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(BreakProgressId, breakProgress);
            propertyBlock.SetFloat(AlphaFadeId, alphaFade);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
