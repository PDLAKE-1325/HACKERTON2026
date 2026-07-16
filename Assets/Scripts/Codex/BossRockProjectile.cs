using UnityEngine;

public class BossRockProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float damage = 20f;
    [SerializeField] private float knockbackForce = 9f;
    [SerializeField] private float lifetime = 6f;

    private bool launched;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();
    }

    public void Launch(Vector2 velocity, Transform owner)
    {
        if (body == null)
            return;

        launched = true;
        body.linearVelocity = velocity;

        if (bodyCollider != null && owner != null)
        {
            foreach (Collider2D ownerCollider in owner.GetComponentsInChildren<Collider2D>())
                Physics2D.IgnoreCollision(bodyCollider, ownerCollider, true);
        }

        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!launched)
            return;

        int collisionLayer = 1 << collision.gameObject.layer;
        if ((playerLayer.value & collisionLayer) != 0)
        {
            IDamageable damageable = collision.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeHit(new HitData
            {
                damage = damage,
                knockbackForce = knockbackForce,
                sourcePosition = transform.position,
                applyMark = false
            });
        }

        Destroy(gameObject);
    }
}
