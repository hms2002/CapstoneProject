using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_ApprenticeHeroSwordChargeSpin", menuName = "GAS/Weapon/Apprentice Hero Sword/Logic Charge Spin")]
public sealed class AbilityLogic_ApprenticeHeroSwordChargeSpin : AbilityLogic
{
    private readonly Dictionary<AbilitySpec, List<MeleeHitboxActor>> activeHitboxesBySpec = new();
    private readonly Dictionary<AbilitySpec, ApprenticeHeroSwordChargePresentationRuntime> activeChargeVisualsBySpec = new();

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null || spec?.Definition == null)
            yield break;

        ApprenticeHeroSwordChargeSpinData data = spec.Definition.sourceObject as ApprenticeHeroSwordChargeSpinData;
        if (data == null)
        {
            Debug.LogError("[ApprenticeHeroSwordChargeSpin] AbilityDefinition.sourceObject must be ApprenticeHeroSwordChargeSpinData.");
            yield break;
        }

        try
        {
            TryPlayAnim(system, data.ChargeAnimationTrigger, spec.Definition);
            AbilityAudioRouter.PlayOneShot(data.ChargeStartSound, system, spec, sourceObjectOverride: data);

            ApprenticeHeroSwordChargePresentationRuntime chargePresentation = BeginChargePresentation(system, spec, data);

            InputBindingService input = InputBindingService.EnsureInstance();
            if (input == null)
            {
                Debug.LogError("[ApprenticeHeroSwordChargeSpin] InputBindingService is required for hold-release timing.");
                yield break;
            }

            float chargeElapsed = 0f;
            chargePresentation?.Update(0f);
            while (true)
            {
                if (IsAbilityCancelled(spec))
                {
                    DestroyTrackedHitboxes(spec);
                    yield break;
                }

                bool released = input.WasReleasedThisFrame(InputActionId.Skill2) || !input.IsPressed(InputActionId.Skill2);
                if (released)
                    break;

                chargeElapsed += Time.deltaTime;
                float liveChargeRatio = data.MaxChargeSeconds > 0f
                    ? Mathf.Clamp01(chargeElapsed / data.MaxChargeSeconds)
                    : 1f;
                chargePresentation?.Update(liveChargeRatio);
                yield return null;
            }

            float effectiveChargeSeconds = Mathf.Clamp(chargeElapsed, data.MinChargeSeconds, data.MaxChargeSeconds);
            float chargeRatio = data.MaxChargeSeconds > 0f
                ? Mathf.Clamp01(effectiveChargeSeconds / data.MaxChargeSeconds)
                : 1f;
            float damageScale = Mathf.Lerp(data.MinDamageScale, data.MaxDamageScale, chargeRatio);
            Vector2 releaseSizeMultiplier = data.ResolveChargeReleaseSizeMultiplier(chargeRatio);
            Color releaseVisualColor = data.ResolveChargeReleaseColor(chargeRatio);

            chargePresentation?.Update(chargeRatio);
            StopChargePresentation(spec, clearParticles: false);

            TryPlayAnim(system, data.ReleaseAnimationTrigger, spec.Definition);
            AbilityAudioRouter.PlayOneShot(data.ReleaseSound, system, spec, sourceObjectOverride: data);

            Vector2 baseDirection = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            if (baseDirection.sqrMagnitude <= 0.0001f)
                baseDirection = Vector2.right;
            baseDirection.Normalize();

            yield return WaitForReleaseHitEvent(system, spec, data);

            try
            {
                if (IsAbilityCancelled(spec))
                {
                    DestroyTrackedHitboxes(spec);
                    yield break;
                }

                Vector2 center = system.transform.position;
                CombatHitPayload payload = ApprenticeHeroSwordHitUtility.BuildPayload(system, spec, data.Damage, damageScale);
                MeleeHitboxActor hitbox = ApprenticeHeroSwordHitUtility.SpawnHitbox(
                    system,
                    spec,
                    data.Hitbox,
                    data.HitLayers,
                    payload,
                    center,
                    baseDirection,
                    baseDirection.x < 0f,
                    releaseSizeMultiplier,
                    releaseSizeMultiplier,
                    true,
                    releaseVisualColor);

                TrackHitbox(spec, hitbox);
                AbilityAudioRouter.PlayOneShotAtPosition(data.PulseSound, system, spec, center, data);

                float elapsed = 0f;
                while (elapsed < data.SpinDuration)
                {
                    if (IsAbilityCancelled(spec))
                    {
                        DestroyTrackedHitboxes(spec);
                        yield break;
                    }

                    elapsed += Time.deltaTime;
                    yield return null;
                }

                if (data.RecoveryDuration > 0f)
                    spec.SetFloat("RecoveryOverride", data.RecoveryDuration);
            }
            finally
            {
                if (IsAbilityCancelled(spec))
                    DestroyTrackedHitboxes(spec);
                else
                    ForgetTrackedHitboxes(spec);
            }
        }
        finally
        {
            StopChargePresentation(spec, clearParticles: true);
        }
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        DestroyTrackedHitboxes(spec);
        StopChargePresentation(spec, clearParticles: true);
    }

    private void TrackHitbox(AbilitySpec spec, MeleeHitboxActor hitbox)
    {
        if (spec == null || hitbox == null)
            return;

        if (!activeHitboxesBySpec.TryGetValue(spec, out List<MeleeHitboxActor> hitboxes) || hitboxes == null)
        {
            hitboxes = new List<MeleeHitboxActor>();
            activeHitboxesBySpec[spec] = hitboxes;
        }

        hitboxes.Add(hitbox);
    }

    private void DestroyTrackedHitboxes(AbilitySpec spec)
    {
        if (spec == null || !activeHitboxesBySpec.TryGetValue(spec, out List<MeleeHitboxActor> hitboxes))
            return;

        for (int i = 0; i < hitboxes.Count; i++)
        {
            MeleeHitboxActor hitbox = hitboxes[i];
            if (hitbox != null)
                Destroy(hitbox.gameObject);
        }

        activeHitboxesBySpec.Remove(spec);
    }

    private void ForgetTrackedHitboxes(AbilitySpec spec)
    {
        if (spec != null)
            activeHitboxesBySpec.Remove(spec);
    }

    private ApprenticeHeroSwordChargePresentationRuntime BeginChargePresentation(
        AbilitySystem system,
        AbilitySpec spec,
        ApprenticeHeroSwordChargeSpinData data)
    {
        if (system == null || spec == null || data == null)
            return null;

        StopChargePresentation(spec, clearParticles: true);

        ApprenticeHeroSwordChargePresentationRuntime visual =
            ApprenticeHeroSwordChargePresentationRuntime.Create(system, data);
        if (visual == null)
            return null;

        activeChargeVisualsBySpec[spec] = visual;
        return visual;
    }

    private void StopChargePresentation(AbilitySpec spec, bool clearParticles)
    {
        if (spec == null || !activeChargeVisualsBySpec.TryGetValue(spec, out ApprenticeHeroSwordChargePresentationRuntime visual))
            return;

        visual.Release(clearParticles);
        activeChargeVisualsBySpec.Remove(spec);
    }

    private static void TryPlayAnim(AbilitySystem system, string animationTrigger, AbilityDefinition definition)
    {
        if (system == null || string.IsNullOrWhiteSpace(animationTrigger))
            return;

        system.TryPlayAnimationTriggerHash(Animator.StringToHash(animationTrigger), definition);
    }

    private static IEnumerator WaitForReleaseHitEvent(
        AbilitySystem system,
        AbilitySpec spec,
        ApprenticeHeroSwordChargeSpinData data)
    {
        if (system == null || spec == null || data == null || data.ReleaseHitEventTag == null)
            yield break;

        float timeout = data.ReleaseHitEventTimeout > 0f
            ? data.ReleaseHitEventTimeout
            : data.SpinDuration;

        yield return AbilityTasks.WaitGameplayEvent(
            system,
            spec,
            data.ReleaseHitEventTag,
            onReceived: null,
            timeout: timeout,
            predicate: eventData => eventData.Spec == spec);
    }

    private sealed class ApprenticeHeroSwordChargePresentationRuntime
    {
        private const int RevealMaskTextureSize = 16;
        private static Sprite directionalRevealMaskSprite;

        private readonly ApprenticeHeroSwordChargeSpinData data;
        private readonly ParticleSystem particleRoot;
        private readonly GameObject revealRoot;
        private readonly SpriteRenderer revealRenderer;
        private readonly SpriteMask revealMask;
        private readonly Sprite fallbackRevealSprite;
        private readonly Transform fullChargeVfxParent;
        private ParticleSystem fullChargeVfxInstance;
        private bool fullChargeVfxPlayed;

        private ApprenticeHeroSwordChargePresentationRuntime(
            ApprenticeHeroSwordChargeSpinData data,
            ParticleSystem particleRoot,
            GameObject revealRoot,
            SpriteRenderer revealRenderer,
            SpriteMask revealMask,
            Sprite fallbackRevealSprite,
            Transform fullChargeVfxParent)
        {
            this.data = data;
            this.particleRoot = particleRoot;
            this.revealRoot = revealRoot;
            this.revealRenderer = revealRenderer;
            this.revealMask = revealMask;
            this.fallbackRevealSprite = fallbackRevealSprite;
            this.fullChargeVfxParent = fullChargeVfxParent;
        }

        public static ApprenticeHeroSwordChargePresentationRuntime Create(
            AbilitySystem system,
            ApprenticeHeroSwordChargeSpinData data)
        {
            if (system == null || data == null)
                return null;

            SpriteRenderer sourceRenderer = ResolveWeaponRenderer(system);
            Transform particleParent = sourceRenderer != null ? sourceRenderer.transform : system.transform;
            ParticleSystem particle = CreateParticle(data, particleParent);

            GameObject revealRoot = null;
            SpriteRenderer revealRenderer = null;
            SpriteMask revealMask = null;
            Sprite fallbackRevealSprite = sourceRenderer != null ? sourceRenderer.sprite : null;

            if (data.EnableChargeReveal && sourceRenderer != null)
            {
                CreateReveal(
                    sourceRenderer,
                    data,
                    fallbackRevealSprite,
                    out revealRoot,
                    out revealRenderer,
                    out revealMask);
            }

            Transform fullChargeVfxParent = data.FullChargeVfxPrefab != null ? system.transform : null;
            if (particle == null && revealRoot == null && fullChargeVfxParent == null)
                return null;

            return new ApprenticeHeroSwordChargePresentationRuntime(
                data,
                particle,
                revealRoot,
                revealRenderer,
                revealMask,
                fallbackRevealSprite,
                fullChargeVfxParent);
        }

        public void Update(float chargeRatio)
        {
            float ratio = Mathf.Clamp01(chargeRatio);
            bool fullCharge = ratio >= data.FullChargeSpriteThreshold;
            if (fullCharge)
                PlayFullChargeVfx();

            if (revealRenderer != null)
            {
                Sprite chargeSprite = data.ChargeRevealSprite != null
                    ? data.ChargeRevealSprite
                    : fallbackRevealSprite;
                Sprite fullSprite = data.FullChargeRevealSprite != null
                    ? data.FullChargeRevealSprite
                    : chargeSprite;

                revealRenderer.sprite = fullCharge ? fullSprite : chargeSprite;
                revealRenderer.color = fullCharge ? data.FullChargeRevealColor : data.ChargeRevealColor;
            }

            if (revealMask == null)
                return;

            Sprite maskSprite = data.ChargeRevealMaskSprite != null
                ? data.ChargeRevealMaskSprite
                : (data.UseDirectionalChargeRevealMask
                    ? GetDirectionalRevealMaskSprite()
                    : (revealRenderer != null ? revealRenderer.sprite : fallbackRevealSprite));
            if (maskSprite != null)
                revealMask.sprite = maskSprite;

            revealMask.alphaCutoff = data.ChargeRevealMaskAlphaCutoff;
            if (data.UseDirectionalChargeRevealMask)
            {
                ApplyDirectionalRevealMask(ratio);
                return;
            }

            Vector2 maskOffset = Vector2.Lerp(data.ChargeRevealMaskStartOffset, data.ChargeRevealMaskEndOffset, ratio);
            Vector2 maskScale = Vector2.Lerp(data.ChargeRevealMaskStartScale, data.ChargeRevealMaskEndScale, ratio);
            Transform maskTransform = revealMask.transform;
            maskTransform.localPosition = new Vector3(maskOffset.x, maskOffset.y, 0f);
            maskTransform.localRotation = Quaternion.identity;
            maskTransform.localScale = new Vector3(maskScale.x, maskScale.y, 1f);
        }

        public void Release(bool clearParticles)
        {
            if (particleRoot != null)
            {
                ParticleSystemStopBehavior stopBehavior = clearParticles
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting;
                ParticleSystem[] systems = particleRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
                for (int i = 0; i < systems.Length; i++)
                    systems[i].Stop(withChildren: true, stopBehavior);

                float destroyDelay = clearParticles ? 0f : data.ChargeParticleStopDelay;
                if (destroyDelay > 0f)
                    UnityEngine.Object.Destroy(particleRoot.gameObject, destroyDelay);
                else
                    UnityEngine.Object.Destroy(particleRoot.gameObject);
            }

            if (revealRoot != null)
                UnityEngine.Object.Destroy(revealRoot);

            if (clearParticles && fullChargeVfxInstance != null)
            {
                StopParticleSystems(fullChargeVfxInstance, ParticleSystemStopBehavior.StopEmittingAndClear);
                UnityEngine.Object.Destroy(fullChargeVfxInstance.gameObject);
                fullChargeVfxInstance = null;
            }
        }

        private void PlayFullChargeVfx()
        {
            if (fullChargeVfxPlayed || data.FullChargeVfxPrefab == null || fullChargeVfxParent == null)
                return;

            fullChargeVfxPlayed = true;
            fullChargeVfxInstance = UnityEngine.Object.Instantiate(data.FullChargeVfxPrefab, fullChargeVfxParent);
            if (fullChargeVfxInstance == null)
                return;

            Transform instanceTransform = fullChargeVfxInstance.transform;
            instanceTransform.localPosition = data.FullChargeVfxLocalPosition;
            instanceTransform.localRotation = Quaternion.Euler(data.FullChargeVfxLocalEulerAngles);
            instanceTransform.localScale = data.FullChargeVfxLocalScale;
            fullChargeVfxInstance.gameObject.SetActive(true);

            ParticleSystem[] systems = fullChargeVfxInstance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                ParticleSystem.MainModule main = system.main;
                if (data.FullChargeVfxUseLocalSimulation)
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;

                system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(withChildren: true);
            }

            float destroyDelay = data.FullChargeVfxDestroyDelay;
            if (destroyDelay > 0f)
                UnityEngine.Object.Destroy(fullChargeVfxInstance.gameObject, destroyDelay);
        }

        private static ParticleSystem CreateParticle(
            ApprenticeHeroSwordChargeSpinData data,
            Transform parent)
        {
            if (data.ChargeParticlePrefab == null || parent == null)
                return null;

            ParticleSystem instance = UnityEngine.Object.Instantiate(data.ChargeParticlePrefab, parent);
            if (instance == null)
                return null;

            Transform instanceTransform = instance.transform;
            instanceTransform.localPosition = data.ChargeParticleLocalPosition;
            instanceTransform.localRotation = Quaternion.Euler(data.ChargeParticleLocalEulerAngles);
            instanceTransform.localScale = data.ChargeParticleLocalScale;
            instance.gameObject.SetActive(true);

            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                if (data.ChargeParticleUseLocalSimulation)
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;

                system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(withChildren: true);
            }

            return instance;
        }

        private static void StopParticleSystems(ParticleSystem root, ParticleSystemStopBehavior stopBehavior)
        {
            if (root == null)
                return;

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < systems.Length; i++)
                systems[i].Stop(withChildren: true, stopBehavior);
        }

        private static void CreateReveal(
            SpriteRenderer sourceRenderer,
            ApprenticeHeroSwordChargeSpinData data,
            Sprite fallbackRevealSprite,
            out GameObject revealRoot,
            out SpriteRenderer revealRenderer,
            out SpriteMask revealMask)
        {
            revealRoot = null;
            revealRenderer = null;
            revealMask = null;

            Sprite revealSprite = data.ChargeRevealSprite != null
                ? data.ChargeRevealSprite
                : fallbackRevealSprite;
            Sprite maskSprite = data.ChargeRevealMaskSprite != null
                ? data.ChargeRevealMaskSprite
                : (data.UseDirectionalChargeRevealMask ? GetDirectionalRevealMaskSprite() : revealSprite);

            if (sourceRenderer == null || revealSprite == null || maskSprite == null)
                return;

            revealRoot = new GameObject("ApprenticeHeroSword_ChargeReveal");
            Transform revealTransform = revealRoot.transform;
            revealTransform.SetParent(sourceRenderer.transform, worldPositionStays: false);
            revealTransform.localPosition = data.ChargeRevealLocalPosition;
            revealTransform.localRotation = Quaternion.Euler(data.ChargeRevealLocalEulerAngles);
            revealTransform.localScale = data.ChargeRevealLocalScale;

            GameObject rendererObject = new("Reveal");
            Transform rendererTransform = rendererObject.transform;
            rendererTransform.SetParent(revealTransform, worldPositionStays: false);

            revealRenderer = rendererObject.AddComponent<SpriteRenderer>();
            revealRenderer.sprite = revealSprite;
            revealRenderer.color = data.ChargeRevealColor;
            revealRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            revealRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            revealRenderer.sortingOrder = sourceRenderer.sortingOrder + data.ChargeRevealSortingOrderOffset;
            revealRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            revealRenderer.drawMode = sourceRenderer.drawMode;
            revealRenderer.size = sourceRenderer.size;
            revealRenderer.flipX = sourceRenderer.flipX;
            revealRenderer.flipY = sourceRenderer.flipY;
            revealRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;

            GameObject maskObject = new("RevealMask");
            Transform maskTransform = maskObject.transform;
            maskTransform.SetParent(revealTransform, worldPositionStays: false);

            revealMask = maskObject.AddComponent<SpriteMask>();
            revealMask.sprite = maskSprite;
            revealMask.alphaCutoff = data.ChargeRevealMaskAlphaCutoff;
            revealMask.isCustomRangeActive = true;
            revealMask.frontSortingLayerID = sourceRenderer.sortingLayerID;
            revealMask.backSortingLayerID = sourceRenderer.sortingLayerID;
            revealMask.frontSortingOrder = revealRenderer.sortingOrder + 1;
            revealMask.backSortingOrder = revealRenderer.sortingOrder - 1;
        }

        private void ApplyDirectionalRevealMask(float ratio)
        {
            if (revealMask == null)
                return;

            Sprite boundsSprite = revealRenderer != null && revealRenderer.sprite != null
                ? revealRenderer.sprite
                : fallbackRevealSprite;
            if (boundsSprite == null)
                return;

            Vector2 direction = ResolveRevealDirection(revealRenderer);
            Vector2 perpendicular = new(-direction.y, direction.x);
            CalculateRevealBounds(
                boundsSprite,
                direction,
                perpendicular,
                out float minDirection,
                out float maxDirection,
                out float minPerpendicular,
                out float maxPerpendicular);

            float padding = data.ChargeRevealMaskPadding;
            float length = Mathf.Max(0.001f, maxDirection - minDirection);
            float width = Mathf.Max(0.001f, maxPerpendicular - minPerpendicular);
            float visibleLength = Mathf.Max(0.001f, (length + padding * 2f) * Mathf.Clamp01(ratio));
            float centerDirection = minDirection - padding + visibleLength * 0.5f;
            float centerPerpendicular = (minPerpendicular + maxPerpendicular) * 0.5f;
            Vector2 localCenter = direction * centerDirection + perpendicular * centerPerpendicular;

            Transform maskTransform = revealMask.transform;
            maskTransform.localPosition = new Vector3(localCenter.x, localCenter.y, 0f);
            maskTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            maskTransform.localScale = new Vector3(
                visibleLength,
                width * data.ChargeRevealMaskWidthMultiplier + padding * 2f,
                1f);
        }

        private Vector2 ResolveRevealDirection(SpriteRenderer renderer)
        {
            Vector2 direction = data.ChargeRevealMaskLocalDirection;
            if (renderer != null)
            {
                if (renderer.flipX)
                    direction.x *= -1f;
                if (renderer.flipY)
                    direction.y *= -1f;
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector2(-1f, 1f).normalized;
        }

        private static void CalculateRevealBounds(
            Sprite sprite,
            Vector2 direction,
            Vector2 perpendicular,
            out float minDirection,
            out float maxDirection,
            out float minPerpendicular,
            out float maxPerpendicular)
        {
            Bounds bounds = sprite.bounds;
            Vector2 min = bounds.min;
            Vector2 max = bounds.max;

            Vector2[] corners =
            {
                new(min.x, min.y),
                new(min.x, max.y),
                new(max.x, min.y),
                new(max.x, max.y)
            };

            minDirection = maxDirection = Vector2.Dot(corners[0], direction);
            minPerpendicular = maxPerpendicular = Vector2.Dot(corners[0], perpendicular);
            for (int i = 1; i < corners.Length; i++)
            {
                float alongDirection = Vector2.Dot(corners[i], direction);
                float alongPerpendicular = Vector2.Dot(corners[i], perpendicular);
                minDirection = Mathf.Min(minDirection, alongDirection);
                maxDirection = Mathf.Max(maxDirection, alongDirection);
                minPerpendicular = Mathf.Min(minPerpendicular, alongPerpendicular);
                maxPerpendicular = Mathf.Max(maxPerpendicular, alongPerpendicular);
            }
        }

        private static Sprite GetDirectionalRevealMaskSprite()
        {
            if (directionalRevealMaskSprite != null)
                return directionalRevealMaskSprite;

            Texture2D texture = new(RevealMaskTextureSize, RevealMaskTextureSize, TextureFormat.RGBA32, false)
            {
                name = "ApprenticeHeroSword_ChargeRevealMaskSquare",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[RevealMaskTextureSize * RevealMaskTextureSize];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);

            texture.SetPixels32(pixels);
            texture.Apply();

            directionalRevealMaskSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, RevealMaskTextureSize, RevealMaskTextureSize),
                new Vector2(0.5f, 0.5f),
                RevealMaskTextureSize);
            directionalRevealMaskSprite.name = "ApprenticeHeroSword_ChargeRevealMaskSquare";
            return directionalRevealMaskSprite;
        }

        private static SpriteRenderer ResolveWeaponRenderer(AbilitySystem system)
        {
            if (system == null)
                return null;

            Animator weaponAnimator = system.WeaponAnimator;
            if (weaponAnimator != null)
            {
                SpriteRenderer renderer = ResolveRendererFromAnimator(weaponAnimator);
                if (renderer != null)
                    return renderer;
            }

            WeaponEquipController equipController = system.GetComponentInChildren<WeaponEquipController>();
            if (equipController == null)
                return null;

            WeaponVisualRig2D visualRig = equipController.GetComponentInChildren<WeaponVisualRig2D>();
            if (visualRig != null)
                return ResolveRendererFromRig(visualRig);

            return null;
        }

        private static SpriteRenderer ResolveRendererFromAnimator(Animator animator)
        {
            if (animator == null)
                return null;

            WeaponVisualRig2D visualRig = animator.GetComponentInParent<WeaponVisualRig2D>();
            if (visualRig != null)
            {
                SpriteRenderer renderer = ResolveRendererFromRig(visualRig);
                if (renderer != null)
                    return renderer;
            }

            return animator.GetComponentInChildren<SpriteRenderer>();
        }

        private static SpriteRenderer ResolveRendererFromRig(WeaponVisualRig2D visualRig)
        {
            if (visualRig == null)
                return null;

            Transform renderRoot = visualRig.RenderRoot;
            if (renderRoot == null)
                return null;

            SpriteRenderer renderer = renderRoot.GetComponent<SpriteRenderer>();
            return renderer != null ? renderer : renderRoot.GetComponentInChildren<SpriteRenderer>();
        }
    }
}
