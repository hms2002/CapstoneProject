using UnityEngine;
using UnityGAS;

public class PlayerMove2D : MonoBehaviour
{
    [SerializeField] private TagSystem tags;
    [SerializeField] private GameplayTag moveBlockTag;
    [SerializeField] private float speed = 5f;

    private Rigidbody2D rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (tags == null) tags = GetComponent<TagSystem>();
    }

    private void FixedUpdate()
    {
        if (tags != null && moveBlockTag != null && tags.HasTag(moveBlockTag))
            return;

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        rb.MovePosition(rb.position + input * speed * Time.fixedDeltaTime);
    }
}
