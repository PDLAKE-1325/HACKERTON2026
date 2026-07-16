using UnityEngine;

public class PlayerProjectile : MonoBehaviour
{
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private PlayerCombat owner;
    private LayerMask hitLayers;
    private float damage;
    private float knockback;
    private Vector2 sourcePosition;
    private bool hasHit;

    private void Awake()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Initialize(
        PlayerCombat projectileOwner,
        Vector2 direction,
        float speed,
        float projectileDamage,
        float projectileKnockback,
        float lifetime,
        LayerMask collisionLayers,
        Sprite image)
    {
        owner = projectileOwner;
        hitLayers = collisionLayers;
        damage = projectileDamage;
        knockback = projectileKnockback;
        sourcePosition = transform.position;

        if (spriteRenderer != null && image != null)
            spriteRenderer.sprite = image;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector2.right;
        direction.Normalize();

        transform.right = direction;
        if (body != null)
            body.linearVelocity = direction * speed;

        Destroy(gameObject, Mathf.Max(0.01f, lifetime));
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit || (hitLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        hasHit = true;
        if (body != null)
            body.linearVelocity = Vector2.zero;

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeHit(new HitData
            {
                damage = damage,
                knockbackForce = knockback,
                sourcePosition = sourcePosition,
                applyMark = true
            });
        }

        if (owner != null)
            owner.HandleProjectileImpact(damageable, transform.position);

        Destroy(gameObject);
    }
}
