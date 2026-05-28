using UnityEngine;
using System;
using System.Collections.Generic;
using DG.Tweening;

public class CinematicDirector : MonoBehaviour
{
    [Header("연결된 시스템")]
    [SerializeField] private PortraitController portraitController;

    public void SetPortraitController(PortraitController controller)
    {
        if (controller != null)
            portraitController = controller;
    }

    // [핵심 변경] 단일 NPCData가 아니라 참여자 명단(List<NPCData>)을 받습니다!
    public void PlayIntro(List<NPCData> participants, Action onComplete, string openingPortraitLabel = null)
    {
        ResolvePortraitController();

        if (participants == null || participants.Count == 0)
        {
            onComplete?.Invoke();
            return;
        }

        if (portraitController == null)
        {
            Debug.LogWarning("[CinematicDirector] PortraitController 참조가 없어 인트로 연출을 건너뜁니다.", this);
            onComplete?.Invoke();
            return;
        }

        // 1. 보스 연출 (첫 번째 인물이 보스일 경우 기존 연출 재생)
        if (participants[0].isBoss)
        {
            PlayBossIntroSequence(participants[0], onComplete, openingPortraitLabel);
            return;
        }

        // 2. 인원수에 따른 자동 배치 시스템!
        if (participants.Count == 1)
        {
            // 1명이면 중앙에!
            SetupAndEnter(participants[0], "center", openingPortraitLabel);
        }
        else if (participants.Count == 2)
        {
            // 2명이면 양쪽으로 사이좋게 등장!
            SetupAndEnter(participants[0], "left", openingPortraitLabel);
            SetupAndEnter(participants[1], "right");
        }
        else if (participants.Count >= 3)
        {
            // 3명이면 화면을 꽉 채워서!
            SetupAndEnter(participants[0], "far_left", openingPortraitLabel);
            SetupAndEnter(participants[1], "center");
            SetupAndEnter(participants[2], "far_right");
        }

        // 등장 애니메이션 시간(대략 0.5초) 대기 후 대화 시작 콜백
        DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke()).SetUpdate(true);
    }

    // 코드가 길어지는 것을 막기 위한 등장 헬퍼 함수
    private void SetupAndEnter(NPCData npcData, string position, string openingPortraitLabel = null)
    {
        portraitController.SetInitialPosition(npcData, position);
        portraitController.EnterAnimation(npcData);
        // 등장할 때는 기본 표정(Normal)으로 띄워줍니다.
        portraitController.DoCrossFade(npcData, ResolvePortraitLabel(openingPortraitLabel), 0f, 0f);
    }

    // =================================================================
    // 보스 연출 (이전과 동일)
    // =================================================================
    private void PlayBossIntroSequence(NPCData npcData, Action onComplete, string openingPortraitLabel)
    {
        if (portraitController == null)
        {
            onComplete?.Invoke();
            return;
        }
        string resolvedPortraitLabel = ResolvePortraitLabel(openingPortraitLabel);
        portraitController.SetupSilhouetteMode(npcData, resolvedPortraitLabel);
        portraitController.SetInitialPosition(npcData, "center");
        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        Tween fadeIn = portraitController.GetSilhouetteFadeInTween(npcData, 1.0f);
        if (fadeIn != null) seq.Append(fadeIn);
        seq.AppendInterval(0.5f);
        Tween colorize = portraitController.GetColorizeTween(npcData, 1.0f);
        if (colorize != null) seq.Append(colorize);
        seq.OnComplete(() => {
            portraitController.DoCrossFade(npcData, resolvedPortraitLabel, 0.5f, 0.5f);
            onComplete?.Invoke();
        });
    }

    public void PlayFastSilhouetteIntro(
        NPCData npcData,
        string position,
        float fadeSeconds,
        bool colorize,
        string openingPortraitLabel,
        Action onComplete)
    {
        ResolvePortraitController();

        if (npcData == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (portraitController == null)
        {
            Debug.LogWarning("[CinematicDirector] PortraitController reference is missing. Fast silhouette intro skipped.", this);
            onComplete?.Invoke();
            return;
        }

        string resolvedPortraitLabel = ResolvePortraitLabel(openingPortraitLabel);
        portraitController.SetupSilhouetteMode(npcData, resolvedPortraitLabel);
        portraitController.SetInitialPosition(npcData, ResolvePositionKey(position));
        portraitController.SnapActiveActorToShownPosition(npcData);

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);
        bool hasTween = false;

        Tween fadeIn = portraitController.GetSilhouetteFadeInTween(npcData, Mathf.Max(0f, fadeSeconds));
        if (fadeIn != null)
        {
            seq.Append(fadeIn);
            hasTween = true;
        }

        if (colorize)
        {
            Tween colorizeTween = portraitController.GetColorizeTween(npcData, Mathf.Max(0f, fadeSeconds));
            if (colorizeTween != null)
            {
                seq.Append(colorizeTween);
                hasTween = true;
            }
        }

        if (!hasTween)
        {
            seq.Kill();
            onComplete?.Invoke();
            return;
        }

        seq.OnComplete(() => onComplete?.Invoke());
    }

    // =================================================================
    // 종료 연출 (이전과 동일)
    // =================================================================
    public void PlayOutro(Action onComplete)
    {
        ResolvePortraitController();

        if (portraitController != null) portraitController.ExitAllAndClear();
        DOVirtual.DelayedCall(0.5f, () => onComplete?.Invoke()).SetUpdate(true);
    }

    private void ResolvePortraitController()
    {
        if (portraitController != null)
            return;

        Canvas dialogueCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Dialogue);
        if (dialogueCanvas == null)
            return;

        portraitController = dialogueCanvas.GetComponentInChildren<PortraitController>(true);
    }

    private static string ResolvePositionKey(string position)
    {
        return string.IsNullOrWhiteSpace(position) ? "center" : position.Trim();
    }

    private static string ResolvePortraitLabel(string label)
    {
        return string.IsNullOrWhiteSpace(label) ? "Normal" : label.Trim();
    }
}
