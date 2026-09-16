using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "LevelRewardEffect_SpreadingEmbers", menuName = "Game/Progression/Level Reward Effects/Spreading Embers")]
public sealed class SpreadingEmbersLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField] private GameplayEffect burnDamageEffect;
    [SerializeField, Min(1)] private int burnStacks = 3;
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.5f;

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.Persistent;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason))
            return false;

        if (burnDamageEffect == null || context.Player.GetComponent<AbilitySystem>() == null)
        {
            failureReason = "화상 피해/플레이어 능력 구성이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        GameObject player = context.Player != null ? context.Player.gameObject : null;
        AbilitySystem abilities = player != null ? player.GetComponent<AbilitySystem>() : null;
        if (player == null || abilities == null || burnDamageEffect == null)
            return null;

        float nextAvailableTime = 0f;

        void HandleBurnKill(BurnKillContext burnKill)
        {
            if (burnKill.SourceSystem != abilities || Time.time < nextAvailableTime)
                return;

            Camera camera = GameplayCameraViewQuery.GetMainCamera();
            if (camera == null)
                camera = Camera.main;
            if (camera == null)
                return;

            Enemy nearest = null;
            float nearestDistanceSqr = float.PositiveInfinity;
            Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
            for (int i = 0; i < enemies.Length; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || enemy == burnKill.Target || enemy.IsDead || !enemy.gameObject.activeInHierarchy)
                    continue;

                Vector3 viewport = camera.WorldToViewportPoint(enemy.transform.position);
                if (viewport.z < 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                    continue;

                float distanceSqr = (enemy.transform.position - burnKill.WorldPosition).sqrMagnitude;
                if (distanceSqr >= nearestDistanceSqr)
                    continue;

                nearest = enemy;
                nearestDistanceSqr = distanceSqr;
            }

            if (nearest == null)
                return;

            BurnStatus2D applied = BurnStatus2D.Apply(
                nearest.gameObject,
                abilities,
                burnDamageEffect,
                player,
                Mathf.Max(1, burnStacks));
            if (applied != null)
                nextAvailableTime = Time.time + Mathf.Max(0f, cooldownSeconds);
        }

        BurnStatus2D.BurnKillConfirmed += HandleBurnKill;
        return new LevelRewardEffectHandle(() => BurnStatus2D.BurnKillConfirmed -= HandleBurnKill);
    }
}
