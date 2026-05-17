using System;
using System.Collections;
using UnityEngine;

public class UpgradeFeature : MonoBehaviour, INPCFeature
{
    private Coroutine delayedOpenRoutine;

    public string FeatureName => "Upgrade";

    public void Execute(Action onComplete)
    {
        NPCFeatureController controller = GetComponent<NPCFeatureController>();
        if (controller != null)
        {
            controller.RequestDialogueExit?.Invoke();
            BeginDelayedOpen();
            return;
        }

        OpenUpgradeUI();
        onComplete?.Invoke();
    }

    private void OnDisable()
    {
        if (delayedOpenRoutine == null)
            return;

        StopCoroutine(delayedOpenRoutine);
        delayedOpenRoutine = null;
    }

    private void BeginDelayedOpen()
    {
        if (delayedOpenRoutine != null)
            StopCoroutine(delayedOpenRoutine);

        delayedOpenRoutine = StartCoroutine(OpenAfterDialogueFlowReleases());
    }

    private IEnumerator OpenAfterDialogueFlowReleases()
    {
        while (DialogueService.Instance != null && DialogueService.Instance.IsPlaying)
            yield return null;

        while (UIManager.Instance != null && UIManager.Instance.IsExternalUiInputBlocked)
            yield return null;

        delayedOpenRoutine = null;
        OpenUpgradeUI();
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
}
