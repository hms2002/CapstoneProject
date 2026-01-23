using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D.Animation;
using DG.Tweening;

public class PortraitController : MonoBehaviour
{
    [Header("데이터 연동")]
    public NPCData npcData;

    [Header("설정")]
    [Tooltip("스프라이트 라이브러리에서 사용할 카테고리 이름 (예: Face)")]
    public string targetCategory = "Face";

    [Header("컴포넌트 연결")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private SpriteLibrary spriteLibrary;

    private RectTransform rectTransform;
    private Vector2 defaultPos;
    private Vector3 defaultScale;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        // [수정] 컴포넌트가 비어있으면 찾되, 없으면 에러 로그 출력
        if (portraitImage == null) portraitImage = GetComponent<Image>();
        if (portraitImage == null) Debug.LogError($"[PortraitController] {name} 오브젝트에 Image 컴포넌트가 없습니다!");

        if (spriteLibrary == null) spriteLibrary = GetComponent<SpriteLibrary>();

        if (rectTransform != null) defaultPos = rectTransform.anchoredPosition;
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
            // 1. 라이브러리 교체
            spriteLibrary.spriteLibraryAsset = data.spriteLibraryAsset;

            // 2. 표정 설정 (기본값)
            // 주의: 라이브러리에 "Normal"이라는 라벨이 반드시 있어야 함
            SetExpression("Normal");
        }
        else
        {
            Debug.LogError($"[PortraitController] {data.npcName}의 NPCData에 Sprite Library Asset이 비어있습니다!");
        }
    }

    // 기능 1: 표정 변경 (# 1001 : face : smile)
    public void SetExpression(string label)
    {
        if (spriteLibrary == null || spriteLibrary.spriteLibraryAsset == null)
        {
            Debug.LogError("[PortraitController] SpriteLibrary가 연결되지 않았습니다.");
            return;
        }

        // [디버깅] 무엇을 찾으려고 하는지 로그로 확인
        // Debug.Log($"[PortraitController] 표정 찾기 시도 -> Category: {targetCategory}, Label: {label}");

        Sprite newSprite = spriteLibrary.GetSprite(targetCategory, label);

        if (newSprite != null)
        {
            portraitImage.sprite = newSprite;
        }
        else
        {
            // [중요] 여기가 실행된다면 라이브러리 설정 문제임
            Debug.LogWarning($"[PortraitController] 스프라이트를 찾을 수 없습니다! \n" +
                             $"대상: {spriteLibrary.spriteLibraryAsset.name} / " +
                             $"카테고리: '{targetCategory}' / 라벨: '{label}'");
        }
    }

    // 기능 2: 움직임 연출 (# 1001 : act : jump)
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

    // [New] 등장 위치 설정
    public void SetInitialPosition(string positionKey)
    {
        if (rectTransform == null) return;

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
        defaultPos = rectTransform.anchoredPosition; // 이동 후 디폴트 위치 갱신
    }

    // [New] 등장 연출
    public void EnterAnimation()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();

        cg.alpha = 0f;
        cg.DOFade(1f, 0.5f);
        rectTransform.DOAnchorPosY(0f, 0.5f).SetEase(Ease.OutBack)
            .OnComplete(() => defaultPos = rectTransform.anchoredPosition); // 연출 끝난 위치를 기준점으로
    }

    // [New] 퇴장 연출
    public void ExitAnimationAndDestroy()
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.DOFade(0f, 0.4f);

        rectTransform.DOAnchorPosY(-200f, 0.4f)
            .OnComplete(() => Destroy(gameObject));
    }

    // [New] 이동 연출 (# 1001 : move : center)
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