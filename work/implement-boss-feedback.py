from pathlib import Path

root = Path(__file__).resolve().parents[1]
path = root / 'Assets/_Project/Runtime/Features/Bosses/DemonKing/Abilities/DemonKingAbilityLogics.cs'
text = path.read_text(encoding='utf-8-sig')
backup = root / 'Temp/boss-feedback-before-ability-logics.cs'
if not backup.exists():
    backup.write_text(text, encoding='utf-8')

def section(name, next_name):
    start = text.index('public class ' + name)
    end = text.index('public class ' + next_name, start)
    return start, end, text[start:end]

def replace_once(s, old, new):
    assert s.count(old) == 1, (old[:100], s.count(old))
    return s.replace(old, new, 1)

# Throw: use the actor's actual circle-cast radius and reflection rules for both segments.
a,b,s=section('AbilityLogic_DemonKingThrowEgoSword','AbilityLogic_DemonKingHomingMagic')
s=replace_once(s,'        demon.PushFaceTargetLock();\n        try','        demon.PushFaceTargetLock();\n        IAttackTelegraphHandle aimWarning = null;\n        IAttackTelegraphHandle reflectedWarning = null;\n        try')
start=s.index('            IAttackTelegraphHandle aimWarning = releaseDelaySeconds')
end=s.index('            float elapsed = 0f;',start)
s=s[:start]+'''            bool hasReflection = sword.ResolveThrowWarningPath(animationOrigin, lockedThrowTarget - animationOrigin,
                demon.WallMask, out Vector2 firstEnd, out Vector2 reflectedStart, out Vector2 reflectedEnd);
            aimWarning = ShowLineAreaWarning(demon, ThrowWarningLine(animationOrigin, firstEnd), releaseDelaySeconds, false);
            if (hasReflection && wallBounceCount > 1)
                reflectedWarning = ShowLineAreaWarning(demon, ThrowWarningLine(reflectedStart, reflectedEnd), releaseDelaySeconds, false);
'''+s[end:]
start=s.index('                // The throw pose')
end=s.index('                elapsed +=',start)
s=s[:start]+'''                hasReflection = sword.ResolveThrowWarningPath(animationOrigin, lockedThrowTarget - animationOrigin,
                    demon.WallMask, out firstEnd, out reflectedStart, out reflectedEnd);
                UpdateLineAreaWarning(aimWarning, demon, ThrowWarningLine(animationOrigin, firstEnd), releaseDelaySeconds, false);
                if (hasReflection && wallBounceCount > 1)
                {
                    if (reflectedWarning == null)
                        reflectedWarning = ShowLineAreaWarning(demon, ThrowWarningLine(reflectedStart, reflectedEnd), releaseDelaySeconds, false);
                    else
                        UpdateLineAreaWarning(reflectedWarning, demon, ThrowWarningLine(reflectedStart, reflectedEnd), releaseDelaySeconds, false);
                }
                else
                {
                    reflectedWarning?.HideImmediate();
                    reflectedWarning = null;
                }

'''+s[end:]
s=replace_once(s,'            aimWarning?.HideImmediate();','            aimWarning?.HideImmediate();\n            reflectedWarning?.HideImmediate();')
s=replace_once(s,'        finally\n        {\n            demon.PopFaceTargetLock();','        finally\n        {\n            aimWarning?.HideImmediate();\n            reflectedWarning?.HideImmediate();\n            demon.PopFaceTargetLock();')
s=replace_once(s,'    private void SpeakThrowReaction(','''    private LineArea ThrowWarningLine(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        return new LineArea((start + end) * 0.5f,
            new Vector2(Mathf.Max(0.01f, delta.magnitude), aimWarningWidth), DemonKingCombatUtil.RotationDeg(delta));
    }

    private void SpeakThrowReaction(''')
text=text[:a]+s+text[b:]

