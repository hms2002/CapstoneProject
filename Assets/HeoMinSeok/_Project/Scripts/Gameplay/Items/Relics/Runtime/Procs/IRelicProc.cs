using UnityEngine;
using UnityGAS;

public interface IRelicProc
{
    Object Token { get; }
    void Handle(GameplayTag tag, AbilityEventData data);
    void Dispose();
}
