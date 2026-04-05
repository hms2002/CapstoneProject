using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 마우스 월드 위치를 읽어 플레이어의 현재 조준 방향을 계산한다.
/// - 조준에 따라 손 pivot 회전을 갱신해 무기 비주얼이 커서를 향하도록 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAim2D : MonoBehaviour, IAimDirectionSource2D
{
    private const string AimBlockedTagResourcePath = "Tags/State.Aim.Blocked";

    [SerializeField] private Camera mainCamera;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag aimLockedTag;

    [Header("Hand")]
    [SerializeField] private Transform hand;
    [SerializeField] private Transform secondaryHand;
    [SerializeField] private float weaponZOffset = 0f;

    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 MouseWorld { get; private set; }

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (aimLockedTag == null) aimLockedTag = Resources.Load<GameplayTag>(AimBlockedTagResourcePath);
        ResolveHandReferences();
    }

    private void Update()
    {
        UpdateMouseAim();
        UpdateHandRotation();
    }

    public Vector2 GetAimDirection()
    {
        return AimDirection;
    }

    private void UpdateMouseAim()
    {
        if (mainCamera == null) return;
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
            return;

        var world = InputBindingService.EnsureInstance().GetPointerWorldPosition(mainCamera, 0f);
        MouseWorld = world;

        Vector2 dir = (world - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    private void UpdateHandRotation()
    {
        if (hand == null) return;
        if (tagSystem != null && aimLockedTag != null && tagSystem.HasTag(aimLockedTag))
            return;

        Vector2 dir = (MouseWorld - (Vector2)transform.position).normalized;
        float rad = Mathf.Atan2(dir.y, dir.x);
        float degreeRaw = rad * Mathf.Rad2Deg;
        float degree = degreeRaw < 0f ? degreeRaw + 360f : degreeRaw;

        ApplyHandRotation(hand, degree);
        ApplyHandRotation(secondaryHand, degree);
    }

    /// <summary>
    /// 책임 :
    /// - 좌/우 손 pivot 참조를 최대한 자동으로 채워 인스펙터 누락에도 회전 동기화를 유지한다.
    /// - 기존 단일 hand 설정과 새 좌/우 pivot 구조를 모두 호환한다.
    /// </summary>
    private void ResolveHandReferences()
    {
        if (hand == null)
            hand = transform.Find("LHand");

        if (secondaryHand == null)
        {
            var left = transform.Find("LHand");
            var right = transform.Find("RHand");

            if (hand == left)
                secondaryHand = right;
            else if (hand == right)
                secondaryHand = left;
            else
                secondaryHand = right;
        }
    }

    /// <summary>
    /// 책임 : 지정된 손 pivot 하나의 회전을 현재 조준 각도로 맞춘다.
    /// </summary>
    private void ApplyHandRotation(Transform targetHand, float degree)
    {
        if (targetHand == null)
            return;

        targetHand.rotation = Quaternion.Euler(0f, 0f, degree + weaponZOffset);
    }
}
