using UnityEngine;
using UnityGAS;

public class Witch : BossControllerBase
{
    protected override void Update()
    {
        base.Update();

        // 스프라이트 반전
        if      (transform.position.x > target.position.x) sprite.flipX = true;
        else if (transform.position.x < target.position.x) sprite.flipX = false;
    }
}