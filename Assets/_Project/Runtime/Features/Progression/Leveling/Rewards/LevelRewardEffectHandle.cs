using System;

internal sealed class LevelRewardEffectHandle : ILevelRewardEffectHandle
{
    private Action dispose;

    public LevelRewardEffectHandle(Action dispose)
    {
        this.dispose = dispose;
    }

    public void Dispose()
    {
        Action action = dispose;
        dispose = null;
        action?.Invoke();
    }
}
