using UnityEngine;

/// <summary>
/// 표식 생성 이펙트의 애니메이션 이벤트를 실제 표식 활성화 호출로 중계할 책임을 가집니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LightningSpearMarkSpawnEffectEventRelay : MonoBehaviour
{
    [SerializeField] private LightningSpearMarkActor markActor;

    private void Awake()
    {
        CacheMarkActor();
    }

    public void ActivateMark()
    {
        CacheMarkActor();
        markActor?.ActivateFromSpawnEffect();
    }

    private void CacheMarkActor()
    {
        if (markActor == null)
            markActor = GetComponentInParent<LightningSpearMarkActor>();
    }
}
