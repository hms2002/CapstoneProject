using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class CinematicDirector : MonoBehaviour
{
    [Header("연결된 시스템")]
    [SerializeField] private PortraitController portraitController;

    // [핵심 변경] 단일 NPCData가 아니라 참여자 명단(List<NPCData>)을 받습니다!
    public void PlayIntro(List<NPCData> participants, Action onComplete)
    {
        if (participants == null || participants.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        // 1. 보스 연출 (첫 번째 인물이 보스일 경우 기존 연출 재생)
        if (participants[0].isBoss)
        {
            PlayBossIntroSequence(participants[0], onComplete);
            return;
        }

        // 2. 인원수에 따른 자동 배치 시스템!
        if (participants.Count == 1)
        {
            // 1명이면 중앙에!
            SetupAndEnter(participants[0], "center");
        }
        else if (participants.Count == 2)
        {
            // 2명이면 양쪽으로 사이좋게 등장!
            SetupAndEnter(participants[0], "left");
            SetupAndEnter(participants[1], "right");
        }
        else if (participants.Count >= 3)
        {
            // 3명이면 화면을 꽉 채워서!
            SetupAndEnter(participants[0], "far_left");
            SetupAndEnter(participants[1], "center");
            SetupAndEnter(participants[2], "far_right");
        }

        // 등장 애니메이션 시간(대략 0.5초) 대기 후 대화 시작 콜백
        DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke());
    }

    // 코드가 길어지는 것을 막기 위한 등장 헬퍼 함수
    private void SetupAndEnter(NPCData npcData, string position)
    {
        portraitController.SetInitialPosition(npcData, position);
        portraitController.EnterAnimation(npcData);
        // 등장할 때는 기본 표정(Normal)으로 띄워줍니다.
        portraitController.DoCrossFade(npcData, "Normal", 0f, 0f);
    }

    // =================================================================
    // 보스 연출 (이전과 동일)
    // =================================================================
    private void PlayBossIntroSequence(NPCData npcData, Action onComplete)
    {
        if (portraitController == null) return;
        portraitController.SetupSilhouetteMode(npcData);
        portraitController.SetInitialPosition(npcData, "center");
        Sequence seq = DOTween.Sequence();
        Tween fadeIn = portraitController.GetSilhouetteFadeInTween(npcData, 1.0f);
        if (fadeIn != null) seq.Append(fadeIn);
        seq.AppendInterval(0.5f);
        Tween colorize = portraitController.GetColorizeTween(npcData, 1.0f);
        if (colorize != null) seq.Append(colorize);
        seq.OnComplete(() => {
            portraitController.DoCrossFade(npcData, "Normal", 0.5f, 0.5f);
            onComplete?.Invoke();
        });
    }

    // =================================================================
    // 종료 연출 (이전과 동일)
    // =================================================================
    public void PlayOutro(Action onComplete)
    {
        if (portraitController != null) portraitController.ExitAllAndClear();
        DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke());
    }
}