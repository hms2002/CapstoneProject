using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.U2D.Animation;

public class PortraitController : MonoBehaviour
{
    public string targetCategory = "Face";

    [Header("프리팹 연결")]
    [SerializeField] private PortraitActor actorPrefab;
    [SerializeField] private GameObject emotePrefab;

    [Header("위치 오프셋")]
    [SerializeField] private float centerPosX = 0f;
    [SerializeField] private float sideOffset = 400f;
    [SerializeField] private float farSideOffset = 700f;
    [SerializeField] private float hidePosY = -600f;

    private List<PortraitActor> actorPool = new List<PortraitActor>();
    private List<GameObject> emotePool = new List<GameObject>();

    // 🚨 [핵심 개선] 파편화된 3개의 딕셔너리를 단 1개로 압축했습니다!
    private Dictionary<int, PortraitActor> activeActors = new Dictionary<int, PortraitActor>();

    private int currentSpeakerId = -1;

    private PortraitActor GetActor()
    {
        foreach (var actor in actorPool)
            if (!actor.gameObject.activeSelf) return actor;

        PortraitActor newActor = Instantiate(actorPrefab, transform);
        actorPool.Add(newActor);
        return newActor;
    }

    private PortraitActor GetOrCreateActiveActor(NPCData data)
    {
        if (activeActors.ContainsKey(data.id))
            return activeActors[data.id];

        PortraitActor newActor = GetActor();
        activeActors[data.id] = newActor;

        // 배우 본인에게 신분증(상태)을 쥐어줍니다!
        newActor.npcId = data.id;
        newActor.currentLabel = "Normal";
        newActor.currentPosition = "center";

        if (data.spriteLibraryAsset != null)
            newActor.SetSprite(data.spriteLibraryAsset.GetSprite(targetCategory, "Normal"));

        return newActor;
    }

    public void HighlightSpeaker(int speakerId)
    {
        currentSpeakerId = speakerId;
        foreach (var kvp in activeActors)
        {
            kvp.Value.SetFocus(kvp.Key == currentSpeakerId);
        }
    }

    public void SetInitialPosition(NPCData targetData, string positionKey)
    {
        if (targetData == null) return;
        PortraitActor actor = GetOrCreateActiveActor(targetData);

        actor.currentPosition = positionKey.ToLower(); // 배우가 자기 위치 갱신

        RectTransform rt = actor.GetComponent<RectTransform>();
        float targetX = GetTargetXByPositionKey(actor.currentPosition);
        rt.anchoredPosition = new Vector2(targetX, hidePosY);
    }

    public void EnterAnimation(NPCData targetData)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return;
        PortraitActor actor = activeActors[targetData.id];

        actor.gameObject.SetActive(true);
        actor.canvasGroup.alpha = 0f;
        actor.canvasGroup.DOFade(1f, 0.5f).SetUpdate(true);

        // [버그 수정 반영] RGB 0(까만색)에서 목표 색상까지 부드럽게 밝아지는 등장 연출!
        actor.image.color = new Color(0f, 0f, 0f, 1f);
        Color targetColor = (targetData.id == currentSpeakerId) ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
        actor.image.DOColor(targetColor, 0.5f).SetUpdate(true);

