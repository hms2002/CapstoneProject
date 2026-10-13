using System;
using TMPro;
using UnityEngine;

/// <summary>Controls four scene-authored icon grids and emits selection/command requests.</summary>
[DisallowMultipleComponent]
public sealed class TestSceneCatalogGrid : MonoBehaviour
{
    public const int WeaponCategory = 0;
    public const int RelicCategory = 1;
    public const int MonsterCategory = 2;
    public const int BossCategory = 3;

    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject[] categoryRoots;
    [SerializeField] private TestSceneCatalogCell[] weaponCells;
    [SerializeField] private TestSceneCatalogCell[] relicCells;
    [SerializeField] private TestSceneCatalogCell[] monsterCells;
    [SerializeField] private TestSceneCatalogCell[] bossCells;
    [SerializeField] private UnityEngine.UI.Button[] categoryButtons;
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [SerializeField] private UnityEngine.UI.Button executeButton;
    [SerializeField] private TMP_Text executeLabel;
    [SerializeField] private TMP_Text selectionText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private GameObject weaponOptions;
    [SerializeField] private GameObject monsterOptions;

    private readonly int[] selectedIndices = new int[4];
    private int currentCategory;

    public event Action<int, int> SelectionChanged;
    public event Action<int> ExecuteRequested;
    public bool IsOpen => panelRoot != null && panelRoot.activeInHierarchy;

    private void OnEnable()
    {
        BindButtons(true);
        for (int category = 0; category < selectedIndices.Length; category++)
        {
            TestSceneCatalogCell[] cells = Cells(category);
            if (cells == null) continue;
            for (int index = 0; index < cells.Length; index++)
                if (cells[index] != null) cells[index].Bind(category, index, Select);
        }
        Refresh();
    }

    private void OnDisable()
    {
        BindButtons(false);
        for (int category = 0; category < selectedIndices.Length; category++)
        {
            TestSceneCatalogCell[] cells = Cells(category);
            if (cells == null) continue;
            foreach (TestSceneCatalogCell cell in cells)
                if (cell != null) cell.Unbind();
        }
    }

    public void Open(int category)
    {
        if (!IsCategory(category)) return;
        currentCategory = category;
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void ShowFeedback(string message)
    {
        if (feedbackText != null) feedbackText.text = message;
    }

    public int Count(int category) => Cells(category)?.Length ?? 0;

    public int GetSelectedIndex(int category)
    {
        if (!IsCategory(category) || Count(category) == 0) return -1;
        return Mathf.Clamp(selectedIndices[category], 0, Count(category) - 1);
    }

    public void Select(int category, int index)
    {
        TestSceneCatalogCell[] cells = Cells(category);
        if (cells == null || index < 0 || index >= cells.Length || cells[index] == null) return;
        selectedIndices[category] = index;
        Refresh();
        SelectionChanged?.Invoke(category, index);
    }

    private void Refresh()
    {
        for (int category = 0; category < selectedIndices.Length; category++)
        {
            if (categoryRoots != null && category < categoryRoots.Length && categoryRoots[category] != null)
                categoryRoots[category].SetActive(category == currentCategory);
            if (categoryButtons != null && category < categoryButtons.Length && categoryButtons[category] != null)
                categoryButtons[category].interactable = category != currentCategory;
            TestSceneCatalogCell[] cells = Cells(category);
            if (cells == null) continue;
            int selectedIndex = GetSelectedIndex(category);
            for (int index = 0; index < cells.Length; index++)
                if (cells[index] != null) cells[index].SetSelected(index == selectedIndex);
        }
        if (weaponOptions != null) weaponOptions.SetActive(currentCategory == WeaponCategory);
        if (monsterOptions != null)
            monsterOptions.SetActive(currentCategory == MonsterCategory);
        int choice = GetSelectedIndex(currentCategory);
        TestSceneCatalogCell[] currentCells = Cells(currentCategory);
        bool hasChoice = choice >= 0 && currentCells[choice] != null;
        if (selectionText != null)
            selectionText.text = hasChoice ? $"선택: {currentCells[choice].DisplayName.Replace("\n", " · ")}" : "선택 가능한 항목이 없습니다.";
        if (executeButton != null) executeButton.interactable = hasChoice;
        if (executeLabel != null)
        {
            executeLabel.text = currentCategory switch
            {
                WeaponCategory => "선택한 무기 지급",
                RelicCategory => "선택한 유물 지급 / 강화",
                MonsterCategory => "선택한 몬스터 생성",
                BossCategory => "보스 생성 (1마리)",
                _ => string.Empty,
            };
        }
    }

    private void Execute()
    {
        if (GetSelectedIndex(currentCategory) >= 0) ExecuteRequested?.Invoke(currentCategory);
    }

    private void OpenWeapons() => Open(WeaponCategory);
    private void OpenRelics() => Open(RelicCategory);
    private void OpenMonsters() => Open(MonsterCategory);
    private void OpenBosses() => Open(BossCategory);

    private void BindButtons(bool bind)
    {
        if (categoryButtons != null)
        {
            if (categoryButtons.Length > WeaponCategory) Bind(categoryButtons[WeaponCategory], OpenWeapons, bind);
            if (categoryButtons.Length > RelicCategory) Bind(categoryButtons[RelicCategory], OpenRelics, bind);
            if (categoryButtons.Length > MonsterCategory) Bind(categoryButtons[MonsterCategory], OpenMonsters, bind);
            if (categoryButtons.Length > BossCategory) Bind(categoryButtons[BossCategory], OpenBosses, bind);
        }
        Bind(closeButton, Close, bind);
        Bind(executeButton, Execute, bind);
    }

    private static void Bind(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction command, bool bind)
    {
        if (button == null) return;
        if (bind) button.onClick.AddListener(command);
        else button.onClick.RemoveListener(command);
    }

    private static bool IsCategory(int category) => category >= WeaponCategory && category <= BossCategory;

    private TestSceneCatalogCell[] Cells(int category) => category switch
    {
        WeaponCategory => weaponCells,
        RelicCategory => relicCells,
        MonsterCategory => monsterCells,
        BossCategory => bossCells,
        _ => null,
    };
}
