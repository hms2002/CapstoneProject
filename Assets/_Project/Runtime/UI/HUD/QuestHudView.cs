using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Projects quest owners into authored HUD rows; never owns quest progress.</summary>
public sealed class QuestHudView : MonoBehaviour, IDefaultHudVisibilityTarget
{
    [SerializeField] private TMP_Text heading;
    [SerializeField] private RectTransform mainGroup;
    [SerializeField] private TMP_Text mainDescription;
    [SerializeField] private TMP_Text subHeading;
    [SerializeField] private RunRouteCatalogSO mainQuestRoutes;
    [SerializeField] private TMP_Text officerDescription;
    private Tween officerMotion;
    private readonly List<Tween> officerLayoutMotions = new();
    private Vector2 officerRestPosition;
    private Vector2 descriptionRestPosition;
    private Vector2 subHeadingRestPosition;
    private Vector2 rowsRestPosition;
    private bool officerVisible;
    private bool officerLeaving;
    private Tween mainMotion;
    private Vector2 mainRestPosition;
    private bool mainVisible;
    private bool lastRunActive;
    private string lastMainText;

    private void Awake()
    {
        if (mainGroup != null) mainRestPosition = mainGroup.anchoredPosition;
        if (officerDescription != null) officerRestPosition = officerDescription.rectTransform.anchoredPosition;
        if (mainDescription != null) descriptionRestPosition = mainDescription.rectTransform.anchoredPosition;
        if (subHeading != null) subHeadingRestPosition = subHeading.rectTransform.anchoredPosition;
        if (rowsRoot != null) rowsRestPosition = rowsRoot.anchoredPosition;
    }

    private void Update()
    {
        RefreshMainQuest();
        if (lastRunActive != RunSessionStore.IsRunActive)
        {
            lastRunActive = RunSessionStore.IsRunActive;
            RefreshParcel();
        }
    }

