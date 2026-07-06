using UnityEngine;
using UnityGAS;

public class MonsterElementGaugeViewInstaller : MonoBehaviour, IMonsterElementGaugeViewInstaller
{
    // 이 클래스의 책임:
    // 몬스터 생성 시 ElementGaugeSystem에 대응하는 월드 게이지 View를 생성하고 대상 수명에 맞춰 정리한다.

    [SerializeField] private MonsterElementGaugeView viewPrefab;
    [SerializeField] private Transform uiParentOverride;
    [SerializeField] private bool installOnStart = true;

    private MonsterElementGaugeView installedView;

    public Component InstallerComponent => this;

    private void Start()
    {
        if (!installOnStart)
            return;

        Install(gameObject);
    }

    private void OnDestroy()
    {
        Uninstall();
    }

    public MonsterElementGaugeView Install(GameObject monster)
    {
        if (monster == null)
            return null;

        if (viewPrefab == null)
            return null;

        var gaugeSystem = monster.GetComponent<ElementGaugeSystem>();
        if (gaugeSystem == null)
            return null;

        if (installedView != null)
            return installedView;

        MonsterElementGaugeView existingView = monster.GetComponentInChildren<MonsterElementGaugeView>(true);
        if (existingView != null)
        {
            installedView = existingView;
            installedView.Bind(monster.transform, gaugeSystem);
            return installedView;
        }

        Transform parent = uiParentOverride != null ? uiParentOverride : monster.transform;
        var view = Instantiate(viewPrefab, parent);
        view.Bind(monster.transform, gaugeSystem);
        installedView = view;
        return installedView;
    }

    public void InstallFor(GameObject monster)
    {
        Install(monster);
    }

    public void Uninstall()
    {
        MonsterElementGaugeView view = installedView != null
            ? installedView
            : GetComponentInChildren<MonsterElementGaugeView>(true);

        if (view == null)
            return;

        Destroy(view.gameObject);
        installedView = null;
    }
}
