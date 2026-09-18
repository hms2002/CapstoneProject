#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>Verifies room introduction completion, interruption, and profile isolation without real save writes.</summary>
public sealed class NpcRoomIntroductionPlayModeTests
{
    private readonly List<GameObject> objects = new();
    private IntroductionTestStore store;
    private NpcRoomIntroduction introduction;
    private IntroductionTestSource source;
    private PlayerInteractor2D player;

    [SetUp]
    public void SetUp()
    {
        store = new IntroductionTestStore();
        GameDataStore.RegisterBackend(store);
        GameObject playerObject = Create("Player");
        playerObject.AddComponent<CapsuleCollider2D>();
        var sensor = new GameObject("InteractionSensor");
        sensor.transform.SetParent(playerObject.transform);
        sensor.AddComponent<CapsuleCollider2D>();
        sensor.AddComponent<PlayerInteractionSensor2D>();
        player = playerObject.AddComponent<PlayerInteractor2D>();
        player.enabled = false;
        source = Create("NPC").AddComponent<IntroductionTestSource>();
        introduction = Create("Room").AddComponent<NpcRoomIntroduction>();
        introduction.Configure(new[] { source.gameObject });
    }

    private GameObject Create(string name)
    {
        var result = new GameObject(name);
        objects.Add(result);
        return result;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        GameDataStore.UnregisterBackend(store);
    }

    [UnityTest]
    public IEnumerator CompletePersistsOnceAndRevisitSkips()
    {
        introduction.NotifyEntered(player);
        yield return null; yield return null;
        Assert.AreEqual(1, source.Starts);
        source.End(true);
        yield return null; yield return null;
        Assert.AreEqual(1, store.Saves);
        Assert.Contains(source.IntroductionKey, store.Data.completedNpcRoomIntroductions);
        GameData restored = JsonUtility.FromJson<GameData>(JsonUtility.ToJson(store.Data));
        Assert.Contains(source.IntroductionKey, restored.completedNpcRoomIntroductions);
        introduction.NotifyExited();
        introduction.NotifyEntered(player);
        yield return null; yield return null;
        Assert.AreEqual(1, source.Starts);
    }

    [UnityTest]
    public IEnumerator InterruptedDialogueCanRetry()
    {
        introduction.NotifyEntered(player);
        yield return null; yield return null;
        source.End(false);
        yield return null; yield return null;
        Assert.AreEqual(0, store.Saves);
        introduction.NotifyEntered(player);
        yield return null; yield return null;
        Assert.AreEqual(2, source.Starts);
    }

    [UnityTest]
    public IEnumerator LeavingBeforeFocusDoesNotStart()
    {
        introduction.NotifyEntered(player);
        introduction.NotifyExited();
        yield return null; yield return null;
        Assert.AreEqual(0, source.Starts);
    }

    [UnityTest]
    public IEnumerator DisableIgnoresLateCompletion()
    {
        introduction.NotifyEntered(player);
        yield return null; yield return null;
        introduction.enabled = false;
        source.End(true);
        yield return null;
        Assert.AreEqual(0, store.Saves);
    }

    [UnityTest]
    public IEnumerator ChangedProfileDoesNotReceivePreviousCompletion()
    {
        introduction.NotifyEntered(player);
        yield return null; yield return null;
        store.Data = new GameData();
        source.End(true);
        yield return null; yield return null;
        Assert.AreEqual(0, store.Saves);
        Assert.IsEmpty(store.Data.completedNpcRoomIntroductions);
    }

    /// <summary>Captures save requests in memory instead of touching a user's profile.</summary>
    private sealed class IntroductionTestStore : IGameDataStoreBackend
    {
        public GameData Data { get; set; } = new GameData();
        public int ActiveSlotIndex => 0;
        public int Saves;
        public event Action<GameData, int> OnDataLoaded { add { } remove { } }
        public GameData EnsureData() => Data;
        public void SaveData() => Saves++;
        public void RequestImmediateSave(Object requester) => Saves++;
        public void RequestDeferredSave(Object requester) => Saves++;
        public void FlushSave(Object requester) { }
    }
}

/// <summary>Models an existing NPC interaction whose completion is explicitly controlled by a test.</summary>
public sealed class IntroductionTestSource : MonoBehaviour, INpcRoomIntroductionSource, IInteractable
{
    public int Starts;
    private Action<bool> ended;
    public string IntroductionKey => "test:npc";
    public bool HandlesIntroductionCamera => true;
    public bool TryStartIntroduction(IPlayerInteractor player, Action<bool> onEnded)
    {
        Starts++;
        ended = onEnded;
        return true;
    }
    public void End(bool completed) => ended?.Invoke(completed);
    public bool CanInteract(IPlayerInteractor player) => true;
    public void OnPlayerNearby() { }
    public void OnPlayerLeave() { }
    public void GetInteract(string value) { }
    public void OnHighlight() { }
    public void OnUnHighlight() { }
    public void OnPlayerInteract(IPlayerInteractor player) { }
    public InteractState GetInteractType() => InteractState.Talking;
    public string GetInteractDescription() => "Talk";
    public Transform GetPromptAnchor() => transform;
}
#endif
