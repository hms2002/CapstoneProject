using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerInteractionSensor2D : MonoBehaviour
{
    [SerializeField] private PlayerInteractableTracker2D tracker;

    public static PlayerInteractionSensor2D EnsureFor(Transform playerRoot, Collider2D sourceCollider, PlayerInteractableTracker2D tracker)
    {
        PlayerInteractionSensor2D misplacedRootSensor = playerRoot.GetComponent<PlayerInteractionSensor2D>();
        if (misplacedRootSensor != null)
            Destroy(misplacedRootSensor);

        PlayerInteractionSensor2D sensor = null;
        for (int i = 0; i < playerRoot.childCount; i++)
        {
            sensor = playerRoot.GetChild(i).GetComponent<PlayerInteractionSensor2D>();
            if (sensor != null)
                break;
        }

        if (sensor == null)
        {
            GameObject sensorObject = new("InteractionSensor");
            sensorObject.layer = playerRoot.gameObject.layer;
            sensorObject.transform.SetParent(playerRoot, false);
            sensor = sensorObject.AddComponent<PlayerInteractionSensor2D>();
        }

        sensor.tracker = tracker;
        sensor.SyncColliderFrom(sourceCollider);
        return sensor;
    }

    private void Awake()
    {
        if (transform.parent == null)
        {
            Collider2D rootCollider = GetComponent<Collider2D>();
            if (rootCollider != null)
                rootCollider.isTrigger = false;

            enabled = false;
            return;
        }

        ResolveTracker();
        EnsureSensorBody();
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        tracker?.RegisterOverlap(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        tracker?.UnregisterOverlap(other);
    }

    private void ResolveTracker()
    {
        if (tracker == null)
            tracker = GetComponentInParent<PlayerInteractableTracker2D>();
    }

    private void EnsureTriggerCollider()
    {
        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
            collider2D.isTrigger = true;
    }

    private void EnsureSensorBody()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.simulated = true;
        body.gravityScale = 0f;
        body.linearDamping = 0f;
        body.angularDamping = 0f;
        body.freezeRotation = true;
    }

    private void SyncColliderFrom(Collider2D sourceCollider)
    {
        Collider2D sensorCollider = GetComponent<Collider2D>();

        if (sourceCollider == null)
        {
            if (sensorCollider == null)
                sensorCollider = gameObject.AddComponent<CircleCollider2D>();

            sensorCollider.isTrigger = true;
            return;
        }

        if (sourceCollider is BoxCollider2D sourceBox)
        {
            BoxCollider2D targetBox = sensorCollider as BoxCollider2D;
            if (targetBox == null)
            {
                if (sensorCollider != null)
                    Destroy(sensorCollider);

                targetBox = gameObject.AddComponent<BoxCollider2D>();
            }

            targetBox.offset = sourceBox.offset;
            targetBox.size = sourceBox.size;
            targetBox.edgeRadius = sourceBox.edgeRadius;
            targetBox.isTrigger = true;
            return;
        }

        if (sourceCollider is CircleCollider2D sourceCircle)
        {
            CircleCollider2D targetCircle = sensorCollider as CircleCollider2D;
            if (targetCircle == null)
            {
                if (sensorCollider != null)
                    Destroy(sensorCollider);

                targetCircle = gameObject.AddComponent<CircleCollider2D>();
            }

            targetCircle.offset = sourceCircle.offset;
            targetCircle.radius = sourceCircle.radius;
            targetCircle.isTrigger = true;
            return;
        }

        if (sourceCollider is CapsuleCollider2D sourceCapsule)
        {
            CapsuleCollider2D targetCapsule = sensorCollider as CapsuleCollider2D;
            if (targetCapsule == null)
            {
                if (sensorCollider != null)
                    Destroy(sensorCollider);

                targetCapsule = gameObject.AddComponent<CapsuleCollider2D>();
            }

            targetCapsule.offset = sourceCapsule.offset;
            targetCapsule.size = sourceCapsule.size;
            targetCapsule.direction = sourceCapsule.direction;
            targetCapsule.isTrigger = true;
            return;
        }

        if (sensorCollider == null)
            sensorCollider = gameObject.AddComponent<CircleCollider2D>();

        sensorCollider.isTrigger = true;
    }
}