    private void RefreshMainQuest()
    {
        int defeated = RunOfficerQuestProgress.CountDefeated(RunSessionStore.Data);
        RefreshOfficerQuest(defeated);
        string text = ResolveMainQuestText(SceneManager.GetActiveScene().name, defeated, mainQuestRoutes);
        bool visible = !string.IsNullOrEmpty(text);
        if (lastMainText != text && mainDescription != null) mainDescription.text = text;
        lastMainText = text;
        if (mainVisible == visible) return;
        mainVisible = visible;
        mainMotion?.Kill();
        if (mainGroup == null) return;
        mainGroup.gameObject.SetActive(visible);
        mainGroup.anchoredPosition = mainRestPosition;
        if (visible)
        {
            mainGroup.anchoredPosition = new Vector2(OffscreenX(mainGroup), mainRestPosition.y);
            mainMotion = mainGroup.DOAnchorPos(mainRestPosition, enterSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    private void RefreshOfficerQuest(int defeated)
    {
        if (officerDescription == null) return;
        GamePlayData data = RunSessionStore.Data;
        string scene = SceneManager.GetActiveScene().name;
        bool visible = RunOfficerQuestProgress.IsVisible(data);
        if (officerLeaving && (!RunOfficerQuestProgress.CanPresentCompletion(data, scene) || SceneTransitionPlayback.IsTransitionActive))
        {
            officerMotion?.Kill();
            officerLeaving = false;
            officerDescription.rectTransform.anchoredPosition = officerRestPosition;
        }
        if (officerVisible != visible)
        {
            officerVisible = visible;
            officerDescription.gameObject.SetActive(visible);
            officerDescription.rectTransform.anchoredPosition = officerRestPosition;
            SetOfficerLayout(visible, false);
        }
        if (!visible) return;
        string text = $"마왕성 간부를 찾아 토벌하자 ({defeated}/3)" + (defeated == 3 ? " · 완료" : "");
        if (officerDescription.text != text) officerDescription.text = text;
        if (officerLeaving || !RunOfficerQuestProgress.CanPresentCompletion(data, scene) ||
            SceneTransitionPlayback.IsTransitionActive || (mainMotion != null && mainMotion.IsActive() && mainMotion.IsPlaying())) return;

        officerLeaving = true;
        RectTransform rect = officerDescription.rectTransform;
        officerMotion = DOTween.Sequence().SetUpdate(true)
            .AppendInterval(0.35f)
            .Append(rect.DOAnchorPosY(officerRestPosition.y + 12f, 0.12f).SetEase(Ease.OutQuad))
            .Append(rect.DOAnchorPosX(OffscreenX(rect), exitSeconds).SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                officerLeaving = false;
                if (!ReferenceEquals(data, RunSessionStore.Data) ||
                    !RunOfficerQuestProgress.CanPresentCompletion(data, SceneManager.GetActiveScene().name)) return;
                RunOfficerQuestProgress.CompletePresentation(data, SceneManager.GetActiveScene().name);
                officerVisible = false;
                officerDescription.gameObject.SetActive(false);
                rect.anchoredPosition = officerRestPosition;
                SetOfficerLayout(false, true);
            });
    }

    private void SetOfficerLayout(bool visible, bool animate)
    {
        foreach (Tween motion in officerLayoutMotions) motion?.Kill();
        officerLayoutMotions.Clear();
        float offset = visible ? officerDescription.rectTransform.rect.height + rowSpacing : 0f;
        Move(mainDescription != null ? mainDescription.rectTransform : null, descriptionRestPosition);
        Move(subHeading != null ? subHeading.rectTransform : null, subHeadingRestPosition);
        Move(rowsRoot, rowsRestPosition);
        void Move(RectTransform rect, Vector2 rest)
        {
            if (rect == null) return;
            Vector2 destination = rest + Vector2.down * offset;
            if (animate) officerLayoutMotions.Add(rect.DOAnchorPos(destination, reflowSeconds).SetEase(Ease.OutCubic).SetUpdate(true));
            else rect.anchoredPosition = destination;
        }
    }

    public static string ResolveMainQuestText(string sceneName, int defeatedNormalBosses, RunRouteCatalogSO routes)
    {
        if (sceneName == "DarkLord_Tutorial") return "마왕을 토벌하기 위해 전진하자.";
        if (SceneDomainNamePolicy.IsHubSceneName(sceneName))
            return "마왕성 공략 준비를 마치고 위 쪽의 포탈로 이동하자.";
        if (sceneName == "Grand Hall")
        {
            if (defeatedNormalBosses >= 3) return "마왕을 토벌하기 위해 포탈로 이동하자.";
            if (defeatedNormalBosses == 2) return "마지막 포탈로 이동하자.";
            if (defeatedNormalBosses == 1) return "둘 중 하나의 포탈을 선택해 이동하자.";
            return "셋 중 하나의 포탈을 선택해 이동하자.";
        }
        CorridorBossRouteSetSO final = routes != null ? routes.FinalRouteSet : null;
        if (sceneName == "LeeJunmo_Boss_DemonKing" || (final != null && sceneName == final.BossSceneName))
            return "마왕을 토벌하자.";
        if (sceneName == "ProceduralDemonkingCorridor" || sceneName == "DemonkingCorridor" ||
            (final != null && sceneName == final.CorridorSceneName))
            return "마왕을 토벌하기 위해 포탈로 이동하자.";
        if (routes != null)
            foreach (CorridorBossRouteSetSO route in routes.NormalRouteSets)
            {
                if (route == null) continue;
                if (sceneName == route.BossSceneName) return "간부를 토벌하자.";
                if (sceneName == route.CorridorSceneName) return "포탈을 찾아 간부를 토벌하자.";
            }
        return null;
    }
    [SerializeField] private RectTransform rowsRoot;
    [SerializeField] private QuestHudRowView rowTemplate;
    [SerializeField, Min(0f)] private float rowSpacing = 12f;
    [SerializeField, Min(0.01f)] private float enterSeconds = 0.35f;
    [SerializeField, Min(0.01f)] private float exitSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float reflowSeconds = 0.25f;
    private readonly List<QuestHudRowView> rows = new();
    private RelicInventory inventory;
    private const string ParcelId = "parcel_delivery";

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered += UnbindPlayer;
        if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        if (subHeading != null) subHeading.gameObject.SetActive(false);
        officerVisible = false;
        if (officerDescription != null) officerDescription.gameObject.SetActive(false);
        lastRunActive = RunSessionStore.IsRunActive;
        BindPlayer(PlayerRuntimeRegistry.CurrentPlayer);
        RefreshMainQuest();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= BindPlayer;
        PlayerRuntimeRegistry.PlayerUnregistered -= UnbindPlayer;
        if (inventory != null) inventory.OnChanged -= RefreshParcel;
        inventory = null;
        foreach (QuestHudRowView row in rows)
        {
            row.KillMotion();
            Destroy(row.gameObject);
        }
        rows.Clear();
        officerMotion?.Kill();
        officerMotion = null;
        officerLeaving = false;
        officerVisible = false;
        if (officerDescription != null)
        {
            officerDescription.gameObject.SetActive(false);
            officerDescription.rectTransform.anchoredPosition = officerRestPosition;
            SetOfficerLayout(false, false);
        }
        mainMotion?.Kill();
        mainMotion = null;
        mainVisible = false;
        lastMainText = null;
        if (mainGroup != null)
        {
            mainGroup.anchoredPosition = mainRestPosition;
            mainGroup.gameObject.SetActive(false);
        }
        if (subHeading != null) subHeading.gameObject.SetActive(false);
    }

    private void BindPlayer(PlayerInteractor2D player)
    {
        if (inventory != null) inventory.OnChanged -= RefreshParcel;
        inventory = player != null ? player.GetComponent<RelicInventory>() : null;
        if (inventory != null) inventory.OnChanged += RefreshParcel;
        RefreshParcel();
    }

    private void UnbindPlayer(PlayerInteractor2D player) => BindPlayer(null);

    private void RefreshParcel()
    {
        if (RunSessionStore.IsRunActive && inventory != null && inventory.CountRelicsOfType<ParcelRelicDefinition>() > 0)
            ShowQuest(ParcelId, "파셀의 소포 배달", "다음 층으로 소포를 배달하세요!");
        else
            RemoveQuest(ParcelId);
    }

    public void ShowQuest(string questId, string title, string description)
    {
        if (!isActiveAndEnabled || rowTemplate == null || rowsRoot == null) return;
        QuestHudRowView row = rows.Find(candidate => candidate.QuestId == questId);
        if (row != null)
        {
            row.SetText(questId, title, description);
            if (!row.IsLeaving) return;
            row.KillMotion();
            row.IsLeaving = false;
            Reflow();
            return;
        }
        row = Instantiate(rowTemplate, rowsRoot);
        row.SetText(questId, title, description);
        row.gameObject.SetActive(true);
        rows.Add(row);
        Vector2 target = PositionFor(rows.Count - 1);
        row.Rect.anchoredPosition = target;
        float left = OffscreenX(row.Rect);
        row.Rect.anchoredPosition = new Vector2(left, target.y);
        row.Motion = row.Rect.DOAnchorPos(target, enterSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
        if (subHeading != null) subHeading.gameObject.SetActive(true);
    }

    public void RemoveQuest(string questId)
    {
        QuestHudRowView row = rows.Find(candidate => candidate.QuestId == questId);
        if (row == null || row.IsLeaving) return;
        row.KillMotion();
        row.IsLeaving = true;
        float raisedY = row.Rect.anchoredPosition.y + 12f;
        float left = OffscreenX(row.Rect);
        row.Motion = DOTween.Sequence().SetUpdate(true)
            .Append(row.Rect.DOAnchorPosY(raisedY, 0.12f).SetEase(Ease.OutQuad))
            .Append(row.Rect.DOAnchorPosX(left, exitSeconds).SetEase(Ease.InCubic))
            .OnComplete(() =>
            {
                rows.Remove(row);
                Destroy(row.gameObject);
                Reflow();
                if (subHeading != null) subHeading.gameObject.SetActive(rows.Count > 0);
            });
    }

    private Vector2 PositionFor(int index) => new(0f, -index * (rowTemplate.Rect.rect.height + rowSpacing));

    private float OffscreenX(RectTransform rect)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        float screenX = RectTransformUtility.WorldToScreenPoint(camera, rect.position).x;
        float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
        return rect.anchoredPosition.x - screenX / scale - rect.rect.width - 40f;
    }

    private void Reflow()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            QuestHudRowView row = rows[i];
            if (row.IsLeaving) continue;
            row.KillMotion();
            row.Motion = row.Rect.DOAnchorPos(PositionFor(i), reflowSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}
