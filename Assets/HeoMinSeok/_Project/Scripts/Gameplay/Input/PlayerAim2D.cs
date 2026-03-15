using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class PlayerAim2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private SampleTopDownPlayer player;

    [Header("Tags")]
    [Tooltip("이 태그가 있으면 Hand 회전을 막습니다.")]
    [SerializeField] private GameplayTag aimLockedTag;

    [Header("Hand")]
    [SerializeField] private Transform hand;
    [SerializeField] private float weaponZOffset = 0f;

    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 MouseWorld { get; private set; }

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (player == null) player = GetComponent<SampleTopDownPlayer>();
    }

    private void Update()
    {
        if (player != null && player.CurrentState == InteractState.Talking)
            return;

        UpdateMouseAim();
        UpdateHandRotation();
    }

    private void UpdateMouseAim()
    {
        if (mainCamera == null) return;

        Vector3 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;
        MouseWorld = world;

        Vector2 dir = (world - transform.position);
        if (dir.sqrMagnitude > 0.0001f)
            AimDirection = dir.normalized;
    }

    private void UpdateHandRotation()
    {
        if (hand == null) return;

        if (tagSystem != null &&
            aimLockedTag != null &&
            tagSystem.HasTag(aimLockedTag))
            return;

        Vector2 dir = (MouseWorld - (Vector2)transform.position);
        if (dir.sqrMagnitude <= 0.0001f)
            return;

        float degreeRaw = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float degree = degreeRaw < 0f ? degreeRaw + 360f : degreeRaw;

        hand.rotation = Quaternion.Euler(0f, 0f, degree + weaponZOffset);
    }
}