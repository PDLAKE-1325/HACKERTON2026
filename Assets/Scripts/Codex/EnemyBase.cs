using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable, IEnemy, IHealthTarget
{
    private enum HitboxShape
    {
        Box,
        Circle
    }

    [Header("References")]
    [SerializeField] protected Rigidbody2D body;
    [SerializeField] protected Animator animator;
    [SerializeField] protected Transform groundAheadCheck;
    [SerializeField] protected Transform attackPoint;
    [SerializeField] private PlayerSkill playerSkill;

    [Header("Layers")]
    [SerializeField] protected LayerMask groundLayer;
    [SerializeField] protected LayerMask playerLayer;

    [Header("Health")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected string hitTrigger = "Hit";
    [SerializeField] protected string deathTrigger = "Death";
    [SerializeField] protected float knockbackMovementLock = 0.18f;
    [SerializeField] protected float deathDestroyDelay = 1f;

    [Header("Patrol")]
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected float patrolRange = 4f;
    [SerializeField] protected float groundAheadDistance = 1.2f;
    [SerializeField] protected Vector2 directionChangeInterval = new Vector2(1.2f, 3f);

    [Header("Attack")]
    [SerializeField] protected float attackDetectionRange = 2f;
    [SerializeField] protected float attackCooldown = 1.2f;
    [SerializeField] protected string attackTrigger = "Attack";
    [SerializeField] private HitboxShape attackHitboxShape = HitboxShape.Box;
    [SerializeField] protected Vector2 attackBoxSize = new Vector2(1.5f, 1f);
    [SerializeField] protected float attackCircleRadius = 0.75f;
    [SerializeField] protected float attackDamage = 15f;
    [SerializeField] protected float attackKnockback = 6f;

    [Header("Mark")]
    [SerializeField] private float markDuration = 20f;
    [SerializeField] private SpriteRenderer markerPrefab;
    [SerializeField] private Sprite finishableMarkSprite;
    [SerializeField] private Sprite unavailableMarkSprite;
    [SerializeField] private Vector3 markerLocalOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private float finisherDamageFallback = 999f;

    private readonly HashSet<Component> hitTargets = new HashSet<Component>();
    private float currentHealth;
    private float spawnX;
    private float patrolDirection = 1f;
    private float nextDirectionChangeTime;
    private float nextAttackTime;
    private float movementLockedUntil;
    private bool attackHitboxEnabled;
    private bool isDead;
    private Coroutine markCoroutine;
    private SpriteRenderer markerInstance;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool HasMark { get; private set; }

    public event Action<IHealthTarget> HealthChanged;
    public event Action<IHealthTarget> Died;

    protected virtual void Awake()
    {
        currentHealth = maxHealth;
        spawnX = transform.position.x;
        patrolDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        ScheduleDirectionChange();

        if (playerSkill == null)
            playerSkill = FindAnyObjectByType<PlayerSkill>();
    }

    protected virtual void FixedUpdate()
    {
        if (isDead || body == null)
            return;

        DetectPlayerForAttack();

        if (attackHitboxEnabled)
            ScanAttackHitbox();

        if (Time.time >= movementLockedUntil)
            Patrol();
    }

    protected virtual void Patrol()
    {
        if (Time.time >= nextDirectionChangeTime)
        {
            patrolDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            ScheduleDirectionChange();
        }

        float distanceFromSpawn = transform.position.x - spawnX;
        if (Mathf.Abs(distanceFromSpawn) >= patrolRange)
            patrolDirection = distanceFromSpawn > 0f ? -1f : 1f;

        if (groundAheadCheck != null)
        {
            Vector2 rayOrigin = groundAheadCheck.position;
            rayOrigin.x += patrolDirection * 0.1f;
            bool hasGroundAhead = Physics2D.Raycast(
                rayOrigin,
                Vector2.down,
                groundAheadDistance,
                groundLayer);

            if (!hasGroundAhead)
                patrolDirection *= -1f;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.x = patrolDirection * moveSpeed;
        body.linearVelocity = velocity;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * patrolDirection;
        transform.localScale = scale;
    }

    protected virtual void DetectPlayerForAttack()
    {
        if (attackPoint == null || Time.time < nextAttackTime)
            return;

        Collider2D player = Physics2D.OverlapCircle(
            attackPoint.position,
            attackDetectionRange,
            playerLayer);
        if (player == null)
            return;

        nextAttackTime = Time.time + attackCooldown;
        movementLockedUntil = Mathf.Max(movementLockedUntil, Time.time + attackCooldown * 0.5f);

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);
    }

    public virtual void EnableAttackHitbox()
    {
        hitTargets.Clear();
        attackHitboxEnabled = true;
        ScanAttackHitbox();
    }

    public virtual void DisableAttackHitbox()
    {
        attackHitboxEnabled = false;
        hitTargets.Clear();
    }

    protected virtual void ScanAttackHitbox()
    {
        if (attackPoint == null)
            return;

        Collider2D[] overlaps = attackHitboxShape == HitboxShape.Box
            ? Physics2D.OverlapBoxAll(
                attackPoint.position,
                attackBoxSize,
                attackPoint.eulerAngles.z,
                playerLayer)
            : Physics2D.OverlapCircleAll(
                attackPoint.position,
                attackCircleRadius,
                playerLayer);

        foreach (Collider2D overlap in overlaps)
        {
            IDamageable damageable = overlap.GetComponentInParent<IDamageable>();
            Component targetComponent = damageable as Component;
            if (damageable == null || targetComponent == null)
                continue;

            if (!hitTargets.Add(targetComponent))
                continue;

            damageable.TakeHit(new HitData
            {
                damage = attackDamage,
                knockbackForce = attackKnockback,
                sourcePosition = attackPoint.position,
                applyMark = false
            });
        }
    }

    public virtual void TakeHit(HitData hitData)
    {
        if (isDead || hitData.damage <= 0f)
            return;

        Vector2 knockbackDirection = ((Vector2)transform.position - hitData.sourcePosition).normalized;
        if (knockbackDirection.sqrMagnitude < 0.01f)
            knockbackDirection = Vector2.up;

        currentHealth = Mathf.Max(0f, currentHealth - hitData.damage);
        movementLockedUntil = Time.time + knockbackMovementLock;

        if (DamageTextManager.Instance != null)
            DamageTextManager.Instance.Show(transform.position, hitData.damage, knockbackDirection);

        if (body != null && hitData.knockbackForce > 0f)
            body.AddForce(knockbackDirection * hitData.knockbackForce, ForceMode2D.Impulse);

        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
            animator.SetTrigger(hitTrigger);

        if (hitData.applyMark && currentHealth > 0f)
            ApplyMark();

        RefreshMarkerSprite();
        HealthChanged?.Invoke(this);

        if (currentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        if (isDead)
            return;

        isDead = true;
        attackHitboxEnabled = false;
        RemoveMark();

        if (body != null)
            body.linearVelocity = Vector2.zero;

        foreach (Collider2D enemyCollider in GetComponentsInChildren<Collider2D>())
            enemyCollider.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        Died?.Invoke(this);
        Destroy(gameObject, deathDestroyDelay);
    }

    protected virtual void ApplyMark()
    {
        HasMark = true;

        if (markCoroutine != null)
            StopCoroutine(markCoroutine);
        markCoroutine = StartCoroutine(MarkTimer());

        if (markerInstance == null && markerPrefab != null)
        {
            markerInstance = Instantiate(markerPrefab, transform);
            markerInstance.transform.localPosition = markerLocalOffset;
        }

        RefreshMarkerSprite();
    }

    public virtual void RemoveMark()
    {
        HasMark = false;

        if (markCoroutine != null)
        {
            StopCoroutine(markCoroutine);
            markCoroutine = null;
        }

        if (markerInstance != null)
        {
            Destroy(markerInstance.gameObject);
            markerInstance = null;
        }
    }

    private IEnumerator MarkTimer()
    {
        yield return new WaitForSeconds(markDuration);
        markCoroutine = null;
        RemoveMark();
    }

    private void RefreshMarkerSprite()
    {
        if (!HasMark || markerInstance == null)
            return;

        float finisherDamage = playerSkill != null
            ? playerSkill.FinisherDamage
            : finisherDamageFallback;
        markerInstance.sprite = currentHealth <= finisherDamage
            ? finishableMarkSprite
            : unavailableMarkSprite;
    }

    private void ScheduleDirectionChange()
    {
        float minimum = Mathf.Min(directionChangeInterval.x, directionChangeInterval.y);
        float maximum = Mathf.Max(directionChangeInterval.x, directionChangeInterval.y);
        nextDirectionChangeTime = Time.time + UnityEngine.Random.Range(minimum, maximum);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(attackPoint.position, attackDetectionRange);

            Gizmos.color = Color.red;
            if (attackHitboxShape == HitboxShape.Box)
            {
                Matrix4x4 previous = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(
                    attackPoint.position,
                    attackPoint.rotation,
                    Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, attackBoxSize);
                Gizmos.matrix = previous;
            }
            else
            {
                Gizmos.DrawWireSphere(attackPoint.position, attackCircleRadius);
            }
        }

        Gizmos.color = Color.green;
        Vector3 left = transform.position + Vector3.left * patrolRange;
        Vector3 right = transform.position + Vector3.right * patrolRange;
        Gizmos.DrawLine(left, right);
    }
}
