using System;
using System.Collections;
using UnityEngine;

public class LightningStrikeVfx : MonoBehaviour
{
    public float startHeight = 6f;
    public float impactDelay = 0.20f;
    public float lifeTime = 1.0f;

    private Action onImpact;

    public void Play(Vector3 impactPos, Action onImpact)
    {
        this.onImpact = onImpact;
        transform.position = impactPos + Vector3.up * startHeight;
        StartCoroutine(CoImpact(impactPos));
    }

    private IEnumerator CoImpact(Vector3 impactPos)
    {
        yield return new WaitForSeconds(impactDelay);

        transform.position = impactPos; // 임팩트 순간
        onImpact?.Invoke();

        yield return new WaitForSeconds(Mathf.Max(0f, lifeTime - impactDelay));
        Destroy(gameObject);
    }
}
