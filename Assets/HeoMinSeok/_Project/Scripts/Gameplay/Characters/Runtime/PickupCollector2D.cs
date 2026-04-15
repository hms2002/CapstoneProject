using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 월드 픽업을 실제로 수집할 수 있는 플레이어 바디 콜라이더를 명시한다.
/// - 픽업 로직이 부모 탐색 없이도 플레이어 AttributeSet과 상호작용 주체를 안전하게 찾도록 돕는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PickupCollector2D : MonoBehaviour
{
    [Header("Owner")]
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private AttributeSet attributeSet;

    public PlayerInteractor2D PlayerInteractor => playerInteractor;
    public AttributeSet AttributeSet => attributeSet;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (playerInteractor == null)
            playerInteractor = GetComponentInParent<PlayerInteractor2D>();

        if (attributeSet == null)
            attributeSet = GetComponentInParent<AttributeSet>();
    }
}
