using UnityEngine;
using UnityGAS;

public class MonsterElementGaugeViewInstaller : MonoBehaviour
{
    [SerializeField] private MonsterElementGaugeView viewPrefab;
    [SerializeField] private Transform uiParentOverride;

    public MonsterElementGaugeView Install(GameObject monster)
    {
        if (monster == null || viewPrefab == null)
            return null;

        var gaugeSystem = monster.GetComponent<ElementGaugeSystem>();
        if (gaugeSystem == null)
            return null;
        if(uiParentOverride == null) uiParentOverride = monster.transform;
        var view = Instantiate(viewPrefab, uiParentOverride);
        view.Bind(monster.transform, gaugeSystem);
        return view;
    }
}