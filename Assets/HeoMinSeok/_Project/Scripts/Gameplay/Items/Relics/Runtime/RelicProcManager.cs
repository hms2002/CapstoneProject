using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public class RelicProcManager : MonoBehaviour
{
    [SerializeField] private AbilitySystem abilitySystem;
    private readonly List<IRelicProc> procs = new();

    private void Awake()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
    }

    private void OnEnable()
    {
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (abilitySystem != null) abilitySystem.OnGameplayEvent += Dispatch;
    }

    private void OnDisable()
    {
        if (abilitySystem != null) abilitySystem.OnGameplayEvent -= Dispatch;
    }

    private void Dispatch(GameplayTag tag, AbilityEventData data)
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
