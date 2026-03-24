using System;

[Serializable]
public struct RunTransitionDirective
{
    public RunTransitionAction action;
    public RunEndReason endReason;

    public bool ShouldStartRun => action == RunTransitionAction.StartRun;
    public bool ShouldEndRun => action == RunTransitionAction.EndRun;

    public RunTransitionDirective(RunTransitionAction action, RunEndReason endReason)
    {
        this.action = action;
        this.endReason = endReason;
    }

    public override string ToString()
    {
        return $"Action={action}, EndReason={endReason}";
    }
}