# Magic: existing visual-only stock prefabs form sequentially; the first aim is part of preparation.
a,b,s=section('AbilityLogic_DemonKingHomingMagic','AbilityLogic_DemonKingBombardment')
s=replace_once(s,'        List<GameObject> stockOrbVisuals = null;','        List<GameObject> stockOrbVisuals = null;\n        IAttackTelegraphHandle aimWarning = null;')
needle='            stockOrbVisuals = CreateStockOrbVisuals(demon, count, stockVisualPrefab);'
s=replace_once(s,needle,needle+'''
            const float preparationSeconds = 1.5f;
            Vector2 firstSpawn = ResolveNextProjectileSpawnPosition(demon, stockOrbVisuals, 0, count, out int firstStockIndex);
            Vector2 firstDirection = ResolveProjectileDirection(demon, firstSpawn);
            Vector2 firstTarget = ResolveAimTargetPosition(demon, firstSpawn, firstDirection);
            int sideSign = ResolveTargetSideSign(demon);
            var stockScales = new Vector3[stockOrbVisuals.Count];
            var stockRenderers = new SpriteRenderer[stockOrbVisuals.Count][];
            var stockColors = new Color[stockOrbVisuals.Count][];
            for (int j = 0; j < stockOrbVisuals.Count; j++)
            {
                stockScales[j] = stockOrbVisuals[j].transform.localScale;
                stockRenderers[j] = stockOrbVisuals[j].GetComponentsInChildren<SpriteRenderer>(true);
                stockColors[j] = new Color[stockRenderers[j].Length];
                for (int k = 0; k < stockRenderers[j].Length; k++)
                    stockColors[j][k] = stockRenderers[j][k].color;
                stockOrbVisuals[j].SetActive(false);
            }
            PlayBodyAnimation(demon, castAnimation, DemonKingController.DarkLordHandBaltState);
            aimWarning = ShowLineWarning(demon, firstSpawn,
                ResolveProjectileWarningEnd(demon, firstSpawn, firstTarget, demon.PlayerMoveSpeedReference * projectileSpeedMultiplier * lifetimeSeconds),
                aimWarningWidth, preparationSeconds);
            float preparationElapsed = 0f;
            while (preparationElapsed < preparationSeconds)
            {
                if (IsAbilityCancelled(spec) || demon.IsDead)
                    yield break;
                if (preparationElapsed < preparationSeconds - 0.35f)
                    firstTarget = ResolveAimTargetPosition(demon, firstSpawn, ResolveProjectileDirection(demon, firstSpawn));
                firstDirection = (firstTarget - firstSpawn).normalized;
                UpdateLineWarning(aimWarning, demon, firstSpawn,
                    ResolveProjectileWarningEnd(demon, firstSpawn, firstTarget, demon.PlayerMoveSpeedReference * projectileSpeedMultiplier * lifetimeSeconds),
                    aimWarningWidth, preparationSeconds);
                for (int j = 0; j < stockOrbVisuals.Count; j++)
                {
                    int order = sideSign >= 0 ? stockOrbVisuals.Count - 1 - j : j;
                    float amount = Mathf.Clamp01((preparationElapsed - order * preparationSeconds / count) / (preparationSeconds / count));
                    stockOrbVisuals[j].SetActive(amount > 0f);
                    float smooth = Mathf.SmoothStep(0f, 1f, amount);
                    stockOrbVisuals[j].transform.localScale = stockScales[j] * Mathf.Lerp(0.2f, 1f, smooth);
                    for (int k = 0; k < stockRenderers[j].Length; k++)
                    {
                        Color color = stockColors[j][k];
                        color.a *= smooth;
                        stockRenderers[j][k].color = color;
                    }
                }
                preparationElapsed += Time.deltaTime;
                yield return null;
            }
            for (int j = 0; j < stockOrbVisuals.Count; j++)
            {
                stockOrbVisuals[j].SetActive(true);
                stockOrbVisuals[j].transform.localScale = stockScales[j];
                for (int k = 0; k < stockRenderers[j].Length; k++)
                    stockRenderers[j][k].color = stockColors[j][k];
            }
            aimWarning?.HideImmediate();
            aimWarning = null;
''')
s=replace_once(s,'                Vector2 fireDirection = ResolveProjectileDirection(demon, spawnPosition);','''                if (i == 0)
                {
                    selectedStockOrbIndex = firstStockIndex;
                    spawnPosition = firstSpawn;
                }
                Vector2 fireDirection = i == 0 ? firstDirection : ResolveProjectileDirection(demon, spawnPosition);''')
