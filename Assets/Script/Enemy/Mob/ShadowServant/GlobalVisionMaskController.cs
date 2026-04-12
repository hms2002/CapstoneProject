using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GlobalVisionMaskController : MonoBehaviour
{
    // 이 클래스의 책임:
    // 전역 어둠 오버레이 알파를 관리하고, 플레이어 시야 마스크 프리팹을 생성·연결하며, 여러 요청자가 동시에 시야 제한을 요청해도 안전하게 상태를 유지한다.

    private static GlobalVisionMaskController instance;

    [SerializeField] private GameObject darkMaskRoot;
    [SerializeField] private SpriteRenderer darkOverlayRenderer;
    [SerializeField] private GameObject playerVisionMaskPrefab;
    [SerializeField] private Transform playerVisionMaskParent;
    [SerializeField] private Vector3 playerVisionMaskOffset;
    [SerializeField, Range(0f, 1f)] private float defaultOverlayAlpha = 200f / 255f;
    [SerializeField, Range(0f, 1f)] private float fogOverlayAlpha = 1f;

    private readonly HashSet<int> activeRequesterIds = new();
    private PlayerVisionMaskFollower spawnedVisionMaskFollower;

    public static GlobalVisionMaskController Instance => instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning(
                $"[{nameof(GlobalVisionMaskController)}] 중복 인스턴스가 감지되어 새 인스턴스를 비활성 상태로 둡니다.",
                this);
            SyncMaskState(false);
            return;
        }

        instance = this;
        EnsureOverlayRenderer();
        SyncMaskState(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>시야 제한 요청을 등록합니다.</summary>
    public void AcquireDarkness(Object requester)
    {
        int requesterId = GetRequesterId(requester);
        activeRequesterIds.Add(requesterId);
        SyncMaskState(true);
    }

    /// <summary>시야 제한 요청을 해제합니다.</summary>
    public void ReleaseDarkness(Object requester)
    {
        int requesterId = GetRequesterId(requester);
        activeRequesterIds.Remove(requesterId);
        SyncMaskState(activeRequesterIds.Count > 0);
    }

    /// <summary>현재 플레이어에 맞는 시야 마스크 프리팹을 준비하고 따라가게 합니다.</summary>
    public void AttachToPlayer(Transform player)
    {
        if (player == null)
            return;

        EnsurePlayerVisionMask();
        spawnedVisionMaskFollower?.SetLocalOffset(playerVisionMaskOffset);
        spawnedVisionMaskFollower?.Bind(player);
    }

    /// <summary>요청자 식별값을 구합니다.</summary>
    private int GetRequesterId(Object requester)
    {
        return requester != null ? requester.GetInstanceID() : 0;
    }

    /// <summary>플레이어 시야 마스크 프리팹 인스턴스를 보장합니다.</summary>
    private void EnsurePlayerVisionMask()
    {
        if (spawnedVisionMaskFollower != null)
            return;

        if (playerVisionMaskPrefab == null)
            return;

        Transform parent = playerVisionMaskParent != null ? playerVisionMaskParent : transform;
        GameObject maskObject = Instantiate(playerVisionMaskPrefab, parent);

        spawnedVisionMaskFollower = maskObject.GetComponent<PlayerVisionMaskFollower>();
        if (spawnedVisionMaskFollower == null)
            spawnedVisionMaskFollower = maskObject.AddComponent<PlayerVisionMaskFollower>();
    }

    /// <summary>전역 어둠 연출 표시 상태를 맞춥니다.</summary>
    private void SyncMaskState(bool isActive)
    {
        if (darkMaskRoot != null && !darkMaskRoot.activeSelf)
            darkMaskRoot.SetActive(true);

        EnsureOverlayRenderer();
        if (darkOverlayRenderer == null)
            return;

        Color overlayColor = darkOverlayRenderer.color;
        overlayColor.a = isActive ? fogOverlayAlpha : defaultOverlayAlpha;
        darkOverlayRenderer.color = overlayColor;
    }

    /// <summary>전역 어둠 오버레이 렌더러 참조를 확보합니다.</summary>
    private void EnsureOverlayRenderer()
    {
        if (darkOverlayRenderer != null)
            return;

        if (darkMaskRoot == null)
            return;

        darkOverlayRenderer = darkMaskRoot.GetComponentInChildren<SpriteRenderer>(true);
    }
}
