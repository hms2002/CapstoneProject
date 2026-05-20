using TMPro;
using UnityEngine;

/// <summary>
/// 책임 : 월드 공간에 생성된 데미지 텍스트의 표시, 이동, 크기 변화, 페이드 아웃 수명을 관리한다.
/// 스스로의 연출만 담당하며, 언제 어디에 생성할지는 담당하지 않는다.
/// </summary>
public class DamagePopupWorldText : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshPro text;
    [SerializeField] private SpriteRenderer optionalSpriteRenderer;

    [Header("Motion (World space)")]
    [SerializeField] private Vector3 moveVelocity = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float lifetime = 0.75f;
    [SerializeField] private float fadeOutRatio = 0.55f;

    [Header("Scale")]
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float endScale = 1.1f;

    [Header("Facing")]
    [Tooltip("항상 카메라 정면을 향하게 할지 여부. 2D 정면 게임이면 보통 꺼도 된다.")]
    [SerializeField] private bool faceCamera = false;

    private float _t;
    private Color _textColor;
    private Color _spriteColor;
    private bool _hasSprite;
    private Vector3 _runtimeMoveVelocity;
    private float _runtimeLifetime;
    private float _runtimeFadeOutRatio;
    private float _runtimeStartScale;
    private float _runtimeEndScale;
    private float _defaultFontSize;

    private void Reset()
    {
        text = GetComponentInChildren<TextMeshPro>();

        if (optionalSpriteRenderer == null)
            optionalSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Awake()
    {
        if (text == null)
            text = GetComponentInChildren<TextMeshPro>();

        if (optionalSpriteRenderer == null)
            optionalSpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (text != null)
        {
            _textColor = text.color;
            _defaultFontSize = text.fontSize;
        }

        _hasSprite = optionalSpriteRenderer != null;
        if (_hasSprite)
            _spriteColor = optionalSpriteRenderer.color;

        ResetRuntimeStyle();
    }

    /// <summary>
    /// 책임 : 표시할 데미지 값을 반영하고 연출 상태를 초기화한다.
    /// </summary>
    public void Setup(int amount)
    {
        Setup(amount.ToString());
    }

    /// <summary>
    /// 책임 : 표시할 커스텀 텍스트를 반영하고 연출 상태를 초기화한다.
    /// </summary>
    public void Setup(string content)
    {
        ResetRuntimeStyle();
        ApplyContent(content);
    }

    /// <summary>
    /// 책임 :
    /// - DamagePopupFormatProfileSO가 결정한 최종 표시 모델을 반영한다.
    /// - 팝업 인스턴스마다 색/폰트/이동/수명 값을 독립적으로 덮어쓸 수 있게 한다.
    /// </summary>
    public void Setup(DamagePopupViewModel viewModel)
    {
        _runtimeMoveVelocity = viewModel.MoveVelocity;
        _runtimeLifetime = Mathf.Max(0.05f, viewModel.Lifetime);
        _runtimeFadeOutRatio = Mathf.Clamp01(viewModel.FadeOutRatio);
        _runtimeStartScale = Mathf.Max(0.01f, viewModel.StartScale);
        _runtimeEndScale = Mathf.Max(0.01f, viewModel.EndScale);

        _textColor = viewModel.TextColor;

        if (text != null && viewModel.OverrideFontSize)
            text.fontSize = viewModel.FontSize;

        ApplyContent(viewModel.Text);
    }

    private void ApplyContent(string content)
    {
        _t = 0f;

        if (text != null)
            text.text = string.IsNullOrWhiteSpace(content) ? string.Empty : content;

        transform.localScale = Vector3.one * _runtimeStartScale;

        if (text != null)
            text.color = _textColor;

        if (_hasSprite)
            optionalSpriteRenderer.color = _spriteColor;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float p = Mathf.Clamp01(_t / _runtimeLifetime);

        transform.position += _runtimeMoveVelocity * Time.deltaTime;
        transform.localScale = Vector3.one * Mathf.Lerp(_runtimeStartScale, _runtimeEndScale, p);

        if (faceCamera)
            UpdateFacing();

        if (p >= _runtimeFadeOutRatio)
        {
            float fp = (p - _runtimeFadeOutRatio) / Mathf.Max(0.0001f, 1f - _runtimeFadeOutRatio);
            float alpha = Mathf.Lerp(1f, 0f, fp);

            if (text != null)
            {
                Color c = _textColor;
                c.a = alpha;
                text.color = c;
            }

            if (_hasSprite)
            {
                Color c = _spriteColor;
                c.a = alpha;
                optionalSpriteRenderer.color = c;
            }
        }

        if (_t >= _runtimeLifetime)
            Destroy(gameObject);
    }

    private void ResetRuntimeStyle()
    {
        _runtimeMoveVelocity = moveVelocity;
        _runtimeLifetime = Mathf.Max(0.05f, lifetime);
        _runtimeFadeOutRatio = Mathf.Clamp01(fadeOutRatio);
        _runtimeStartScale = Mathf.Max(0.01f, startScale);
        _runtimeEndScale = Mathf.Max(0.01f, endScale);

        if (text != null && _defaultFontSize > 0f)
            text.fontSize = _defaultFontSize;
    }

    /// <summary>
    /// 책임 : 필요할 때 월드 텍스트가 카메라 정면을 향하도록 보정한다.
    /// </summary>
    private void UpdateFacing()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        transform.forward = cam.transform.forward;
    }
}
