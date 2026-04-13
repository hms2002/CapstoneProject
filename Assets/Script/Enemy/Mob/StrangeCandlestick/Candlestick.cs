using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(CandlestickSeal))]
public class Candlestick : MonoBehaviour, IDamageReceiver
{
    // 이 클래스의 책임:
    // 촛대의 봉인 상태와 피격 가능 레이어를 관리한다.

    private static readonly string[] TurnOffTriggerNames =
    {
        "turnOff",
        "die",
        "isDead"
    };

    private static readonly string[] TurnOffStateNames =
    {
        "StrangeCandlestick_TurnOff",
        "TurnOff",
        "Die"
    };

    private static readonly string[] IdleStateNames =
    {
        "StrangeCandlestick_Idle",
        "Idle"
    };

    private static readonly List<Candlestick> instances = new();

    private CandlestickSeal candlestickSeal;
    private SpriteHitFlashController hitFlash;
    private Animator animator;
    private int defaultLayer;
    private int sealedLayer;

    public static IReadOnlyList<Candlestick> Instances => instances;
    public bool IsSealed => candlestickSeal != null && candlestickSeal.IsSealed;

    private void Awake()
    {
        candlestickSeal = GetComponent<CandlestickSeal>();
        hitFlash = GetComponent<SpriteHitFlashController>();
        animator = GetComponent<Animator>();
        defaultLayer = gameObject.layer;
        sealedLayer = GetSealedLayer();

        if (candlestickSeal != null)
            candlestickSeal.SealChanged += OnSealChanged;

        SyncHitLayer(IsSealed);
        SyncAnimation(IsSealed);
    }

    private void OnDestroy()
    {
        if (candlestickSeal != null)
            candlestickSeal.SealChanged -= OnSealChanged;
    }

    private void OnEnable()
    {
        if (!instances.Contains(this))
            instances.Add(this);
    }

    private void OnDisable()
    {
        instances.Remove(this);
    }

    /// <summary>촛대를 봉인 상태로 바꿉니다.</summary>
    public bool Seal()
    {
        if (candlestickSeal == null) return false;

        candlestickSeal.Seal();
        return true;
    }

    /// <summary>봉인 상태일 때만 타격을 처리합니다.</summary>
    public bool TryApplyDamage(DamageRequest request)
    {
        if (!IsSealed || candlestickSeal == null) return false;

        if (!candlestickSeal.UseHit()) return false;

        if (hitFlash != null)
            hitFlash.PlayFlash();

        return true;
    }

    /// <summary>봉인 상태에 맞게 피격 레이어를 맞춥니다.</summary>
    private void SyncHitLayer(bool isSealed)
    {
        gameObject.layer = isSealed ? sealedLayer : defaultLayer;
    }

    /// <summary>봉인 상태가 바뀌면 레이어와 애니메이션을 같이 갱신합니다.</summary>
    private void OnSealChanged(bool isSealed)
    {
        SyncHitLayer(isSealed);
        SyncAnimation(isSealed);
    }

    /// <summary>봉인 상태에서 사용할 레이어를 찾습니다.</summary>
    private int GetSealedLayer()
    {
        int layer = LayerMask.NameToLayer("TEMP_Enemy_LAYER");
        if (layer < 0) return defaultLayer;

        return layer;
    }

    /// <summary>봉인 상태에 맞는 촛대 애니메이션을 재생합니다.</summary>
    private void SyncAnimation(bool isSealed)
    {
        if (animator == null)
            return;

        if (isSealed)
        {
            if (TrySetAnimatorTrigger(TurnOffTriggerNames))
                return;

            TryPlayAnimatorState(TurnOffStateNames);
            return;
        }

        TryPlayAnimatorState(IdleStateNames);
    }

    /// <summary>후보 이름 중 존재하는 Trigger 파라미터를 발동합니다.</summary>
    private bool TrySetAnimatorTrigger(params string[] candidateNames)
    {
        if (animator == null || candidateNames == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < candidateNames.Length; i++)
        {
            string candidateName = candidateNames[i];
            if (string.IsNullOrWhiteSpace(candidateName))
                continue;

            int candidateHash = Animator.StringToHash(candidateName);
            for (int j = 0; j < parameters.Length; j++)
            {
                AnimatorControllerParameter parameter = parameters[j];
                if (parameter.type != AnimatorControllerParameterType.Trigger ||
                    parameter.nameHash != candidateHash)
                {
                    continue;
                }

                animator.SetTrigger(candidateHash);
                return true;
            }
        }

        return false;
    }

    /// <summary>후보 이름 중 존재하는 상태를 즉시 재생합니다.</summary>
    private bool TryPlayAnimatorState(params string[] stateNames)
    {
        if (animator == null || stateNames == null)
            return false;

        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            int stateHash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, stateHash))
                continue;

            animator.Play(stateHash, 0, 0f);
            return true;
        }

        return false;
    }
}
