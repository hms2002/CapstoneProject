#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

/// <summary>Root-owned TestScene sandbox setup using real UI controllers; no mock hierarchy or persistent reward selection.</summary>
public static class UiBatchingLiveScenarios
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] DefaultLocales = { "ko", "en", "ja", "zh-Hans", "zh-Hant" };
    [Serializable] public sealed class ScenarioEvidence
    {
        public string variant;
        public string scope = "Existing TestScene sandbox, actual GlobalUIRoot/controllers, two normal temporary weapons in initially empty slots. No HP/resource override, ability cast or reward effect selection.";
        public List<string> temporaryWeapons = new();
        public List<Offer> rewardOffers = new();
        public List<string> settledFontGates = new();
    }
    [Serializable] public sealed class Offer
    {
        public string locale;
        public List<string> rewardIds = new();
        public List<string> iconNames = new();
    }

    /// <summary>Use -uiBatchSetupMethod UiBatchingLiveScenarios.Setup and -uiBatchRoot GameplayHUDCanvas.</summary>
    public static void Run()
    {
        if (Arg("-uiBatchSetupMethod", "") != "UiBatchingLiveScenarios.Setup")
            throw new ArgumentException("Pass -uiBatchSetupMethod UiBatchingLiveScenarios.Setup -uiBatchRoot GameplayHUDCanvas.");
        TestScenePresentationTraceIsolation.Enter();
        UiBatchingValidationRunner.Run();
    }

    public static IEnumerator Setup() => RunSuite();

    public static IEnumerator RunSuite()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "TestScene")
            throw new InvalidOperationException("Live UI scenarios require the existing TestScene sandbox in Play Mode.");
        double timeout = Time.realtimeSinceStartupAsDouble + 60;
        while ((PlayerRuntimeRegistry.CurrentPlayer == null || GlobalUIRoot.Instance == null || UIManager.Instance == null) &&
            Time.realtimeSinceStartupAsDouble < timeout) yield return null;
        if (PlayerRuntimeRegistry.CurrentPlayer == null || GlobalUIRoot.Instance == null || UIManager.Instance == null)
            throw new TimeoutException("The authored player/global UI did not initialize.");
        var panel = Object.FindAnyObjectByType<TestSceneControlPanel>();
        if (panel == null) throw new InvalidOperationException("TestScene sandbox panel is missing.");
        panel.ClearSpawned();
        var panelRoot = (RectTransform)typeof(TestSceneControlPanel).GetField("panelRoot", Fields)?.GetValue(panel);
        if (panelRoot != null && panelRoot.gameObject.activeSelf) panel.TogglePanel();
        UIManager.Instance.CloseAllPopups();
        for (int frame = 0; frame < 10; frame++) yield return null;
        if (Object.FindObjectsByType<HeartTokenUI>(FindObjectsInactive.Include).Length > 128)
            throw new InvalidOperationException("Normal HUD guard failed; do not measure an inflated-health fixture.");
        var session = Object.FindAnyObjectByType<LevelRewardSessionController>(FindObjectsInactive.Include);
        var presenter = Object.FindAnyObjectByType<LevelRewardSelectionPresenter>(FindObjectsInactive.Include);
        if (session == null || presenter == null || RunLevelProgression.State == null || !RunSessionStore.IsRunActive)
            throw new InvalidOperationException("Existing reward session/run sandbox is unavailable.");
        var state = RunLevelProgression.State;
        int previousPending = state.pendingRewardCount, previousSeed = state.rewardRandomSeed, previousSequence = state.rewardOfferSequence;
        LevelRewardOfferState previousOffer = state.activeRewardOffer;
        yield return WaitForAppLocaleBootstrap();
        var previousLocale = LocalizationSettings.SelectedLocale;
        string output = Path.GetFullPath(Arg("-uiBatchOutput", "Library/UiBatchValidation"));
        var evidence = new ScenarioEvidence { variant = Arg("-uiBatchVariant", "unknown") };
        var weapons = PlayerRuntimeRegistry.CurrentPlayer.GetComponent<WeaponInventory2D>();
        if (weapons == null || weapons.SlotCount != 2 || weapons.ActiveIndex >= 0 ||
            weapons.GetWeaponInSlot(0) != null || weapons.GetWeaponInSlot(1) != null)
            throw new InvalidOperationException("The disposable fixture requires the sandbox's two initially empty weapon slots.");
        string[] locales = Arg("-uiBatchScenarioLocales", string.Join(",", DefaultLocales)).Split(',');
        try
        {
            string[] weaponPaths = {
                "Assets/_Project/Data/Items/Weapons/Definitions/WD_LightningSpear.asset",
                "Assets/_Project/Data/Items/Weapons/Definitions/WD_OddIron.asset" };
            for (int slot = 0; slot < weaponPaths.Length; slot++)
            {
                var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPaths[slot]);
                if (definition == null || !weapons.TrySetWeaponSlot(slot, definition))
                    throw new InvalidOperationException("The normal weapon inventory rejected the temporary fixture loadout.");
                evidence.temporaryWeapons.Add(weaponPaths[slot]);
            }
            weapons.Equip(0);
            for (int frame = 0; frame < 30; frame++) yield return null;
            foreach (string localeText in locales)
            {
                string code = localeText.Trim();
                yield return WaitForLocalizationInitialization();
                var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                if (locale == null) throw new InvalidOperationException("Requested locale is missing: " + code);
                LocalizationSettings.SelectedLocale = locale;
                UIManager.Instance.CloseAllPopups();
                Canvas hud = RequireCanvas(GlobalCanvasLayer.GameplayHUD);
                yield return WaitForFonts(hud, code, "hud-" + code, evidence);
                yield return UiBatchingValidationRunner.Capture(hud.gameObject, Path.Combine(output, "hud-" + code + ".json"),
                    "hud-live", evidence.variant, null);

                if (InventoryUIManager.Instance == null || !InventoryUIManager.Instance.TryOpen())
                    throw new InvalidOperationException("Existing player inventory rejected its normal open command.");
                yield return WaitSeconds(1.5);
                Canvas popup = RequireCanvas(GlobalCanvasLayer.Popup);
                yield return WaitForFonts(popup, code, "inventory-" + code, evidence);
                yield return UiBatchingValidationRunner.Capture(popup.gameObject, Path.Combine(output, "inventory-" + code + ".json"),
                    "inventory-live", evidence.variant, null);
                InventoryUIManager.Instance.Close();
                yield return WaitSeconds(1);
                UIManager.Instance.CloseAllPopups();

                // Only the disposable TestScene state is changed. The controller still validates
                // its normal pending-offer/combat/UI gates and owns pause, presentation and closing.
                state.pendingRewardCount = 1;
                state.rewardRandomSeed = 28147;
                state.rewardOfferSequence = 0;
                state.activeRewardOffer = new LevelRewardOfferState();
                if (!session.TryOpenSession(out string reason)) throw new InvalidOperationException("Reward open rejected: " + reason);
                if (!session.IsSessionOpen || session.Candidates.Count != 3) throw new InvalidOperationException("Expected exactly three real reward candidates.");
                var offer = new Offer { locale = code };
                foreach (LevelRewardDefinitionSO definition in session.Candidates)
                { offer.rewardIds.Add(definition.RewardId); offer.iconNames.Add(definition.Icon != null ? definition.Icon.name : "none"); }
                if (evidence.rewardOffers.Count > 0 && string.Join("|", evidence.rewardOffers[0].rewardIds) != string.Join("|", offer.rewardIds))
                    throw new InvalidOperationException("Reward candidates drifted across locale scenarios.");
                evidence.rewardOffers.Add(offer);
                timeout = Time.realtimeSinceStartupAsDouble + 20;
                var transitioning = typeof(LevelRewardSelectionPresenter).GetField("isTransitioning", Fields);
                while (transitioning != null && (bool)transitioning.GetValue(presenter) && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
                if (transitioning != null && (bool)transitioning.GetValue(presenter)) throw new TimeoutException("Reward opening animation did not settle.");
                yield return WaitSeconds(.5);
                yield return WaitForFonts(RequireCanvas(GlobalCanvasLayer.Reward), code, "reward3-" + code, evidence);
                yield return UiBatchingValidationRunner.Capture(RequireCanvas(GlobalCanvasLayer.Reward).gameObject,
                    Path.Combine(output, "reward3-" + code + ".json"), "reward-three-live", evidence.variant, null);
                session.CloseSession();
                UIManager.Instance.CloseAllPopups();
                yield return WaitSeconds(1);
            }
            // Menu chrome shares the authored Popup canvas; use the normal stack, not SetActive.
            PauseMenuUI pause = PauseMenuUI.EnsureInstance();
            if (pause == null || !UIManager.Instance.TryPushUI(pause)) throw new InvalidOperationException("Pause UI rejected its normal open command.");
            yield return WaitSeconds(1.5);
            yield return WaitForFonts(RequireCanvas(GlobalCanvasLayer.Popup), CurrentLocaleCode(), "pause", evidence);
            yield return UiBatchingValidationRunner.Capture(RequireCanvas(GlobalCanvasLayer.Popup).gameObject,
                Path.Combine(output, "pause.json"), "pause-live", evidence.variant, null);
            UIManager.Instance.CloseAllPopups(); yield return WaitSeconds(1);
            if (!UIManager.Instance.OpenSettingsPanel()) throw new InvalidOperationException("Settings UI rejected its normal open command.");
            yield return WaitSeconds(1.5);
            yield return WaitForFonts(RequireCanvas(GlobalCanvasLayer.Popup), CurrentLocaleCode(), "settings", evidence);
            yield return UiBatchingValidationRunner.Capture(RequireCanvas(GlobalCanvasLayer.Popup).gameObject,
                Path.Combine(output, "settings.json"), "settings-live", evidence.variant, null);
            UIManager.Instance.CloseAllPopups(); yield return WaitSeconds(1);
            if (!UIManager.Instance.OpenKeyBindingPanel()) throw new InvalidOperationException("Key-binding UI rejected its normal open command.");
            yield return WaitSeconds(1.5);
            yield return WaitForFonts(RequireCanvas(GlobalCanvasLayer.Popup), CurrentLocaleCode(), "keybind", evidence);
            yield return UiBatchingValidationRunner.Capture(RequireCanvas(GlobalCanvasLayer.Popup).gameObject,
                Path.Combine(output, "keybind.json"), "keybind-live", evidence.variant, null);
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "live-scenario-evidence.json"), JsonUtility.ToJson(evidence, true));
        }
        finally
        {
            session.CloseSession();
            if (UIManager.Instance != null) UIManager.Instance.CloseAllPopups();
            state.pendingRewardCount = previousPending; state.rewardRandomSeed = previousSeed;
            state.rewardOfferSequence = previousSequence; state.activeRewardOffer = previousOffer;
            if (weapons != null)
            {
                weapons.Unequip();
                if (!weapons.TrySetWeaponSlot(0, null, false) || !weapons.TrySetWeaponSlot(1, null, false))
                    Debug.LogError("UI batch fixture could not clear its temporary sandbox loadout.");
            }
            if (previousLocale != null) LocalizationSettings.SelectedLocale = previousLocale;
        }
    }

    static Canvas RequireCanvas(GlobalCanvasLayer layer)
    { Canvas canvas = GlobalUIRoot.GetCanvas(layer); if (canvas == null || !canvas.isActiveAndEnabled) throw new InvalidOperationException("Authored canvas is missing/inactive: " + layer); return canvas; }
    static string CurrentLocaleCode() => LocalizationSettings.SelectedLocale?.Identifier.Code ?? "unavailable";
    static IEnumerator WaitForAppLocaleBootstrap()
    {
        yield return WaitForLocalizationInitialization();
        // The app owner awaits the saved language's String Table after package initialization,
        // then writes SelectedLocale. Observe its completion before the harness selects a locale.
        FieldInfo initialized = typeof(GameSettingsService).GetField("initialized", Fields);
        FieldInfo pendingLocale = typeof(GameSettingsService).GetField("localeInitialization", Fields);
        if (initialized == null || pendingLocale == null) throw new MissingFieldException("GameSettingsService locale bootstrap state");
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        int stableFrames = 0;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            GameSettingsService settings = GameSettingsService.Instance;
            string expected = settings == null ? "unavailable" : settings.CurrentLanguage switch
            {
                GameLanguageOption.Korean => "ko", GameLanguageOption.Japanese => "ja",
                GameLanguageOption.SimplifiedChinese => "zh-Hans", GameLanguageOption.TraditionalChinese => "zh-Hant", _ => "en"
            };
            bool ready = settings != null && (bool)initialized.GetValue(settings) && pendingLocale.GetValue(settings) == null && CurrentLocaleCode() == expected;
            if (ready && ++stableFrames >= 2)
            {
                // The app clears its writer after SelectedLocale, but that write
                // starts a fresh package preload. Await that current operation too.
                yield return WaitForLocalizationInitialization();
                if ((bool)initialized.GetValue(settings) && pendingLocale.GetValue(settings) == null && CurrentLocaleCode() == expected)
                {
                    Debug.Log("UIBATCH_APP_LOCALE_READY," + expected);
                    yield break;
                }
                stableFrames = 0;
            }
            if (!ready) stableFrames = 0;
            yield return null;
        }
        throw new TimeoutException("The app's saved-locale bootstrap did not complete within 30 wall-clock seconds; SelectedLocale=" + CurrentLocaleCode());
    }
    static IEnumerator WaitForLocalizationInitialization()
    {
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        AsyncOperationHandle<LocalizationSettings> previousOperation = default;
        object previousLocale = null;
        int stableFrames = 0;
        string state = "not sampled";
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            var operation = LocalizationSettings.InitializationOperation;
            bool ready = operation.IsValid() && operation.IsDone;
            state = "current initialization:valid=" + operation.IsValid() + ",done=" + ready;
            if (ready && operation.Status == AsyncOperationStatus.Failed)
                throw new InvalidOperationException(state + ",status=" + operation.Status, operation.OperationException);
            ready &= operation.IsValid() && operation.Status == AsyncOperationStatus.Succeeded;
            if (ready)
            {
                var strings = LocalizationSettings.StringDatabase.PreloadOperation;
                var assets = LocalizationSettings.AssetDatabase.PreloadOperation;
                foreach (var preload in new[] { strings, assets })
                {
                    bool completed = preload.IsValid() && preload.IsDone;
                    state += "; preload:valid=" + preload.IsValid() + ",done=" + completed;
                    if (completed && preload.Status == AsyncOperationStatus.Failed)
                        throw new InvalidOperationException(state + ",status=" + preload.Status, preload.OperationException);
                    ready &= completed && preload.Status == AsyncOperationStatus.Succeeded;
                }
            }
            var locale = LocalizationSettings.SelectedLocale;
            stableFrames = ready && operation.Equals(previousOperation) && ReferenceEquals(locale, previousLocale)
                ? stableFrames + 1 : ready ? 1 : 0;
            previousOperation = operation; previousLocale = locale;
            if (stableFrames >= 3) yield break;
            yield return null;
        }
        throw new TimeoutException("Current locale initialization/preloads did not settle within 30 wall-clock seconds: " + state);
    }
    static IEnumerator WaitForFonts(Canvas canvas, string expectedLocale, string label, ScenarioEvidence evidence)
    {
        yield return WaitForLocalizationInitialization();
        // Fixed warm-up alone can capture a random opening rotation. Compare the
        // real settled presentation state without forcing its animation or pose.
        double motionDeadline = Time.realtimeSinceStartupAsDouble + 30;
        bool motionReady;
        do
        {
            motionReady = true;
            if (label == "settings" || label == "keybind")
                foreach (UIChainDropPresentation motion in canvas.GetComponentsInChildren<UIChainDropPresentation>())
                {
                    if (!motion.isActiveAndEnabled) continue;
                    var panel = typeof(UIChainDropPresentation).GetField("panelRoot", Fields).GetValue(motion) as RectTransform;
                    float rotation = (float)typeof(UIChainDropPresentation).GetField("openLocalZRotation", Fields).GetValue(motion);
                    if (panel != null && Quaternion.Angle(panel.localRotation, Quaternion.Euler(0f, 0f, rotation)) > .0001f)
                        motionReady = false;
                }
            if (!motionReady) yield return null;
        } while (!motionReady && Time.realtimeSinceStartupAsDouble < motionDeadline);
        if (!motionReady) throw new TimeoutException("Authored menu motion did not settle: " + label);
        double deadline = Time.realtimeSinceStartupAsDouble + 30;
        int matchingChecks = 0, activeBindings = 0;
        string previousState = null, reason = "waiting for package font callbacks";
        yield return null; // Allow the adapter's first layout LateUpdate after opening/switching locale.
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            bool ready = CurrentLocaleCode() == expectedLocale;
            string state = expectedLocale;
            activeBindings = 0;
            reason = ready ? "waiting for active font adapters" : "SelectedLocale=" + CurrentLocaleCode() + ", expected=" + expectedLocale;
            var bindings = canvas.GetComponentsInChildren<LocalizeTmpFontEvent>(true);
            Array.Sort(bindings, (a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
            foreach (LocalizeTmpFontEvent binding in bindings)
            {
                if (!binding.isActiveAndEnabled) continue;
                activeBindings++;
                TMP_Text text = binding.GetComponent<TMP_Text>();
                var reference = binding.AssetReference;
                if (text == null || reference == null) { ready = false; reason = binding.name + ": missing TMP target/asset reference"; break; }
                var operation = reference.CurrentLoadingOperationHandle;
                if (!operation.IsValid() || !operation.IsDone || operation.Status != AsyncOperationStatus.Succeeded ||
                    operation.Result == null || text.font != operation.Result)
                {
                    ready = false;
                    reason = binding.name + ": handle=" + (operation.IsValid() ? operation.Status.ToString() : "invalid") +
                        ", actualFont=" + (text.font != null ? text.font.name : "null") +
                        ", resultFont=" + (operation.IsValid() && operation.IsDone && operation.Result != null ? operation.Result.name : "pending/null");
                    break;
                }
                state += "|" + binding.GetInstanceID() + ":" + operation.GetHashCode() + ":" + text.font.GetInstanceID();
            }
            matchingChecks = ready ? state == previousState ? matchingChecks + 1 : 1 : 0;
            previousState = ready ? state : null;
            // Three matching checks span two completed frame/LateUpdate intervals.
            if (matchingChecks >= 3)
            {
                string result = label + ";locale=" + expectedLocale + ";activeBindings=" + activeBindings + ";stableLateUpdateFrames=2";
                evidence.settledFontGates.Add(result);
                Debug.Log("UIBATCH_FONTS_SETTLED," + result);
                yield break;
            }
            yield return null;
        }
        throw new TimeoutException("Font callback gate did not settle within 30 wall-clock seconds: " + label + ", " + reason);
    }
    static IEnumerator WaitSeconds(double seconds)
    { double end = Time.realtimeSinceStartupAsDouble + seconds; while (Time.realtimeSinceStartupAsDouble < end) yield return null; }
    static string Arg(string name, string fallback)
    { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return fallback; }
}
#endif
