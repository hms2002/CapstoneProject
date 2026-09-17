#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CapstoneAudio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Responsibility: verify scene-owned BGM requests, stale request rejection and lifecycle without real save/player bootstraps.
public sealed class SceneBgmRequestPlayModeTests
{
    private ISoundPlaybackBackend previousBackend;
    private IRunRouteBackend previousRouteBackend;
    private RecordingBackend backend;
    private Scene previousScene;
    private Scene first;
    private Scene second;

    [SetUp]
    public void SetUp()
    {
        previousBackend = (ISoundPlaybackBackend)typeof(SoundPlaybackUtility)
            .GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        previousRouteBackend = RunRoutePlayback.Backend;
        RunRoutePlayback.RegisterBackend(null);
        backend = new RecordingBackend();
        SoundPlaybackUtility.RegisterBackend(backend);
        previousScene = SceneManager.GetActiveScene();
        first = SceneManager.CreateScene("BgmRequest_First");
        second = SceneManager.CreateScene("BgmRequest_Second");
        SceneManager.SetActiveScene(first);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        SceneManager.SetActiveScene(previousScene);
        if (first.isLoaded) yield return SceneManager.UnloadSceneAsync(first);
        if (second.isLoaded) yield return SceneManager.UnloadSceneAsync(second);
        SoundPlaybackUtility.RegisterBackend(previousBackend);
        RunRoutePlayback.RegisterBackend(previousRouteBackend);
    }

    [Test]
    public void OnlyCurrentLoadedSceneCanPlayOrStopMusic()
    {
        SoundRef music = SoundRef.FromKey("bgm.hub");
        Assert.That(SoundPlaybackUtility.TryPlayMusic(music, first), Is.True);
        Assert.That(SoundPlaybackUtility.TryPlayMusic(music, second), Is.False);
        Assert.That(SoundPlaybackUtility.TryStopMusic(second), Is.False);
        Assert.That(SoundPlaybackUtility.TryPlayMusic(music, default), Is.False);
        Assert.That(SoundPlaybackUtility.TryStopMusic(default), Is.False);
        Assert.That(SoundPlaybackUtility.TryPlayMusic(default, first), Is.False);
        Assert.That(backend.Requests, Is.EqualTo(new[] { "bgm.hub" }));
        Assert.That(SoundPlaybackUtility.TryStopMusic(first), Is.True);
        Assert.That(backend.StopCount, Is.EqualTo(1));
    }

