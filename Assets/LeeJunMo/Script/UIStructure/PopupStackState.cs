using System.Collections.Generic;
using UnityEngine;

public sealed class PopupStackState
{
    private readonly List<IStackableUI> stack = new List<IStackableUI>();

    public void Clear()
    {
        stack.Clear();
    }

    public void Push(IStackableUI ui)
    {
        if (ui == null)
            return;

        PruneDeadEntries();
        stack.Remove(ui);
        stack.Add(ui);
    }

    public bool Remove(IStackableUI ui)
    {
        if (ui == null)
            return false;

        PruneDeadEntries();
        return stack.Remove(ui);
    }

    public bool TryGetTop(out IStackableUI ui)
    {
        PruneDeadEntries();

        if (stack.Count == 0)
        {
            ui = null;
            return false;
        }

        ui = stack[stack.Count - 1];
        return ui != null;
    }

    public bool HasAny()
    {
        PruneDeadEntries();
        return stack.Count > 0;
    }

    public void PruneDeadEntries()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            IStackableUI ui = stack[i];
            if (ui == null)
            {
                stack.RemoveAt(i);
                continue;
            }

            if (ui is MonoBehaviour behaviour && behaviour == null)
                stack.RemoveAt(i);
        }
    }
}
