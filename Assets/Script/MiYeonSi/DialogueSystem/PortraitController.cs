using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;
using DG.Tweening;

public class PortraitController : MonoBehaviour
{
    [Header("데이터 연동")]
    public NPCData npcData;

    [Header("설정")]
    [Tooltip("표정 스프라이트 카테고리 이름 (예: Face)")]
    public string targetCategory = "Face";

    [Header("이모티콘 설정")]
    [Tooltip("EmoteController가 붙어있는 말풍선 프리팹")]
    public GameObject emotePrefab;

    [Header("컴포넌트 연결")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private SpriteLibrary spriteLibrary;

    private RectTransform rectTransform;
    private Vector2 defaultPos;
    private Vector3 defaultScale;

    // [New] 현재 떠 있는 이모티콘을 기억하는 변수
    private GameObject currentEmoteObject;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (portraitImage == null) portraitImage = GetComponent<Image>();
        if (spriteLibrary == null) spriteLibrary = GetComponent<SpriteLibrary>();

        if (rectTransform != null)
        {
            defaultPos = rectTransform.anchoredPosition;
        }
        defaultScale = transform.localScale;
    }

    private void Start()
    {
        if (npcData != null)
        {
            ApplyNPCData(npcData);
        }
    }

    public void ApplyNPCData(NPCData data)
    {
        this.npcData = data;

        if (data.spriteLibraryAsset != null)
        {
            spriteLibrary.spriteLibraryAsset = data.spriteLibraryAsset;
            spriteLibrary.RefreshSpriteResolvers();
            SetExpression("Normal");
        }
        else
        {
            Debug.LogError($"[PortraitController] {data.npcName} 데이터에 Sprite Library Asset이 없습니다!");
        }
    }

    public void SetExpression(string label)
    {
        if (spriteLibrary == null || spriteLibrary.spriteLibraryAsset == null) return;

        Sprite newSprite = spriteLibrary.GetSprite(targetCategory, label);

        if (newSprite != null)
        {
            portraitImage.sprite = newSprite;
        }
    }

    // =================================================================
    // [Updated] 기능 2: 이모티콘 실행 (중복 방지 로직 추가)
    // =================================================================
    public void ShowEmote(string emoteName)
    {
        if (emotePrefab == null || npcData == null) return;

        // 1. [핵심] 이미 떠 있는 이모티콘이 있다면 즉시 파괴!
        if (currentEmoteObject != null)
        {
            Destroy(currentEmoteObject);
            currentEmoteObject = null;
        }

        // 2. 프리팹 생성
        GameObject go = Instantiate(emotePrefab, transform);

        // 3. [핵심] 방금 만든 것을 '현재 이모티콘'으로 등록
        currentEmoteObject = go;

        // 4. 위치 및 스케일 설정
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = npcData.emoteOffset;
            rt.localScale = Vector3.one;
        }

        // 5. 초기화
        EmoteController ctrl = go.GetComponent<EmoteController>();
        if (ctrl != null)
        {
            ctrl.Init(emoteName);
        }
    }

    public void PlayAction(string action)
    {
        if (rectTransform == null) return;

        rectTransform.DOKill();
        rectTransform.anchoredPosition = defaultPos;
        transform.localScale = defaultScale;
        transform.localRotation = Quaternion.identity;

        switch (action.ToLower())
        {
            case "jump":
                rectTransform.DOJumpAnchorPos(defaultPos, 50f, 1, 0.4f);
                break;
            case "shake":
                rectTransform.DOShakeAnchorPos(0.5f, 15f, 30, 90);
                break;
            case "zoom_in":
                transform.DOScale(defaultScale * 1.2f, 0.25f).SetEase(Ease.OutBack);
                break;
            case "nod":
                Sequence seq = DOTween.Sequence();
                seq.Append(rectTransform.DOAnchorPosY(defaultPos.y - 15f, 0.1f));
                seq.Append(rectTransform.DOAnchorPosY(defaultPos.y, 0.1f));
                break;
        }
    }

    public void SetInitialPosition(string positionKey)
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // 앵커 중앙 고정
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        float xPos = 0;
        switch (positionKey.ToLower())
        {
            case "left": xPos = -400f; break;
            case "right": xPos = 400f; break;
            case "center": xPos = 0f; break;
            case "far_left": xPos = -700f; break;
            case "far_right": xPos = 700f; break;
        }

        rectTransform.anchoredPosition = new Vector2(xPos, -200f);
        defaultPos = rectTransform.anchoredPosition;
    }

    public void EnterAnimation()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.DOFade(1f, 0.5f);

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -200f);
            rectTransform.DOAnchorPosY(0f, 0.5f).SetEase(Ease.OutBack)
                .OnComplete(() => defaultPos = rectTransform.anchoredPosition);
        }
    }

    public void ExitAnimationAndDestroy()
    {
        // [추가] 초상화가 사라질 때 이모티콘도 같이 정리 (혹시 몰라서 추가)
        if (currentEmoteObject != null) Destroy(currentEmoteObject);

        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.DOFade(0f, 0.4f);

        if (rectTransform != null)
        {
            rectTransform.DOAnchorPosY(-200f, 0.4f)
                .OnComplete(() => Destroy(gameObject));
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void MovePosition(string positionKey)
    {
        if (rectTransform == null) return;

        float targetX = 0;
        switch (positionKey.ToLower())
        {
            case "left": targetX = -400f; break;
            case "right": targetX = 400f; break;
            case "center": targetX = 0f; break;
            case "far_left": targetX = -700f; break;
            case "far_right": targetX = 700f; break;
        }
        rectTransform.DOAnchorPosX(targetX, 0.5f).SetEase(Ease.OutQuart)
            .OnComplete(() => defaultPos = rectTransform.anchoredPosition);
    }
}