s=replace_once(s,'                float aimSeconds = Mathf.Max(0f, aimWarningSeconds);','                float aimSeconds = i == 0 ? 0f : Mathf.Max(0f, aimWarningSeconds);')
s=replace_once(s,'                IAttackTelegraphHandle aimWarning = aimSeconds > 0f','                aimWarning = aimSeconds > 0f')
s=replace_once(s,'                fireDirection = ResolveProjectileDirection(demon, spawnPosition);\n                DemonKingProjectile2D.Spawn(','                fireDirection = i == 0 ? firstDirection : ResolveProjectileDirection(demon, spawnPosition);\n                DemonKingProjectile2D.Spawn(')
s=replace_once(s,'            CleanupStockOrbVisuals(stockOrbVisuals);','            aimWarning?.HideImmediate();\n            motion?.CancelMotion();\n            CleanupStockOrbVisuals(stockOrbVisuals);')
text=text[:a]+s+text[b:]

# Rush: preserve the warned geometry, prepare subsequent legs from the previous endpoint.
a,b,s=section('AbilityLogic_DemonKingWallBounceRush','AbilityLogic_DemonKingGroggyRecoverCounter')
s=s.replace('private float warningSeconds = 0.6f;', 'private float warningSeconds = 2f;')
s=replace_once(s,'        demon.PushThresholdStaggerGuard();','        demon.PushThresholdStaggerGuard();\n        IAttackTelegraphHandle warningView = null;')
s=replace_once(s,'            IAttackTelegraphHandle warningView = ShowLineAreaWarning(demon, warningLine, warningSeconds);','            float preparationSeconds = Mathf.Max(2f, warningSeconds);\n            warningView = ShowLineAreaWarning(demon, warningLine, preparationSeconds, false);')
s=s.replace('while (elapsed < warningSeconds)', 'while (elapsed < preparationSeconds)')
start=s.index('                warningTrajectory = ResolveRushTrajectory')
end=s.index('                elapsed +=', start)
s=s[:start]+s[end:]
s=replace_once(s,'            float rushSpeed =','            WallRushTrajectory nextTrajectory = warningTrajectory;\n            float rushSpeed =')
s=replace_once(s,'                WallRushTrajectory rushTrajectory = ResolveRushTrajectory(demon, start, demon.GetDirectionToTargetOrFacing(start));','''                warningView?.HideImmediate();
                warningView = null;
                WallRushTrajectory rushTrajectory = nextTrajectory;
                bool nextWarningPrepared = false;''')
s=replace_once(s,'                        if (progress >= disappearProgress)','''                        if (!nextWarningPrepared && i + 1 < wallBounceCount && progress >= 0.8f)
                        {
                            nextWarningPrepared = true;
                            nextTrajectory = ResolveRushTrajectory(demon, safeEndpoint, demon.GetDirectionToTargetOrFacing(safeEndpoint));
                            warningView = ShowLineAreaWarning(demon, ResolveRushLineArea(safeEndpoint, nextTrajectory),
                                rushSeconds + Mathf.Max(0.1f, rushEndPoseHoldSeconds), false);
                        }
                        if (progress >= disappearProgress)''')
s=replace_once(s,'                motion?.CancelMotion();\n                demon.transform.position = safeEndpoint;','''                if (!nextWarningPrepared && i + 1 < wallBounceCount)
                {
                    nextTrajectory = ResolveRushTrajectory(demon, safeEndpoint, demon.GetDirectionToTargetOrFacing(safeEndpoint));
                    warningView = ShowLineAreaWarning(demon, ResolveRushLineArea(safeEndpoint, nextTrajectory),
                        Mathf.Max(0.1f, rushEndPoseHoldSeconds), false);
                }
                motion?.CancelMotion();
                demon.transform.position = safeEndpoint;''')
s=replace_once(s,'        finally\n        {\n            demon.StopBodyAfterimage();\n            demon.PopThresholdStaggerGuard();','        finally\n        {\n            warningView?.HideImmediate();\n            demon.StopBodyAfterimage();\n            demon.PopThresholdStaggerGuard();')
text=text[:a]+s+text[b:]

# A recalled actor can become held during a forced phase transition before this continuation runs.
a,b,s=section('AbilityLogic_DemonKingRecallEgoSword','AbilityLogic_DemonKingEgoSwordVerticalStrike')
s=replace_once(s,'            if (!sword.IsHeld)\n                sword.CompleteRecallAtOwner();','''            if (IsAbilityCancelled(spec) || demon.IsDead || demon.RuntimeData.FinalDesperationStarted)
                yield break;
            if (!sword.IsHeld)
                sword.CompleteRecallAtOwner();''')
text=text[:a]+s+text[b:]
path.write_text(text,encoding='utf-8')
print('Updated throw, preparation, rush and recall guards.')