    [Test]
    public void MissingBackendDoesNotReportSuccess()
    {
        SoundPlaybackUtility.RegisterBackend(null);
        Assert.That(SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("bgm.hub"), first), Is.False);
        Assert.That(SoundPlaybackUtility.TryStopMusic(first), Is.False);
    }

    [UnityTest]
    public IEnumerator DeathReturn_RequestsHubWithoutAnyRouteOrPortal()
    {
        CreateRequester(first, "bgm.shadow_corridor");
        yield return null;
        Assert.That(backend.LastKey, Is.EqualTo("bgm.shadow_corridor"));
        Assert.That(SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("gameoverbgm"), first), Is.True);
        Assert.That(backend.LastKey, Is.EqualTo("gameoverbgm"));

        CreateRequester(second, "bgm.hub");
        yield return null; // Additive, non-active scenes must not replace GameOver music.
        Assert.That(backend.LastKey, Is.EqualTo("gameoverbgm"));
        SceneManager.SetActiveScene(second);
        Assert.That(backend.LastKey, Is.EqualTo("bgm.hub"));
        Assert.That(RunRoutePlayback.ActiveRouteCatalog, Is.Null);
        Assert.That(SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("gameoverbgm"), first), Is.False);
        Assert.That(SoundPlaybackUtility.TryStopMusic(first), Is.False);

        yield return SceneManager.UnloadSceneAsync(first);
        Assert.That(SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("gameoverbgm"), first), Is.False);
        Assert.That(backend.LastKey, Is.EqualTo("bgm.hub"));
        Assert.That(backend.StopCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator SceneEntry_DoesNotOverwriteLaterBossRequestOnFollowingFrames()
    {
        CreateRequester(first, "bgm.shadow_corridor");
        yield return null;
        SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("bgm.boss.shadow"), first);
        int count = backend.Requests.Count;
        yield return null;
        yield return null;
        Assert.That(backend.LastKey, Is.EqualTo("bgm.boss.shadow"));
        Assert.That(backend.Requests.Count, Is.EqualTo(count));
    }

    [UnityTest]
    public IEnumerator EmptySceneMusic_ExplicitlyStopsPreviousMusic()
    {
        SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("gameoverbgm"), first);
        CreateRequester(second, null);
        yield return null;
        Assert.That(backend.StopCount, Is.Zero);
        SceneManager.SetActiveScene(second);
        Assert.That(backend.StopCount, Is.EqualTo(1));
        Assert.That(backend.LastKey, Is.Null);
    }

    [UnityTest]
    public IEnumerator DisabledRequester_CannotStopOrReplaceNewMusic()
    {
        SceneBgmRequester requester = CreateRequester(first, "bgm.hub");
        yield return null;
        requester.enabled = false;
        Assert.That(requester.RequestMusic(), Is.False);
        SceneManager.SetActiveScene(second);
        SoundPlaybackUtility.TryPlayMusic(SoundRef.FromKey("titlescenebgm"), second);
        Object.Destroy(requester.gameObject);
        yield return null;
        Assert.That(backend.LastKey, Is.EqualTo("titlescenebgm"));
        Assert.That(backend.StopCount, Is.Zero);
    }

    [TestCase("bgm.hub")]
    [TestCase("bgm.shadow_corridor")]
    [TestCase("bgm.boss.shadow")]
    [TestCase("TitleSceneBGM")]
    [TestCase("GameOverBGM")]
    public void ConfiguredMusicKeysHavePlayableCatalogEntries(string key)
    {
        AudioCatalogSO catalog = Resources.Load<AudioCatalogSO>("Audio/DefaultAudioCatalog");
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.TryGetEntry(key, out AudioCatalogEntry entry), Is.True);
        Assert.That(entry.bus, Is.EqualTo(AudioBus.BGM));
        Assert.That(entry.HasPlayableClip, Is.True);
    }

    private static SceneBgmRequester CreateRequester(Scene scene, string key)
    {
        var root = new GameObject("SceneBgm");
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        SceneBgmRequester requester = root.AddComponent<SceneBgmRequester>();
        using (var serialized = new SerializedObject(requester))
        {
            SerializedProperty sound = serialized.FindProperty("sceneMusic");
            sound.FindPropertyRelative("key").stringValue = key ?? string.Empty;
            sound.FindPropertyRelative("volumeMultiplier").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        root.SetActive(true);
        return requester;
    }

    // Responsibility: record requests at the real Core audio boundary without audio-device or playback timing dependencies.
    private sealed class RecordingBackend : ISoundPlaybackBackend
    {
        public readonly List<string> Requests = new();
        public string LastKey;
        public int StopCount;
        public void PlayMusic(in SoundRef soundRef) { LastKey = soundRef.key; Requests.Add(soundRef.key); }
        public void StopMusic() { LastKey = null; StopCount++; }
        public AudioHandle Play(in SoundRef soundRef, in SoundPlaybackContext context) => AudioHandle.Invalid;
        public AudioHandle PlayTrackedOneShot(in SoundRef soundRef, in SoundPlaybackContext context) => AudioHandle.Invalid;
        public void DuckCombatSfx(float targetVolume, float fadeSeconds) { }
        public bool IsPlaying(AudioHandle handle) => false;
        public void Stop(AudioHandle handle, float fadeOutDuration = 0f) { }
        public void SetPitch(AudioHandle handle, float pitch) { }
    }
}
#endif
