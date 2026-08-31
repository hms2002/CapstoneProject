using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 이동 시작 전에 플레이어에게 적용할 로컬 출발 연출 방식을 정의한다.
/// </summary>
public enum SceneTravelDepartureMode
{
    None = 0,
    PullIntoEndpoint = 1
}

/// <summary>
/// 책임 : 씬 로드 전후 화면을 덮고 해제하는 전환 시각 방식을 정의한다.
/// </summary>
public enum SceneTransitionVisualMode
{
    AlphaFade = 0,
    HorizontalWipeRightToLeft = 1
}

/// <summary>
/// 책임 : 목적 endpoint에 배치된 플레이어가 조작을 되찾기 전에 재생할 도착 연출 방식을 정의한다.
/// </summary>
public enum SceneTravelArrivalMode
{
    None = 0,
    MoveFromOffset = 1
}

/// <summary>
/// 책임 : 한 연결 방향에서 재사용할 출발·화면 전환·도착 연출 파라미터를 데이터로 제공한다.
/// </summary>
[CreateAssetMenu(
    fileName = "SceneTravelPresentationProfile",
    menuName = "Capstone/Scene Management/Travel Presentation Profile")]
public sealed class SceneTravelPresentationProfileSO : ScriptableObject
{
    [Header("Departure")]
    [SerializeField] private SceneTravelDepartureMode departureMode;
    [SerializeField, Min(0f)] private float departureDuration = 0.55f;
    [SerializeField] private float departureRotationDegrees = 720f;
    [SerializeField] private Vector3 departureTargetOffset;
    [SerializeField] private SoundRef departureSound;
    [SerializeField] private GameplayPresentationDefinition departurePresentation;

    [Header("Screen Transition")]
    [SerializeField] private SceneTransitionVisualMode transitionVisualMode = SceneTransitionVisualMode.AlphaFade;
    [SerializeField, Min(0f)] private float coverDuration = 0.2f;
    [SerializeField, Min(0f)] private float revealDuration = 0.2f;

    [Header("Arrival")]
    [SerializeField] private SceneTravelArrivalMode arrivalMode;
    [SerializeField, Min(0f)] private float arrivalDuration = 0.55f;
    [SerializeField] private Vector3 arrivalStartOffset = new(0f, 3f, 0f);
    [SerializeField] private float arrivalRotationDegrees = 720f;
    [SerializeField] private SoundRef arrivalSound;
    [SerializeField] private GameplayPresentationDefinition arrivalPresentation;

    public SceneTravelDepartureMode DepartureMode => departureMode;
    public float DepartureDuration => Mathf.Max(0f, departureDuration);
    public float DepartureRotationDegrees => departureRotationDegrees;
    public Vector3 DepartureTargetOffset => departureTargetOffset;
    public SoundRef DepartureSound => departureSound;
    public GameplayPresentationDefinition DeparturePresentation => departurePresentation;
    public SceneTransitionVisualMode TransitionVisualMode => transitionVisualMode;
    public float CoverDuration => Mathf.Max(0f, coverDuration);
    public float RevealDuration => Mathf.Max(0f, revealDuration);
    public SceneTravelArrivalMode ArrivalMode => arrivalMode;
    public float ArrivalDuration => Mathf.Max(0f, arrivalDuration);
    public Vector3 ArrivalStartOffset => arrivalStartOffset;
    public float ArrivalRotationDegrees => arrivalRotationDegrees;
    public SoundRef ArrivalSound => arrivalSound;
    public GameplayPresentationDefinition ArrivalPresentation => arrivalPresentation;
}
