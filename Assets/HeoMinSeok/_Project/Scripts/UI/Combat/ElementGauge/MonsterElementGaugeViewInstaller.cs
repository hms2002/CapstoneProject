using UnityEngine;
using UnityGAS;

public class MonsterElementGaugeViewInstaller : MonoBehaviour
{
    // 이 클래스의 책임:
    // 몬스터 생성 시 ElementGaugeSystem에 대응하는 월드 게이지 View를 생성하고 대상에 바인딩한다.

    [SerializeField] private MonsterElementGaugeView viewPrefab;
    [SerializeField] private Transform uiParentOverride;

    public MonsterElementGaugeView Install(GameObject monster)
    {
        if (monster == null)
            return null;

        if (viewPrefab == null)
            return null;

        var gaugeSystem = monster.GetComponent<ElementGaugeSystem>();
        if (gaugeSystem == null)
            return null;

        Transform parent = uiParentOverride != null ? uiParentOverride : monster.transform;
        var view = Instantiate(viewPrefab, parent);
        view.Bind(monster.transform, gaugeSystem);
        return view;
    }
}
