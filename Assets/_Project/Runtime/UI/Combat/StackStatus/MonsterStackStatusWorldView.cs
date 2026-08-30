using System.Collections;
using TMPro;
using UnityEngine;
using UnityGAS;

/// <summary>정수형 상태를 몬스터 머리 위 사각 아이콘·바·숫자로 표시하는 독립 월드 뷰입니다.</summary>
[DisallowMultipleComponent]
public sealed class MonsterStackStatusWorldView : MonoBehaviour
{
    private const string StatusSortingLayerName = "UI";

    private sealed class Backend : IMonsterStackStatusViewBackend
    {
        public void Attach(GameObject target, IMonsterStackStatusSource source)
        {
            if (target == null || source == null) return;
            MonsterStackStatusWorldView view = target.GetComponent<MonsterStackStatusWorldView>();
            if (view == null) view = target.AddComponent<MonsterStackStatusWorldView>();
            view.Bind(source);
        }

        public void Detach(GameObject target, IMonsterStackStatusSource source)
        {
            if (target == null) return;
            MonsterStackStatusWorldView view = target.GetComponent<MonsterStackStatusWorldView>();
            if (view != null && ReferenceEquals(view.source, source))
                view.Unbind(source);
        }
    }

    private static readonly Backend SharedBackend = new();
    private static Sprite squareSprite;

    private IMonsterStackStatusSource source;
    private GameObject visualRoot;
    private Transform icon;
    private Transform fill;
    private TextMeshPro countText;
    private Coroutine pulseRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBackend() => MonsterStackStatusViewPlayback.RegisterBackend(SharedBackend);

    public void Bind(IMonsterStackStatusSource value)
    {
        Unsubscribe();
        source = value;
        EnsureVisuals();
        source.StackChanged += Refresh;
        source.PulseRequested += PlayPulse;
        Refresh();
    }

    private void Unbind(IMonsterStackStatusSource value)
    {
        if (!ReferenceEquals(source, value)) return;
        Unsubscribe();
        if (visualRoot != null) visualRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (visualRoot != null) Destroy(visualRoot);
    }

    private void Unsubscribe()
    {
        if (source == null) return;
        source.StackChanged -= Refresh;
        source.PulseRequested -= PlayPulse;
        source = null;
    }

    private void EnsureVisuals()
    {
        if (visualRoot != null) return;

        visualRoot = new GameObject("StackStatusView");
        visualRoot.transform.SetParent(transform, false);
        visualRoot.transform.localPosition = new Vector3(0f, 1.25f, 0f);

        icon = CreateSquare("Icon", visualRoot.transform, new Vector3(-0.58f, 0f, 0f), new Vector2(0.24f, 0.24f), source.DisplayColor, 2).transform;
        CreateSquare("BarBackground", visualRoot.transform, Vector3.zero, new Vector2(0.9f, 0.14f), new Color(0.08f, 0.04f, 0.03f, 0.9f), 0);
        fill = CreateSquare("BarFill", visualRoot.transform, new Vector3(-0.45f, 0f, 0f), new Vector2(0.9f, 0.1f), source.DisplayColor, 1).transform;

        GameObject textObject = new("Count");
        textObject.transform.SetParent(visualRoot.transform, false);
        textObject.transform.localPosition = new Vector3(0.62f, -0.02f, 0f);
        countText = textObject.AddComponent<TextMeshPro>();
        countText.alignment = TextAlignmentOptions.Center;
        countText.fontSize = 2.4f;
        countText.color = Color.white;
        countText.sortingLayerID = SortingLayer.NameToID(StatusSortingLayerName);
        countText.sortingOrder = 3;
        countText.rectTransform.sizeDelta = new Vector2(0.65f, 0.35f);
    }

    private void Refresh()
    {
        if (source == null || visualRoot == null) return;
        bool visible = source.CurrentStacks > 0;
        visualRoot.SetActive(visible);
        if (!visible) return;

        float ratio = Mathf.Clamp01(source.CurrentStacks / (float)Mathf.Max(1, source.MaxStacks));
        fill.localScale = new Vector3(0.9f * ratio, 0.1f, 1f);
        fill.localPosition = new Vector3(-0.45f + 0.45f * ratio, 0f, 0f);
        countText.text = source.CurrentStacks.ToString();
    }

    private void PlayPulse()
    {
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        if (icon == null) yield break;
        icon.localScale = new Vector3(0.34f, 0.34f, 1f);
        yield return new WaitForSeconds(0.09f);
        if (icon != null) icon.localScale = new Vector3(0.24f, 0.24f, 1f);
        pulseRoutine = null;
    }

    private static GameObject CreateSquare(string name, Transform parent, Vector3 localPosition, Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        renderer.sortingLayerName = StatusSortingLayerName;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite == null)
            squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        return squareSprite;
    }
}
