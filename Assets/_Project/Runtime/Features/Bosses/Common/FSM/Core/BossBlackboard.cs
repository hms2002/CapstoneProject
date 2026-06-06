using UnityEngine;

public sealed class BossBlackboard
{
    // 이 클래스의 책임:
    // 공통 FSM이 함께 참조하는 전투 문맥(타깃, 거리, 방향, HP, 페이즈, 상태 시간)만 보관한다.

    private readonly Transform ownerTransform;

    public BossBlackboard(Transform ownerTransform)
    {
        this.ownerTransform = ownerTransform;
    }

    public Transform CurrentTarget { get; private set; }
    public float DistanceToTarget { get; private set; }
    public Vector2 DirectionToTarget { get; private set; }
    public float CurrentHpRatio { get; private set; } = 1f;

    public string CurrentStateName { get; private set; }
    public float StateElapsedTime { get; private set; }

    public int CurrentPhaseIndex { get; private set; }

    public void Tick(float deltaTime, Transform target, float currentHpRatio)
    {
        StateElapsedTime += deltaTime;
        CurrentTarget = target;
        CurrentHpRatio = Mathf.Clamp01(currentHpRatio);

        if (ownerTransform != null && CurrentTarget != null)
        {
            Vector3 delta = CurrentTarget.position - ownerTransform.position;
            DistanceToTarget = delta.magnitude;
            DirectionToTarget = delta.sqrMagnitude > 0.0001f
                ? ((Vector2)delta).normalized
                : Vector2.zero;
        }
        else
        {
            DistanceToTarget = float.MaxValue;
            DirectionToTarget = Vector2.zero;
        }
    }

    public void NotifyStateChanged(string stateName)
    {
        CurrentStateName = stateName;
        StateElapsedTime = 0f;
    }

    public void SetPhaseIndex(int phaseIndex)
    {
        CurrentPhaseIndex = Mathf.Max(0, phaseIndex);
    }
}
