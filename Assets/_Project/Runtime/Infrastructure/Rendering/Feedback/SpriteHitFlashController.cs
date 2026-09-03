using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 : SpriteHitFlash 셰이더의 _FlashAmount 값을 제어하여
/// 스프라이트 피격 플래시를 재생한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpriteHitFlashController : MonoBehaviour, IHitFlashController2D
{
    [Header("Targets")]
    [SerializeField] private SpriteRenderer[] targetRenderers;

    [Header("Flash")]
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashMultiply = 1.5f;
    [SerializeField] private float flashDuration = 0.08f;

    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
    private static readonly int FlashMultiplyId = Shader.PropertyToID("_FlashMultiply");

    private MaterialPropertyBlock _mpb;
    private Coroutine _flashRoutine;

    private void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        _mpb = new MaterialPropertyBlock();
        ApplyFlash(0f);
    }

    private void OnDisable()
    {
        StopFlash();
    }

    /// <summary>
    /// 책임 : 설정된 지속 시간 동안 피격 플래시를 재생한다.
    /// 이미 재생 중이면 처음부터 다시 시작한다.
    /// </summary>
    public void PlayFlash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(CoPlayFlash());
    }

    /// <summary>
    /// 책임 : 즉시 플래시를 종료하고 셰이더 값을 원상 복구한다.
    /// </summary>
    public void StopFlash()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        ApplyFlash(0f);
    }

    /// <summary>
    /// 책임 : 짧은 시간 동안 FlashAmount를 1에서 0으로 감소시켜
    /// 자연스러운 피격 플래시를 만든다.
    /// </summary>
    private IEnumerator CoPlayFlash()
    {
        float elapsed = 0f;
        ApplyFlash(1f);

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flashDuration);
            ApplyFlash(1f - t);
            yield return null;
        }

        ApplyFlash(0f);
        _flashRoutine = null;
    }

    /// <summary>
    /// 책임 : 모든 대상 SpriteRenderer에 셰이더 프로퍼티를 반영한다.
    /// </summary>
    private void ApplyFlash(float amount)
    {
        if (targetRenderers == null)
            return;

        _mpb ??= new MaterialPropertyBlock();

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var sr = targetRenderers[i];
            if (sr == null)
                continue;

            sr.GetPropertyBlock(_mpb);
            _mpb.SetColor(FlashColorId, flashColor);
            _mpb.SetFloat(FlashAmountId, Mathf.Clamp01(amount));
            _mpb.SetFloat(FlashMultiplyId, flashMultiply);
            sr.SetPropertyBlock(_mpb);
        }
    }
}