        RectTransform rt = actor.GetComponent<RectTransform>();
        rt.DOAnchorPosY(0f, 0.5f).SetUpdate(true).SetEase(Ease.OutBack);
    }

    public void DoCrossFade(NPCData targetData, string label, float appearTime, float fadeOutTime)
    {
        if (targetData == null || targetData.spriteLibraryAsset == null) return;
        int id = targetData.id;

        if (activeActors.ContainsKey(id))
        {
            PortraitActor currentActor = activeActors[id];
            // 배우가 직접 자신의 현재 표정을 확인합니다.
            if (currentActor.gameObject.activeSelf && currentActor.currentLabel == label)
                return;
        }

        PortraitActor nextActor = GetActor();
        nextActor.npcId = id;
        nextActor.currentLabel = label;

        Sprite faceSprite = targetData.spriteLibraryAsset.GetSprite(targetCategory, label);
        nextActor.SetSprite(faceSprite);
        nextActor.SetFocus(id == currentSpeakerId, 0f);

        // 이전 배우의 위치를 물려받아 저장
        string currentPosKey = activeActors.ContainsKey(id) ? activeActors[id].currentPosition : "center";
        nextActor.currentPosition = currentPosKey;

        float targetX = GetTargetXByPositionKey(currentPosKey);
        nextActor.GetComponent<RectTransform>().anchoredPosition = new Vector2(targetX, 0f);

        nextActor.transform.SetAsLastSibling();
        nextActor.FadeIn(appearTime);

        if (activeActors.ContainsKey(id))
        {
            PortraitActor prevActor = activeActors[id];
            if (prevActor != null && prevActor != nextActor)
                prevActor.FadeOut(fadeOutTime);
        }

        activeActors[id] = nextActor; // 딕셔너리 하나만 업데이트하면 끝!
    }

    public void ShowEmote(NPCData targetData, string emoteName)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return;
        PortraitActor actor = activeActors[targetData.id];

        GameObject targetEmote = null;
        foreach (var e in emotePool) { if (!e.activeSelf) { targetEmote = e; break; } }

        if (targetEmote == null)
        {
            targetEmote = Instantiate(emotePrefab, transform);
            emotePool.Add(targetEmote);
        }

        targetEmote.transform.SetParent(actor.transform, false);
        targetEmote.GetComponent<RectTransform>().anchoredPosition = targetData.emoteOffset;
        targetEmote.SetActive(true);
        targetEmote.GetComponent<EmoteController>()?.Init(emoteName);
    }

    public void MovePosition(NPCData targetData, string positionKey)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return;
        PortraitActor actor = activeActors[targetData.id];

        actor.currentPosition = positionKey.ToLower(); // 배우 상태 업데이트
        float targetX = GetTargetXByPositionKey(actor.currentPosition);

        RectTransform rt = actor.GetComponent<RectTransform>();
        rt.DOAnchorPosX(targetX, 0.5f).SetUpdate(true).SetEase(Ease.OutQuart);
    }

    public void PlayAction(NPCData targetData, string action)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return;
        PortraitActor actor = activeActors[targetData.id];
        RectTransform rt = actor.GetComponent<RectTransform>();
        rt.DOKill();

        // 딕셔너리 검색 없이 배우에게 바로 물어봅니다.
        Vector2 defaultPos = new Vector2(GetTargetXByPositionKey(actor.currentPosition), 0f);

        switch (action.ToLower())
        {
            case "jump": rt.DOJumpAnchorPos(defaultPos, 50f, 1, 0.4f).SetUpdate(true); break;
            case "shake": rt.DOShakeAnchorPos(0.5f, 15f, 30, 90).SetUpdate(true); break;
        }
    }

    public void ExitAnimationAndDestroy(NPCData targetData)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return;
        PortraitActor actor = activeActors[targetData.id];

        actor.FadeOut(0.4f);
        actor.GetComponent<RectTransform>().DOAnchorPosY(hidePosY, 0.4f).SetUpdate(true).OnComplete(() => {
            actor.HideImmediate();
        });

        activeActors.Remove(targetData.id); // 딕셔너리 하나만 지우면 메모리 싹 비워짐!
    }

    public void ExitAllAndClear()
    {
        foreach (var actor in actorPool) actor.FadeOut(0.4f);
        foreach (var emote in emotePool) if (emote != null) emote.SetActive(false);

        activeActors.Clear(); // 딕셔너리 하나만 지우면 끝!
        currentSpeakerId = -1;
    }

    public void SetupSilhouetteMode(NPCData targetData)
    {
        if (targetData == null) return;
        PortraitActor actor = GetOrCreateActiveActor(targetData);
        actor.image.color = Color.black;
        actor.canvasGroup.alpha = 0f;
        actor.gameObject.SetActive(true);
    }

    public Tween GetSilhouetteFadeInTween(NPCData targetData, float duration)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return null;
        return activeActors[targetData.id].canvasGroup.DOFade(1f, duration).SetUpdate(true);
    }

    public Tween GetColorizeTween(NPCData targetData, float duration)
    {
        if (targetData == null || !activeActors.ContainsKey(targetData.id)) return null;
        return activeActors[targetData.id].image.DOColor(Color.white, duration).SetUpdate(true);
    }

    private float GetTargetXByPositionKey(string key)
    {
        switch (key.ToLower())
        {
            case "left": return -sideOffset;
            case "right": return sideOffset;
            case "center": return centerPosX;
            case "far_left": return -farSideOffset;
            case "far_right": return farSideOffset;
            default: return centerPosX;
        }
    }
}
