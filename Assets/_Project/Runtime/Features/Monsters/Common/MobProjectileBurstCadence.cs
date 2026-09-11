using UnityEngine;

/// <summary>
/// Owns one monster's successful-shot budget and attack-speed-independent rest deadline.
/// Callers count a simultaneous scatter volley once and each sequential shot separately.
/// </summary>
public sealed class MobProjectileBurstCadence
{
    private const int MinimumShots = 3;
    private const int MaximumShots = 5;
    private const float RestSeconds = 2f;

    private int shotsRemaining;
    private float restUntil;

    public bool IsResting(float now) => now < restUntil;

    public float GetRemainingRestSeconds(float now) => Mathf.Max(0f, restUntil - now);

    public void RecordShot(float now)
    {
        if (IsResting(now))
            return;

        if (shotsRemaining == 0)
            shotsRemaining = Random.Range(MinimumShots, MaximumShots + 1);

        shotsRemaining--;
        if (shotsRemaining == 0)
            restUntil = now + RestSeconds;
    }
}
