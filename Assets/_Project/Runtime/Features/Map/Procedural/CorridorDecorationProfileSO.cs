using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 한 테마에서 사용할 복도 장식 모듈과 복도당 랜드마크 제한을 보관한다.
/// - 런타임 조립기와 제작 미리보기가 동일한 장식 후보 집합을 사용하게 한다.
/// </summary>
[CreateAssetMenu(
    fileName = "CorridorDecorationProfile",
    menuName = "Gameplay/Dungeon/Corridor Decoration Profile")]
public sealed class CorridorDecorationProfileSO : ScriptableObject
{
    [Tooltip("한 연결 복도에 배치할 수 있는 Landmark 조각의 최대 수입니다.")]
    [SerializeField, Min(0)] private int maxLandmarksPerCorridor = 1;
    [SerializeField] private List<CorridorDecorationModuleSO> modules = new();

    public int MaxLandmarksPerCorridor => Mathf.Max(0, maxLandmarksPerCorridor);
    public IReadOnlyList<CorridorDecorationModuleSO> Modules =>
        modules ?? (IReadOnlyList<CorridorDecorationModuleSO>)Array.Empty<CorridorDecorationModuleSO>();

#if UNITY_EDITOR
    /// <summary>
    /// 책임 : 복도 제작 툴이 랜드마크 제한과 모듈 후보를 중복 없이 테마 장식 프로필에 저장한다.
    /// </summary>
    public void EditorConfigure(
        int landmarkLimit,
        IReadOnlyList<CorridorDecorationModuleSO> decorationModules)
    {
        maxLandmarksPerCorridor = Mathf.Max(0, landmarkLimit);
        modules ??= new List<CorridorDecorationModuleSO>();
        modules.Clear();
        if (decorationModules == null)
            return;

        for (int moduleIndex = 0; moduleIndex < decorationModules.Count; moduleIndex++)
        {
            CorridorDecorationModuleSO module = decorationModules[moduleIndex];
            if (module != null && !modules.Contains(module))
                modules.Add(module);
        }
    }

    /// <summary>
    /// 책임 : 새로 Bake한 모듈을 기존 테마 후보를 지우지 않고 한 번만 등록한다.
    /// </summary>
    public void EditorAddModule(CorridorDecorationModuleSO module)
    {
        modules ??= new List<CorridorDecorationModuleSO>();
        if (module != null && !modules.Contains(module))
            modules.Add(module);
    }

    private void OnValidate()
    {
        maxLandmarksPerCorridor = Mathf.Max(0, maxLandmarksPerCorridor);
        modules ??= new List<CorridorDecorationModuleSO>();
        for (int moduleIndex = modules.Count - 1; moduleIndex >= 0; moduleIndex--)
        {
            CorridorDecorationModuleSO module = modules[moduleIndex];
            if (module == null || modules.IndexOf(module) != moduleIndex)
                modules.RemoveAt(moduleIndex);
        }
    }
#endif
}

/// <summary>
/// 책임 : 한 복도 안에서 선택된 장식 모듈과 첫 진행축 셀 오프셋을 불변 결과로 전달한다.
/// </summary>
public readonly struct CorridorDecorationPlacement
{
    public CorridorDecorationModuleSO Module { get; }
    public int ForwardOffset { get; }
    public int EndOffsetExclusive => ForwardOffset + (Module != null ? Module.Length : 0);

    public CorridorDecorationPlacement(
        CorridorDecorationModuleSO module,
        int forwardOffset)
    {
        Module = module;
        ForwardOffset = Mathf.Max(0, forwardOffset);
    }
}

