using UnityEngine;

// 호감도 레벨 도달 시 발생할 효과의 부모 클래스입니다.
public abstract class AffectionEffect : ScriptableObject
{
    [SerializeField] public string effectDescription; // 유저에게 보여줄 설명
    public abstract void Execute(); // 실제 효과 로직
}