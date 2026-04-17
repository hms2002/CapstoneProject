using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 이 클래스의 책임:
/// 촛대 봉인 스택을 월드 공간 pip 표식으로 렌더링하고, 봉인 해제 시 재점화 사운드를 재생하는 기본 프레젠테이션 구현체가 된다.
/// 인터페이스 기반 계약을 구현해 이후 숫자형/룬형/셰이더형 연출로 쉽게 교체할 수 있는 기본값을 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CandlestickSealPipPresentation : MonoBehaviour, ICandlestickSealPresentation
{
    [Header("Pip")]
    [SerializeField] private List<SpriteRenderer> pipRenderers = new();

    [Header("Audio")]
    [SerializeField] private SoundRef reigniteSound;

    private readonly List<SpriteRenderer> marks = new();

    private void Awake()
    {
        ResolveConfiguredPipRenderers();
        HideSeal();
    }

    private void OnEnable()
    {
        HideSeal();
    }

    public void ShowSeal(int currentStacks, int maxStacks)
    {
        EnsureMarks(maxStacks);
        SetVisibleCount(currentStacks);
    }

    public void UpdateSealStacks(int currentStacks, int maxStacks)
    {
        EnsureMarks(maxStacks);
        SetVisibleCount(currentStacks);
    }

    public void PlaySealBroken()
    {
        HideSeal();
        SoundPlaybackUtility.Play(
            reigniteSound,
            instigator: gameObject,
            causer: gameObject,
            target: null,
            position: transform.position,
            sourceObject: this);
    }

    public void HideSeal()
    {
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i] != null)
                marks[i].gameObject.SetActive(false);
        }
    }

    private void EnsureMarks(int maxStacks)
    {
        ResolveConfiguredPipRenderers();
    }

    private void SetVisibleCount(int currentStacks)
    {
        int visibleCount = Mathf.Max(0, currentStacks);
        for (int i = 0; i < marks.Count; i++)
        {
            if (marks[i] != null)
                marks[i].gameObject.SetActive(i < visibleCount);
        }
    }

    private void ResolveConfiguredPipRenderers()
    {
        marks.Clear();
        if (pipRenderers == null)
            return;

        for (int i = 0; i < pipRenderers.Count; i++)
        {
            SpriteRenderer renderer = pipRenderers[i];
            if (renderer != null)
                marks.Add(renderer);
        }
    }
}
