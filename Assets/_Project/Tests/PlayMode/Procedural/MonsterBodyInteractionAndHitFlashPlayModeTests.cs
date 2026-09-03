using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;

/// <summary>
/// 책임:
/// Player/Enemy 진영별 몸체 충돌, Enemy 간 소프트 분리, 피격 점멸 재생 및 비활성 초기화를 회귀 검증한다.
/// </summary>
public sealed class MonsterBodyInteractionAndHitFlashPlayModeTests
{
    [Test]
    public void CollisionProfiles_ExcludeOnlySameFaction()
    {
        GameObject player = new("PlayerBody");
        GameObject enemy = new("EnemyBody");
        try
        {
            player.layer = LayerMask.NameToLayer("Player");
            enemy.layer = LayerMask.NameToLayer("TEMP_Enemy_LAYER");

            CircleCollider2D playerCollider = player.AddComponent<CircleCollider2D>();
            CircleCollider2D enemyCollider = enemy.AddComponent<CircleCollider2D>();
            EntityCollisionProfile2D playerProfile = player.AddComponent<EntityCollisionProfile2D>();
            EntityCollisionProfile2D enemyProfile = enemy.AddComponent<EntityCollisionProfile2D>();

            int playerMask = LayerMask.GetMask("Player");
            int enemyMask = LayerMask.GetMask("TEMP_Enemy_LAYER");
            playerProfile.Configure(
                new Collider2D[] { playerCollider },
                0,
                playerMask,
                EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);
            enemyProfile.Configure(
                new Collider2D[] { enemyCollider },
                0,
                enemyMask,
                EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);

            Assert.That((playerCollider.excludeLayers.value & playerMask) != 0, Is.True);
            Assert.That((playerCollider.excludeLayers.value & enemyMask) == 0, Is.True);
            Assert.That((enemyCollider.excludeLayers.value & enemyMask) != 0, Is.True);
            Assert.That((enemyCollider.excludeLayers.value & playerMask) == 0, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(player);
        }
    }

    [UnityTest]
    public IEnumerator OpposingFactionBodies_PushWithoutCrossingThroughEachOther()
    {
        GameObject player = CreateDynamicBody("PlayerBody", "Player", new Vector2(-1.25f, 0f));
        GameObject enemy = CreateDynamicBody("EnemyBody", "TEMP_Enemy_LAYER", Vector2.zero);
        try
        {
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Rigidbody2D enemyBody = enemy.GetComponent<Rigidbody2D>();
            CircleCollider2D playerCollider = player.GetComponent<CircleCollider2D>();
            CircleCollider2D enemyCollider = enemy.GetComponent<CircleCollider2D>();
            EntityCollisionProfile2D playerProfile = player.AddComponent<EntityCollisionProfile2D>();
            EntityCollisionProfile2D enemyProfile = enemy.AddComponent<EntityCollisionProfile2D>();

            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemyBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            enemyBody.mass = 2.5f;
            playerProfile.Configure(
                new Collider2D[] { playerCollider },
                0,
                LayerMask.GetMask("Player"),
                EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);
            enemyProfile.Configure(
                new Collider2D[] { enemyCollider },
                0,
                LayerMask.GetMask("TEMP_Enemy_LAYER"),
                EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);

            playerBody.linearVelocity = Vector2.right * 10f;
            for (int i = 0; i < 8; i++)
                yield return new WaitForFixedUpdate();

            Assert.That(player.transform.position.x, Is.LessThan(enemy.transform.position.x));
            Assert.That(enemy.transform.position.x, Is.GreaterThan(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(player);
        }
    }

    [UnityTest]
    public IEnumerator ActorSoftCollision_ProducesSeparationVelocityWhileEnemyBodiesPassThrough()
    {
        GameObject first = CreateDynamicBody("FirstEnemy", "TEMP_Enemy_LAYER", Vector2.zero);
        GameObject second = CreateDynamicBody("SecondEnemy", "TEMP_Enemy_LAYER", new Vector2(0.25f, 0f));
        try
        {
            CircleCollider2D firstCollider = first.GetComponent<CircleCollider2D>();
            Rigidbody2D firstBody = first.GetComponent<Rigidbody2D>();
            ExternalMovementController2D externalMovement = first.AddComponent<ExternalMovementController2D>();
            EntityCollisionProfile2D profile = first.AddComponent<EntityCollisionProfile2D>();
            int enemyMask = LayerMask.GetMask("TEMP_Enemy_LAYER");
            profile.Configure(
                new Collider2D[] { firstCollider },
                0,
                enemyMask,
                EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);

            ActorSoftCollision2D softCollision = first.AddComponent<ActorSoftCollision2D>();
            softCollision.Configure(
                firstCollider,
                firstBody,
                externalMovement,
                profile,
                enemyMask,
                0,
                suspendForPassThroughMode: false,
                configuredPushSpeed: 2.8f,
                configuredPushResistance: 2f,
                configuredPushDurationSeconds: 0.08f,
                configuredWallProbeDistance: 0f,
                configuredMaxActorsPerTick: 8);

            yield return new WaitForFixedUpdate();

            Assert.That(softCollision.PushResistance, Is.EqualTo(2f).Within(0.001f));
            Assert.That(externalMovement.GetCurrentExternalVelocity().x, Is.EqualTo(-1.4f).Within(0.05f));
        }
        finally
        {
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(first);
        }
    }

    [UnityTest]
    public IEnumerator SpriteHitFlashController_UsesPointZeroEightSecondsAndResetsWhenDisabled()
    {
        GameObject target = new("FlashTarget");
        try
        {
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            SpriteHitFlashController flash = target.AddComponent<SpriteHitFlashController>();
            MaterialPropertyBlock properties = new();
            int flashAmountId = Shader.PropertyToID("_FlashAmount");

            flash.PlayFlash();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(flashAmountId), Is.GreaterThan(0.99f));

            yield return new WaitForSeconds(0.10f);

            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(flashAmountId), Is.EqualTo(0f).Within(0.001f));

            flash.PlayFlash();
            flash.enabled = false;
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat(flashAmountId), Is.EqualTo(0f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(target);
        }
    }

    private static GameObject CreateDynamicBody(string name, string layerName, Vector2 position)
    {
        GameObject instance = new(name);
        instance.layer = LayerMask.NameToLayer(layerName);
        instance.transform.position = position;
        Rigidbody2D body = instance.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        CircleCollider2D collider = instance.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        return instance;
    }
}
