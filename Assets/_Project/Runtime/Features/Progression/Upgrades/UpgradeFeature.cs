using System;
using System.Collections;
using UnityEngine;
using UnityGAS;

// 책임: NPC 상호작용에서 업그레이드 UI 열기와 플레이어 조작 차단 인계를 수행한다.
public class UpgradeFeature : MonoBehaviour, INPCFeature
{
    private const string ControlBlockTagSetResourcePath = "Tags/TagSet/TS_BlockControlByUI";

    private Coroutine delayedOpenRoutine;
    private GameplayTagSet controlBlockTagSet;
    private PlayerUIControlLockBridge controlLockBridge;
    private bool isControlBlockedForUpgradeHandoff;

    public string FeatureName => "Upgrade";

    public void Execute(Action onComplete)
    {
        AcquireUpgradeHandoffControlBlock();

        NPCFeatureController controller = GetComponent<NPCFeatureController>();
        if (controller != null)
        {
            controller.RequestDialogueExit?.Invoke();
            BeginDelayedOpen();
            return;
        }

        try
        {
            OpenUpgradeUI();
            onComplete?.Invoke();
        }
        finally
        {
            ReleaseUpgradeHandoffControlBlock();
        }
    }

    private void OnDisable()
    {
        if (delayedOpenRoutine != null)
        {
            StopCoroutine(delayedOpenRoutine);
            delayedOpenRoutine = null;
        }

        ReleaseUpgradeHandoffControlBlock();
    }

    private void BeginDelayedOpen()
    {
        if (delayedOpenRoutine != null)
            StopCoroutine(delayedOpenRoutine);

        AcquireUpgradeHandoffControlBlock();
        delayedOpenRoutine = StartCoroutine(OpenAfterDialogueFlowReleases());
    }

    private IEnumerator OpenAfterDialogueFlowReleases()
    {
        try
        {
            while (DialoguePlayback.IsPlaying)
                yield return null;

            while (UiInteractionStateQuery.IsExternalUiInputBlocked())
                yield return null;

            OpenUpgradeUI();
        }
        finally
        {
            delayedOpenRoutine = null;
            ReleaseUpgradeHandoffControlBlock();
        }
    }

    private static void OpenUpgradeUI()
    {
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.ToggleUI();
            return;
        }

        Debug.LogWarning("[UpgradeFeature] UpgradeManager.Instance is missing in the scene.");
    }

    private void AcquireUpgradeHandoffControlBlock()
    {
        if (isControlBlockedForUpgradeHandoff)
            return;

        if (controlBlockTagSet == null)
            controlBlockTagSet = Resources.Load<GameplayTagSet>(ControlBlockTagSetResourcePath);

        if (controlBlockTagSet == null)
            return;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null && PlayerInteractor2D.Instance != null)
            playerTransform = PlayerInteractor2D.Instance.transform;

        controlLockBridge = PlayerUIControlLockBridge.GetOrAdd(playerTransform);
        if (controlLockBridge != null && controlLockBridge.Acquire(this, controlBlockTagSet))
            isControlBlockedForUpgradeHandoff = true;
    }

    private void ReleaseUpgradeHandoffControlBlock()
    {
        if (!isControlBlockedForUpgradeHandoff)
            return;

        if (controlLockBridge != null && controlBlockTagSet != null)
            controlLockBridge.Release(this, controlBlockTagSet);

        controlLockBridge = null;
        isControlBlockedForUpgradeHandoff = false;
    }
}