/// <summary>
/// 책임:
/// - 가변 복도 전체 길이에 요청 축과 일치하는 Start·Short·Middle·Landmark·Filler·End 모듈을 겹치지 않게 조합한다.
/// - 레이아웃 Seed와 연결 번호만으로 같은 배치 결과를 반복 생성한다.
/// </summary>
public static class CorridorDecorationComposer
{
    public static List<CorridorDecorationPlacement> Compose(
        CorridorDecorationProfileSO profile,
        int corridorLength,
        int layoutSeed,
        int connectionIndex,
        CorridorDecorationAxis axis = CorridorDecorationAxis.Horizontal)
    {
        var placements = new List<CorridorDecorationPlacement>();
        if (profile == null || corridorLength <= 0 || profile.Modules.Count == 0)
            return placements;

        const int spanStart = 0;
        int spanEnd = corridorLength;
        int spanLength = corridorLength;

        var random = new System.Random(unchecked(
            layoutSeed ^
            (connectionIndex * 486187739) ^
            (corridorLength * 16777619) ^
            ((int)axis * 104729)));

        List<CorridorDecorationModuleSO> exactShort = CollectCandidates(
            profile.Modules,
            axis,
            CorridorDecorationModuleRole.Short,
            spanLength,
            requireExactLength: true);
        if (exactShort.Count > 0)
        {
            placements.Add(new CorridorDecorationPlacement(
                exactShort[random.Next(exactShort.Count)],
                spanStart));
            return placements;
        }

        int cursor = spanStart;
        CorridorDecorationModuleSO start = SelectCandidate(
            profile.Modules,
            axis,
            CorridorDecorationModuleRole.Start,
            spanEnd - cursor,
            random);
        if (start != null)
        {
            placements.Add(new CorridorDecorationPlacement(start, cursor));
            cursor += start.Length;
        }

        CorridorDecorationModuleSO end = SelectCandidate(
            profile.Modules,
            axis,
            CorridorDecorationModuleRole.End,
            spanEnd - cursor,
            random);
        int bodyEnd = end != null ? spanEnd - end.Length : spanEnd;
        if (bodyEnd < cursor)
        {
            end = null;
            bodyEnd = spanEnd;
        }

        int landmarkCount = 0;
        CorridorDecorationModuleSO previous = start;
        while (cursor < bodyEnd)
        {
            int remaining = bodyEnd - cursor;
            var candidates = new List<CorridorDecorationModuleSO>();
            AddCandidates(
                candidates,
                profile.Modules,
                axis,
                CorridorDecorationModuleRole.Middle,
                remaining);
            AddCandidates(
                candidates,
                profile.Modules,
                axis,
                CorridorDecorationModuleRole.Filler,
                remaining);
            if (landmarkCount < profile.MaxLandmarksPerCorridor)
            {
                AddCandidates(
                    candidates,
                    profile.Modules,
                    axis,
                    CorridorDecorationModuleRole.Landmark,
                    remaining);
            }

            RemoveRepeatedCandidateWhenAlternativesExist(candidates, previous);
            if (candidates.Count == 0)
            {
                cursor++;
                previous = null;
                continue;
            }

            CorridorDecorationModuleSO selected = candidates[random.Next(candidates.Count)];
            placements.Add(new CorridorDecorationPlacement(selected, cursor));
            cursor += selected.Length;
            previous = selected;
            if (selected.Role == CorridorDecorationModuleRole.Landmark)
                landmarkCount++;
        }

        if (end != null)
            placements.Add(new CorridorDecorationPlacement(end, bodyEnd));

        placements.Sort((left, right) => left.ForwardOffset.CompareTo(right.ForwardOffset));
        return placements;
    }

    private static CorridorDecorationModuleSO SelectCandidate(
        IReadOnlyList<CorridorDecorationModuleSO> modules,
        CorridorDecorationAxis axis,
        CorridorDecorationModuleRole role,
        int maximumLength,
        System.Random random)
    {
        List<CorridorDecorationModuleSO> candidates = CollectCandidates(
            modules,
            axis,
            role,
            maximumLength,
            requireExactLength: false);
        return candidates.Count > 0
            ? candidates[random.Next(candidates.Count)]
            : null;
    }

    private static List<CorridorDecorationModuleSO> CollectCandidates(
        IReadOnlyList<CorridorDecorationModuleSO> modules,
        CorridorDecorationAxis axis,
        CorridorDecorationModuleRole role,
        int length,
        bool requireExactLength)
    {
        var results = new List<CorridorDecorationModuleSO>();
        if (modules == null)
            return results;

        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            CorridorDecorationModuleSO module = modules[moduleIndex];
            if (module == null ||
                module.Axis != axis ||
                module.Role != role ||
                (requireExactLength
                    ? module.Length != length
                    : module.Length > length))
            {
                continue;
            }

            results.Add(module);
        }

        return results;
    }

    private static void AddCandidates(
        List<CorridorDecorationModuleSO> destination,
        IReadOnlyList<CorridorDecorationModuleSO> modules,
        CorridorDecorationAxis axis,
        CorridorDecorationModuleRole role,
        int maximumLength)
    {
        for (int moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
        {
            CorridorDecorationModuleSO module = modules[moduleIndex];
            if (module != null &&
                module.Axis == axis &&
                module.Role == role &&
                module.Length <= maximumLength)
                destination.Add(module);
        }
    }

    private static void RemoveRepeatedCandidateWhenAlternativesExist(
        List<CorridorDecorationModuleSO> candidates,
        CorridorDecorationModuleSO previous)
    {
        if (previous == null || candidates.Count <= 1)
            return;

        bool hasAlternative = false;
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            if (candidates[candidateIndex] != previous)
            {
                hasAlternative = true;
                break;
            }
        }

        if (hasAlternative)
            candidates.RemoveAll(candidate => candidate == previous);
    }
}
