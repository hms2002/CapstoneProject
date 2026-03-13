using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// GameplayEffect 관련 GameplayCue 연출 전담 라우터.
    /// Runner는 "언제" 연출할지만 결정하고,
    /// 실제 Cue 실행 / Add / Remove / CueParams 생성은 이 라우터가 맡는다.
    /// </summary>
    public sealed class GameplayEffectPresentationRouter
    {
        private readonly GameplayCueManager cueManager;

        public GameplayEffectPresentationRouter(GameplayCueManager cueManager)
        {
            this.cueManager = cueManager;
        }

        public void PlayExecute(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (cueManager == null || effect == null || effect.cueOnExecute == null)
                return;

            cueManager.ExecuteCue(
                effect.cueOnExecute,
                BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx));
        }

        public void AddWhileActive(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (cueManager == null || effect == null || effect.cueWhileActive == null)
                return;

            cueManager.AddCue(
                effect.cueWhileActive,
                BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx));
        }

        public void RemoveWhileActive(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (cueManager == null || effect == null || effect.cueWhileActive == null)
                return;

            cueManager.RemoveCue(
                effect.cueWhileActive,
                BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx));
        }

        public void PlayRemove(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (cueManager == null || effect == null || effect.cueOnRemove == null)
                return;

            cueManager.ExecuteCue(
                effect.cueOnRemove,
                BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx));
        }

        private GameplayCueParams BuildCueParams(
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            var p = new GameplayCueParams
            {
                Instigator = instigator,
                Causer = causer,
                Target = target,
                SourceObject = sourceObject,
                Magnitude = magnitude
            };

            if (ctx != null)
            {
                if (ctx.Hit3D.HasValue)
                {
                    var h = ctx.Hit3D.Value;
                    p.Position = h.point;
                    p.Normal = h.normal;
                    return p;
                }

                if (ctx.Hit2D.HasValue)
                {
                    var h2 = ctx.Hit2D.Value;
                    p.Position = h2.point;
                    p.Normal = h2.normal;
                    return p;
                }
            }

            p.Position = target != null ? target.transform.position : Vector3.zero;
            p.Normal = Vector3.up;
            return p;
        }
    }
}