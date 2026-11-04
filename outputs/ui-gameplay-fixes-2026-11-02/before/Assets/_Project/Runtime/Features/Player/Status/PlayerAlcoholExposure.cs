using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Owns one player's alcohol charge across overlapping puddles. Effects use the
    /// existing GE/HUD path; presentation only reads charge, overflow and drain state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerAlcoholExposure : MonoBehaviour
    {
        private const int SpiriNpcId = 2001;
        private const string AlcoholOwnerKey = "Puddle.Alcohol";
        private const float ChargeSeconds = 3f;
        private const float DrainSeconds = 1.5f;
        private const float EffectSeconds = 5f;
        private const float RefreshSeconds = 0.2f;

        [SerializeField] private CombatBuffDebuffApplicationDefinition alcoholBuffDefinition;
        [SerializeField] private CombatBuffDebuffApplicationDefinition confusionDefinition;

        private readonly List<AlcoholPuddleArea> contactingPuddles = new();
        private GameplayEffectRunner runner;
        private CombatBuffDebuffApplier applier;
        private CombatBuffDebuffApplicationDefinition appliedDefinition;
        private float chargedSeconds;
        private float drainStartCharge;
        private float drainElapsed;
        private float refreshElapsed;

        public float FillAmount => Mathf.Clamp01(chargedSeconds / ChargeSeconds);
        public bool IsOverflowing { get; private set; }
        public bool IsDraining { get; private set; }
        public bool HasAlcoholContact => contactingPuddles.Count > 0;

        private void Awake()
        {
            runner = GetComponent<GameplayEffectRunner>();
            applier = GetComponent<CombatBuffDebuffApplier>();
        }

        public void EnterPuddle(AlcoholPuddleArea source)
        {
            if (!isActiveAndEnabled || !IsValidSource(source) || contactingPuddles.Contains(source))
                return;

            contactingPuddles.Add(source);
            IsDraining = false;
            drainElapsed = 0f;
            if (IsOverflowing)
                RefreshOverflowEffect();
            else if (HasOwnedAlcoholBuff())
                ApplyEffect(alcoholBuffDefinition);
        }

        public void ExitPuddle(AlcoholPuddleArea source)
        {
            if (!contactingPuddles.Remove(source) || HasAlcoholContact)
                return;

            BeginDrain();
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float deltaTime)
        {
            RemoveInvalidSources();
            if (deltaTime <= 0f)
                return;

            if (HasAlcoholContact)
            {
                float timeToOverflow = ChargeSeconds - chargedSeconds;
                chargedSeconds = Mathf.Min(ChargeSeconds, chargedSeconds + deltaTime);
                if (!IsOverflowing && deltaTime > timeToOverflow)
                {
                    chargedSeconds = ChargeSeconds;
                    IsOverflowing = true;
                    RefreshOverflowEffect();
                    deltaTime = Mathf.Max(0f, deltaTime - timeToOverflow);
                }

                if (IsOverflowing || HasOwnedAlcoholBuff())
                    TickRefresh(deltaTime);
                return;
            }

            if (!IsDraining)
                return;

            float drainStep = Mathf.Min(deltaTime, DrainSeconds - drainElapsed);
            drainElapsed += drainStep;
            chargedSeconds = Mathf.Max(0f, drainStartCharge * (1f - drainElapsed / DrainSeconds));
            if (IsOverflowing || HasOwnedAlcoholBuff())
                TickRefresh(drainStep);

            if (drainElapsed >= DrainSeconds)
                CompleteDrain();
        }

        private void RemoveInvalidSources()
        {
            bool hadContact = HasAlcoholContact;
            for (int i = contactingPuddles.Count - 1; i >= 0; i--)
                if (!IsValidSource(contactingPuddles[i]))
                    contactingPuddles.RemoveAt(i);

            if (hadContact && !HasAlcoholContact)
                BeginDrain();
        }

        private static bool IsValidSource(AlcoholPuddleArea source) =>
            source != null && source.isActiveAndEnabled && source.CanApplyGroundEffect;

        private void BeginDrain()
        {
            IsDraining = FillAmount > 0f;
            drainStartCharge = chargedSeconds;
            drainElapsed = 0f;
        }

        private void TickRefresh(float deltaTime)
        {
            refreshElapsed += deltaTime;
            if (refreshElapsed < RefreshSeconds)
                return;

            // One pulse per frame is sufficient, including after a long frame.
            refreshElapsed %= RefreshSeconds;
            if (IsOverflowing)
                RefreshOverflowEffect(resetRefreshClock: false);
            else
                ApplyEffect(alcoholBuffDefinition);
        }

        private void RefreshOverflowEffect(bool resetRefreshClock = true)
        {
            CombatBuffDebuffApplicationDefinition desired = HasSpiriReward()
                ? alcoholBuffDefinition : confusionDefinition;
            if (appliedDefinition != desired)
            {
                RemoveOwnedEffect(appliedDefinition);
                appliedDefinition = desired;
            }

            ApplyEffect(desired);
            if (resetRefreshClock)
                refreshElapsed = 0f;
        }

        private void CompleteDrain()
        {
            if (IsOverflowing || HasOwnedAlcoholBuff())
            {
                // The final refresh aligns the GE and its five-second attribute
                // modifiers. Confusion instead ends with the empty glass.
                if (appliedDefinition == alcoholBuffDefinition || !IsOverflowing)
                    ApplyEffect(alcoholBuffDefinition);
                else
                    RemoveOwnedEffect(appliedDefinition);
            }

            chargedSeconds = 0f;
            IsOverflowing = false;
            IsDraining = false;
            drainElapsed = 0f;
            drainStartCharge = 0f;
            refreshElapsed = 0f;
        }

        private void ApplyEffect(CombatBuffDebuffApplicationDefinition definition)
        {
            if (definition == null)
                return;

            if (applier == null)
                applier = CombatBuffDebuffApplier.GetOrAdd(gameObject);
            applier.ApplyFromSource(this, gameObject, definition, AlcoholOwnerKey, EffectSeconds);
        }

        private void RemoveOwnedEffect(CombatBuffDebuffApplicationDefinition definition)
        {
            if (runner != null && definition != null && definition.GameplayEffect != null)
                runner.EndEffectsBySourceObject(gameObject, definition.GameplayEffect, this);
        }

        private bool HasOwnedAlcoholBuff()
        {
            if (runner == null || alcoholBuffDefinition == null)
                return false;

            ActiveGameplayEffect active = runner.FindActiveEffect(alcoholBuffDefinition.GameplayEffect, gameObject);
            return active != null && active.SourceObject == this && active.TimeRemaining > 0f;
        }

        internal bool OwnsSceneLocalEffect(string effectId) =>
            OwnsSceneLocalEffect(alcoholBuffDefinition, effectId) ||
            OwnsSceneLocalEffect(confusionDefinition, effectId);

        internal bool OwnsSceneLocalTag(string tagPath) =>
            OwnsSceneLocalTag(alcoholBuffDefinition, tagPath) ||
            OwnsSceneLocalTag(confusionDefinition, tagPath);

        private bool OwnsSceneLocalEffect(CombatBuffDebuffApplicationDefinition definition, string effectId)
        {
            GameplayEffect effect = definition != null ? definition.GameplayEffect : null;
            return effect != null && effect.name == effectId && OwnsActiveEffect(effect);
        }

        private bool OwnsSceneLocalTag(CombatBuffDebuffApplicationDefinition definition, string tagPath)
        {
            GameplayEffect effect = definition != null ? definition.GameplayEffect : null;
            if (effect == null || !OwnsActiveEffect(effect))
                return false;

            if (effect.grantedTags != null)
                for (int i = 0; i < effect.grantedTags.Count; i++)
                    if (MatchesTag(effect.grantedTags[i], tagPath))
                        return true;

            if (effect.grantedTagSets == null || effect.grantedTagSets.Count == 0)
                return false;

            var granted = new HashSet<GameplayTag>();
            var visited = new HashSet<GameplayTagSet>();
            for (int i = 0; i < effect.grantedTagSets.Count; i++)
                if (effect.grantedTagSets[i] != null)
                    effect.grantedTagSets[i].CollectTags(granted, visited);
            foreach (GameplayTag tag in granted)
                if (MatchesTag(tag, tagPath))
                    return true;
            return false;
        }

        private bool OwnsActiveEffect(GameplayEffect effect)
        {
            if (runner == null)
                return false;

            IReadOnlyList<ActiveGameplayEffect> active = runner.ActiveEffects;
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null && active[i].Effect == effect && active[i].SourceObject == this)
                    return true;
            return false;
        }

        // Explicit-tag snapshots currently use the leaf Name, while other callers
        // may supply the full Path. Both identify the configured granted tag here.
        private static bool MatchesTag(GameplayTag tag, string tagPath) =>
            tag != null && (tag.Path == tagPath || tag.Name == tagPath);

        private static bool HasSpiriReward()
        {
            int amount = 0;
            List<AffectionRecord> records = GameDataStore.Data?.affectionData?.affectionRecords;
            if (records != null)
                for (int i = 0; i < records.Count; i++)
                    if (records[i] != null && records[i].npcId == SpiriNpcId)
                        amount = records[i].amount;

            List<PendingRunAffectionChange> pending = RunSessionStore.Data?.pendingRunAffectionChanges;
            if (pending != null)
                for (int i = 0; i < pending.Count; i++)
                    if (pending[i] != null && pending[i].npcId == SpiriNpcId)
                        amount += pending[i].delta;

            // The manager can exist before Start has loaded its profile cache.
            int cached = AffectionManager.Instance != null
                ? AffectionManager.Instance.GetAffection(SpiriNpcId) : 0;
            return Mathf.Max(cached, amount) >= 1;
        }

        private void ResetExposure()
        {
            RemoveOwnedEffect(alcoholBuffDefinition);
            RemoveOwnedEffect(confusionDefinition);
            contactingPuddles.Clear();
            appliedDefinition = null;
            chargedSeconds = 0f;
            IsOverflowing = false;
            IsDraining = false;
            drainStartCharge = 0f;
            drainElapsed = 0f;
            refreshElapsed = 0f;
        }

        private void OnDisable() => ResetExposure();
        private void OnDestroy() => ResetExposure();
    }
}
