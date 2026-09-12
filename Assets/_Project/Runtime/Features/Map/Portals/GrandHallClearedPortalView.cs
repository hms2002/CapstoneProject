using UnityEngine;

/// <summary>Projects this run's cleared Grand Hall route onto the authored portal visuals.</summary>
[DisallowMultipleComponent]
public sealed class GrandHallClearedPortalView : MonoBehaviour
{
    [SerializeField] private ScenePortal portal;
    [SerializeField] private SpriteRenderer portalSprite;
    [SerializeField] private Sprite disabledSprite;
    [SerializeField] private GameObject particleRoot;
    private RequiredBossClearScenePortalAccessRule requiredBossClearRule;
    private Sprite activeSprite;
    private bool particlesOriginallyActive;
    private bool? lastCleared;

    private void Awake()
    {
        requiredBossClearRule = GetComponent<RequiredBossClearScenePortalAccessRule>();
        activeSprite = portalSprite != null ? portalSprite.sprite : null;
        particlesOriginallyActive = particleRoot != null && particleRoot.activeSelf;
    }

    private void OnEnable() => lastCleared = null;

    private void Update()
    {
        // Match interaction requirements without displaying their denial popup.
        bool cleared = false;
        if (gameObject.scene.name == "Grand Hall" && portal != null)
        {
            bool requirementsBlocked = requiredBossClearRule != null &&
                requiredBossClearRule.isActiveAndEnabled && !requiredBossClearRule.AreRequirementsMet;
            bool defeated = RunSessionStore.IsRunActive &&
                RunRoutePlayback.GetTravelBlockWarning(portal) == WarningPopupCode.BossAlreadyDefeatedThisRun;
            cleared = requirementsBlocked || defeated;
        }
        ApplyCleared(cleared);
    }

    public void ApplyCleared(bool cleared)
    {
        if (lastCleared == cleared) return;
        lastCleared = cleared;
        if (portalSprite != null) portalSprite.sprite = cleared ? disabledSprite : activeSprite;
        if (particleRoot == null) return;
        if (cleared)
            foreach (var particle in particleRoot.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleRoot.SetActive(!cleared && particlesOriginallyActive);
    }
}
