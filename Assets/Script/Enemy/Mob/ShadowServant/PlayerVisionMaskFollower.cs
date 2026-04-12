using UnityEngine;

[DisallowMultipleComponent]
public class PlayerVisionMaskFollower : MonoBehaviour
{
    // 이 클래스의 책임:
    // 플레이어 시야 마스크 오브젝트를 지정된 플레이어 위치에 맞춰 따라가게 한다.

    [SerializeField] private Vector3 localOffset;

    private Transform target;

    /// <summary>따라갈 플레이어 대상을 설정합니다.</summary>
    public void Bind(Transform followTarget)
    {
        target = followTarget;
        SyncNow();
    }

    /// <summary>플레이어 추적 시 사용할 로컬 오프셋을 설정합니다.</summary>
    public void SetLocalOffset(Vector3 offset)
    {
        localOffset = offset;
        SyncNow();
    }

    private void LateUpdate()
    {
        SyncNow();
    }

    /// <summary>현재 목표 위치에 즉시 맞춥니다.</summary>
    private void SyncNow()
    {
        if (target == null)
            return;

        transform.position = target.position + localOffset;
    }
}
