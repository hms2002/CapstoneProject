using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal sealed class ChestRevealLayoutDriverScope
{
    private readonly List<BehaviourState> capturedStates = new();
    private readonly HashSet<Behaviour> capturedBehaviours = new();

    public bool IsActive => capturedStates.Count > 0;

    public void CaptureAndDisable(RectTransform chestPanel, RectTransform inventoryPanel)
    {
        Restore();

        RectTransform commonAncestor = ResolveCommonAncestor(chestPanel, inventoryPanel);
        ForceLayout(chestPanel);
        ForceLayout(inventoryPanel);
        Canvas.ForceUpdateCanvases();

        CaptureMovingBranch(chestPanel, commonAncestor);
        CaptureMovingBranch(inventoryPanel, commonAncestor);
    }

    public void Restore()
    {
        for (int i = capturedStates.Count - 1; i >= 0; i--)
        {
            BehaviourState state = capturedStates[i];
            if (state.Behaviour != null)
                state.Behaviour.enabled = state.WasEnabled;
        }

        capturedStates.Clear();
        capturedBehaviours.Clear();
        Canvas.ForceUpdateCanvases();
    }

    private void CaptureMovingBranch(RectTransform movingPanel, RectTransform commonAncestor)
    {
        if (movingPanel == null)
            return;

        Transform current = movingPanel.parent;
        while (current != null)
        {
            CaptureParentDrivers(current);

            if (current == commonAncestor)
                break;

            current = current.parent;
        }
    }

    private void CaptureParentDrivers(Transform transform)
    {
        Capture(transform.GetComponent<LayoutGroup>());
        Capture(transform.GetComponent<ContentSizeFitter>());
    }

    private void Capture(Behaviour behaviour)
    {
        if (behaviour == null || capturedBehaviours.Contains(behaviour))
            return;

        capturedBehaviours.Add(behaviour);
        capturedStates.Add(new BehaviourState(behaviour, behaviour.enabled));

        if (behaviour.enabled)
            behaviour.enabled = false;
    }

    private static void ForceLayout(RectTransform rect)
    {
        if (rect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
    }

    private static RectTransform ResolveCommonAncestor(RectTransform first, RectTransform second)
    {
        if (first == null || second == null)
            return null;

        Transform current = first;
        while (current != null)
        {
            if (second == current || second.IsChildOf(current))
                return current as RectTransform;

            current = current.parent;
        }

        return null;
    }

    private readonly struct BehaviourState
    {
        public readonly Behaviour Behaviour;
        public readonly bool WasEnabled;

        public BehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }
    }
}
