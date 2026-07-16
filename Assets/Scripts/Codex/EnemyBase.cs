using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class EnemyBase : MonoBehaviour, IDamageable, IEnemy, IHealthTarget
{
    private const float AttackFallbackDelay = 0.1f;
    private const float MoveAnimationThreshold = 0.05f;

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
    [SerializeField] protected LayerMask wallLayer;
    [SerializeField] protected LayerMask playerLayer;

    [Header("Health")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected string hitTrigger = "Hit";
    [SerializeField] protected string deathTrigger = "Death";
    [SerializeField] protected float knockbackMovementLock = 0.18f;

    [Header("Death Visual")]
    [SerializeField] private float deathAnimationHold = 0.25f;
    [SerializeField] private float deathFadeDuration = 0.65f;
    [SerializeField] private GameObject deathParticlePrefab;
    [SerializeField] private Vector3 deathParticleOffset;
    [SerializeField] private float deathParticleLifetime = 2f;

    [Header("Patrol")]
    [SerializeField] protected float moveSpeed = 2f;
    [SerializeField] protected float patrolRange = 4f;
    [SerializeField] protected float groundAheadDistance = 1.2f;

    [Header("Chase")]
    [SerializeField] protected float playerDetectionRange = 6f;
    [SerializeField] protected float loseTargetRange = 8f;
    [SerializeField] protected float chaseSpeed = 3.5f;

    [Header("Wall Turn")]
    [SerializeField] protected float wallCheckDistance = 0.15f;
    [SerializeField] protected float wallTurnPauseDuration = 0.45f;
    [SerializeField] protected float wallRetreatDuration = 0.8f;

    [Header("Attack")]
    [SerializeField] protected float attackDetectionRange = 2f;
    [SerializeField] protected float attackCooldown = 1.2f;
    [SerializeField] protected string attackTrigger = "Attack";
    [SerializeField] private HitboxShape attackHitboxShape = HitboxShape.Box;
    [SerializeField] protected Vector2 attackBoxSize = new Vector2(1.5f, 1f);
    [SerializeField] protected float attackCircleRadius = 0.75f;
    [SerializeField] protected float attackDamage = 15f;
    [SerializeField] protected float attackKnockback = 6f;

    [Header("Animation")]
    [SerializeField] private string moveBool = "Move";

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
    private float nextAttackTime;
    private float movementLockedUntil;
    private float wallPauseUntil;
    private float wallRetreatUntil;
    private float pendingAttackHitTime = -1f;
    private bool attackHitboxEnabled;
    private bool attackDamageApplied;
    private bool isDead;
    private Collider2D bodyCollider;
    private PlayerHealth playerTarget;
    private Coroutine markCoroutine;
    private SpriteRenderer markerInstance;
    private Sequence deathSequence;

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
        bodyCollider = GetComponent<Collider2D>();

        if (wallLayer.value == 0)
            wallLayer = LayerMask.GetMask("Wall");

        if (playerSkill == null)
            playerSkill = FindAnyObjectByType<PlayerSkill>();
    }

    protected virtual void Update()
    {
        bool isMoving = !isDead &&
            body != null &&
            Mathf.Abs(body.linearVelocity.x) > MoveAnimationThreshold;
        SetMoveAnimation(isMoving);
    }

    protected virtual void FixedUpdate()
    {
        if (isDead || body == null)
            return;

        if (attackHitboxEnabled)
            ScanAttackHitbox();

        ResolvePendingAttack();

        UpdatePlayerTarget();

        if (Time.time < movementLockedUntil)
            return;

        if (Time.time < wallPauseUntil)
        {
            StopHorizontalMovement();
            return;
        }

        if (Time.time < wallRetreatUntil)
        {
            MoveHorizontal(patrolDirection, moveSpeed);
            return;
        }

        if (playerTarget != null)
            ChasePlayer();
        else
            Patrol();
    }

    protected virtual void Patrol()
    {
        float distanceFromSpawn = transform.position.x - spawnX;
        if (distanceFromSpawn >= patrolRange)
            patrolDirection = -1f;
        else if (distanceFromSpawn <= -patrolRange)
            patrolDirection = 1f;

        FaceDirection(patrolDirection);

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

        MoveHorizontal(patrolDirection, moveSpeed);
    }

    protected virtual void ChasePlayer()
    {
        if (playerTarget == null)
            return;

        Vector2 offset = playerTarget.transform.position - transform.position;
        float chaseDirection = Mathf.Sign(offset.x);
        if (Mathf.Approximately(chaseDirection, 0f))
            chaseDirection = patrolDirection;

        FaceDirection(chaseDirection);
        if (offset.magnitude <= attackDetectionRange)
        {
            StopHorizontalMovement();
            DetectPlayerForAttack();
            return;
        }

        MoveHorizontal(chaseDirection, chaseSpeed);
    }

    protected virtual void UpdatePlayerTarget()
    {
        if (playerTarget != null)
        {
            float distance = Vector2.Distance(transform.position, playerTarget.transform.position);
            if (playerTarget.IsDead || !playerTarget.gameObject.activeInHierarchy ||
                distance > Mathf.Max(playerDetectionRange, loseTargetRange))
            {
                playerTarget = null;
            }
        }

        if (playerTarget != null)
            return;

        Collider2D detectedPlayer = Physics2D.OverlapCircle(
            transform.position,
            playerDetectionRange,
            playerLayer);
        if (detectedPlayer != null)
            playerTarget = detectedPlayer.GetComponentInParent<PlayerHealth>();
    }

    private void MoveHorizontal(float direction, float speed)
    {
        direction = direction >= 0f ? 1f : -1f;
        if (TryFindWallAhead(direction))
        {
            BeginWallTurn(direction);
            return;
        }

        FaceDirection(direction);
        Vector2 velocity = body.linearVelocity;
        velocity.x = direction * speed;
        body.linearVelocity = velocity;
    }

    private bool TryFindWallAhead(float direction)
    {
        Vector2 castDirection = direction >= 0f ? Vector2.right : Vector2.left;
        Vector2 origin = body.position;
        Vector2 castSize = new Vector2(0.1f, 0.1f);

        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            origin = bounds.center;
            castSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x * 0.9f),
                Mathf.Max(0.05f, bounds.size.y * 0.75f));
        }

        return Physics2D.BoxCast(
            origin,
            castSize,
            0f,
            castDirection,
            wallCheckDistance,
            wallLayer).collider != null;
    }

    private void BeginWallTurn(float blockedDirection)
    {
        patrolDirection = blockedDirection > 0f ? -1f : 1f;
        wallPauseUntil = Time.time + Mathf.Max(0f, wallTurnPauseDuration);
        wallRetreatUntil = wallPauseUntil + Mathf.Max(0f, wallRetreatDuration);
        playerTarget = null;
        StopHorizontalMovement();
        FaceDirection(patrolDirection);
    }

    private void StopHorizontalMovement()
    {
        Vector2 velocity = body.linearVelocity;
        velocity.x = 0f;
        body.linearVelocity = velocity;
    }

    private void SetMoveAnimation(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(moveBool))
            animator.SetBool(moveBool, value);
    }

    private void FaceDirection(float direction)
    {
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (direction >= 0f ? 1f : -1f);
        transform.localScale = scale;
    }

    protected virtual void DetectPlayerForAttack()
    {
        if (playerTarget == null || Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;
        movementLockedUntil = Mathf.Max(movementLockedUntil, Time.time + attackCooldown * 0.5f);
        pendingAttackHitTime = Time.time + AttackFallbackDelay;
        attackDamageApplied = false;
        attackHitboxEnabled = false;
        hitTargets.Clear();

        if (animator != null && !string.IsNullOrEmpty(attackTrigger))
            animator.SetTrigger(attackTrigger);
    }

    public virtual void EnableAttackHitbox()
    {
        attackHitboxEnabled = true;
        if (!attackDamageApplied)
            ScanAttackHitbox();
    }

    public virtual void DisableAttackHitbox()
    {
        attackHitboxEnabled = false;
        hitTargets.Clear();
    }

    protected virtual void ScanAttackHitbox()
    {
        if (attackPoint == null || attackDamageApplied)
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

            ApplyAttackDamage(damageable, attackPoint.position);
            break;
        }
    }

    private void ResolvePendingAttack()
    {
        if (pendingAttackHitTime < 0f || Time.time < pendingAttackHitTime)
            return;

        pendingAttackHitTime = -1f;
        if (attackDamageApplied)
            return;

        ScanAttackHitbox();
        if (attackDamageApplied || playerTarget == null || playerTarget.IsDead)
            return;

        float distance = Vector2.Distance(transform.position, playerTarget.transform.position);
        if (distance <= attackDetectionRange + 0.25f)
            ApplyAttackDamage(playerTarget, transform.position);
    }

    private void ApplyAttackDamage(IDamageable damageable, Vector2 sourcePosition)
    {
        attackDamageApplied = true;
        damageable.TakeHit(new HitData
        {
            damage = attackDamage,
            knockbackForce = attackKnockback,
            sourcePosition = sourcePosition,
            applyMark = false
        });
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
        SetMoveAnimation(false);
        attackHitboxEnabled = false;
        pendingAttackHitTime = -1f;
        RemoveMark();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.simulated = false;
        }

        foreach (Collider2D enemyCollider in GetComponentsInChildren<Collider2D>())
            enemyCollider.enabled = false;

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        if (deathParticlePrefab != null)
        {
            GameObject particleObject = Instantiate(
                deathParticlePrefab,
                transform.position + deathParticleOffset,
                Quaternion.identity);
            foreach (ParticleSystem particle in particleObject.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particle.main;
                main.useUnscaledTime = true;
                particle.Clear(true);
                particle.Play(true);
            }

            Destroy(particleObject, deathParticleLifetime);
        }

        Died?.Invoke(this);
        PlayDeathFade();
    }

    private void PlayDeathFade()
    {
        deathSequence?.Kill();
        deathSequence = DOTween.Sequence().SetUpdate(true);
        deathSequence.AppendInterval(Mathf.Max(0f, deathAnimationHold));

        Tween scaleTween = transform.DOScale(Vector3.zero, Mathf.Max(0.01f, deathFadeDuration))
            .SetEase(Ease.InQuad);
        deathSequence.Append(scaleTween);

        foreach (SpriteRenderer spriteRenderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            deathSequence.Join(
                spriteRenderer.DOFade(0f, deathFadeDuration)
                    .SetEase(Ease.InQuad));
        }

        foreach (MeshRenderer meshRenderer in GetComponentsInChildren<MeshRenderer>(true))
        {
            Material material = meshRenderer.material;
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", 5f);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", 10f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;

            if (material.HasProperty("_BaseColor"))
            {
                deathSequence.Join(
                    material.DOFade(0f, "_BaseColor", deathFadeDuration)
                        .SetEase(Ease.InQuad));
            }
            else if (material.HasProperty("_Color"))
            {
                deathSequence.Join(
                    material.DOFade(0f, deathFadeDuration)
                        .SetEase(Ease.InQuad));
            }
        }

        deathSequence.OnComplete(() =>
        {
            deathSequence = null;
            Destroy(gameObject);
        });
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

    private System.Collections.IEnumerator MarkTimer()
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

    protected virtual void OnDestroy()
    {
        deathSequence?.Kill();
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, playerDetectionRange);

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
