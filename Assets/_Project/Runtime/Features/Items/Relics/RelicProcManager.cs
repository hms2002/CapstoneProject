using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>
/// 책임 :
/// - 장착된 유물 proc 집합을 관리하고, 공용 gameplay event relay를 통해 받은 이벤트를 각 proc에 분배한다.
/// - 유물 개별 구현이 ASC 이벤트 채널을 직접 구독하지 않게 만들어 proc 등록/해제와 이벤트 소비를 한 계층에 모은다.
/// </summary>
public class RelicProcManager : MonoBehaviour, IAbilityGameplayEventListener
{
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private AbilityGameplayEventRelay gameplayEventRelay;
    private readonly List<IRelicProc> procs = new();

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        for (int i = 0; i < procs.Count; i++)
            procs[i]?.Tick(deltaTime);
    }

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (gameplayEventRelay == null) gameplayEventRelay = GetComponent<AbilityGameplayEventRelay>();
        if (gameplayEventRelay == null && abilitySystem != null) gameplayEventRelay = gameObject.AddComponent<AbilityGameplayEventRelay>();
    }

    private void OnEnable()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (gameplayEventRelay == null) gameplayEventRelay = GetComponent<AbilityGameplayEventRelay>();
        if (gameplayEventRelay == null && abilitySystem != null) gameplayEventRelay = gameObject.AddComponent<AbilityGameplayEventRelay>();

        gameplayEventRelay?.Register(this);
    }

    private void OnDisable()
    {
        gameplayEventRelay?.Unregister(this);
    }

    public void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data)
    {
        for (int i = 0; i < procs.Count; i++)
            procs[i]?.Handle(tag, data);
    }

    public void Register(IRelicProc proc)
    {
        if (proc == null) return;
        procs.Add(proc);
    }

    public void UnregisterAll(Object token)
    {
        if (token == null) return;

        for (int i = procs.Count - 1; i >= 0; i--)
        {
            var p = procs[i];
            if (p == null) { procs.RemoveAt(i); continue; }

            if (p.Token == token)
            {
                p.Dispose();
                procs.RemoveAt(i);
            }
        }
    }
}
