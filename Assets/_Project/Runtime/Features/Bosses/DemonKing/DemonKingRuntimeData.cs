public enum DemonKingEgoSwordMode
{
    Hold,
    Drop
}

public enum DemonKingPatternRole
{
    None,
    HoldNormal,
    DropNormal,
    ThrowSword,
    RecallSword,
    Hp50Rush,
    GroggyRecoverCounter,
    FinalDesperation
}

public sealed class DemonKingRuntimeData
{
    public DemonKingEgoSwordMode SwordMode { get; private set; } = DemonKingEgoSwordMode.Hold;
    public int HoldPatternUseCount { get; private set; }
    public int EgoSwordPatternUseCount { get; private set; }
    public bool RecallRequested { get; private set; }
    public bool GroggyRecoverCounterRequested { get; private set; }
    public bool Hp50PatternUsed { get; private set; }
    public bool FinalDesperationStarted { get; private set; }

    public bool ShouldThrowSword(int threshold)
    {
        return SwordMode == DemonKingEgoSwordMode.Hold && HoldPatternUseCount >= threshold;
    }

    public bool ShouldRecallSword()
    {
        return SwordMode == DemonKingEgoSwordMode.Drop && RecallRequested;
    }

    public void RecordHoldNormalPattern()
    {
        if (SwordMode == DemonKingEgoSwordMode.Hold)
            HoldPatternUseCount++;
    }

    public void SetSwordHeld()
    {
        SwordMode = DemonKingEgoSwordMode.Hold;
        HoldPatternUseCount = 0;
        EgoSwordPatternUseCount = 0;
        RecallRequested = false;
    }

    public void SetSwordDropped()
    {
        SwordMode = DemonKingEgoSwordMode.Drop;
        HoldPatternUseCount = 0;
        EgoSwordPatternUseCount = 0;
        RecallRequested = false;
    }

    public void RecordEgoSwordPatternUse(int recallThreshold)
    {
        if (SwordMode != DemonKingEgoSwordMode.Drop)
            return;

        EgoSwordPatternUseCount++;
        if (EgoSwordPatternUseCount >= recallThreshold)
            RecallRequested = true;
    }

    public void RequestRecallSword()
    {
        if (SwordMode == DemonKingEgoSwordMode.Drop)
            RecallRequested = true;
    }

    public void RequestGroggyRecoverCounter()
    {
        if (!FinalDesperationStarted)
            GroggyRecoverCounterRequested = true;
    }

    public void ConsumeGroggyRecoverCounter()
    {
        GroggyRecoverCounterRequested = false;
    }

    public void MarkHp50PatternUsed()
    {
        Hp50PatternUsed = true;
    }

    public void MarkFinalDesperationStarted()
    {
        FinalDesperationStarted = true;
        Hp50PatternUsed = true;
        RecallRequested = false;
        GroggyRecoverCounterRequested = false;
    }

#if UNITY_EDITOR
    public void ResetForWorkbenchRuntimeRefresh()
    {
        SwordMode = DemonKingEgoSwordMode.Hold;
        HoldPatternUseCount = 0;
        EgoSwordPatternUseCount = 0;
        RecallRequested = false;
        GroggyRecoverCounterRequested = false;
        Hp50PatternUsed = false;
        FinalDesperationStarted = false;
    }
#endif
}
