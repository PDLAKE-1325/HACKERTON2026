using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    private enum HitboxShape
    {
        Box,
        Circle
    }

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform meleeAttackPoint;
    [SerializeField] private Transform muzzle;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private TargetHealthBar targetHealthBar;

    [Header("Enemy Layer")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private LayerMask rangedHitLayers;

    [Header("Melee")]
    [SerializeField] private HitboxShape meleeHitboxShape = HitboxShape.Box;
    [SerializeField] private Vector2 meleeBoxSize = new Vector2(1.6f, 1f);
    [SerializeField] private float meleeCircleRadius = 0.8f;
    [SerializeField] private float meleeDamage = 20f;
    [SerializeField] private float meleeKnockback = 8f;
    [SerializeField] private float meleeCooldown = 0.4f;
    [SerializeField] private string meleeTrigger = "Melee";
    [SerializeField] private float meleeHitShakeIntensity = 1f;
    [SerializeField] private float meleeHitShakeDuration = 0.12f;

    [Header("Ranged Hitscan")]
    [SerializeField] private float rangedDamage = 12f;
    [SerializeField] private float rangedKnockback = 5f;
    [SerializeField] private float rangedDistance = 30f;
    [SerializeField] private float rangedCooldown = 0.45f;
    [SerializeField] private string rangedTrigger = "Shoot";
    [SerializeField] private float fireShakeIntensity = 0.65f;
    [SerializeField] private float fireShakeDuration = 0.08f;
    [SerializeField] private float hitShakeIntensity = 1.2f;
    [SerializeField] private float hitShakeDuration = 0.12f;

    [Header("Ranged Visuals")]
    [SerializeField] private LineRenderer bulletTrailPrefab;
    [SerializeField] private float bulletTrailSpeed = 45f;
    [SerializeField] private float trailRemainDuration = 0.05f;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private float muzzleFlashDuration = 0.12f;
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 0.4f;

    private readonly HashSet<Component> hitTargets = new HashSet<Component>();
    private bool meleeHitboxEnabled;
    private float nextMeleeTime;
    private float nextRangedTime;

    private void Update()
    {
        if (meleeHitboxEnabled)
            ScanMeleeHitbox();
    }

    public void TryMeleeAttack()
    {
        if (Time.time < nextMeleeTime)
            return;

        nextMeleeTime = Time.time + meleeCooldown;
        if (animator != null && !string.IsNullOrEmpty(meleeTrigger))
            animator.SetTrigger(meleeTrigger);
    }

    public void EnableAttackHitbox()
    {
        hitTargets.Clear();
        meleeHitboxEnabled = true;
        ScanMeleeHitbox();
    }

    public void DisableAttackHitbox()
    {
        meleeHitboxEnabled = false;
        hitTargets.Clear();
    }

    public void TryRangedAttack()
    {
        if (Time.time < nextRangedTime || muzzle == null || InputManager.Instance == null)
            return;

        nextRangedTime = Time.time + rangedCooldown;
        if (animator != null && !string.IsNullOrEmpty(rangedTrigger))
            animator.SetTrigger(rangedTrigger);

        Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
        if (cameraToUse == null)
            return;

        Vector3 mouseWorld = cameraToUse.ScreenToWorldPoint(InputManager.Instance.MousePosition);
        Vector2 start = muzzle.position;
        Vector2 direction = ((Vector2)mouseWorld - start).normalized;
        if (direction.sqrMagnitude < 0.01f)
            direction = transform.right;

        RaycastHit2D hit = Physics2D.Raycast(start, direction, rangedDistance, rangedHitLayers);
        Vector2 end = hit.collider != null ? hit.point : start + direction * rangedDistance;

        SpawnMuzzleFlash();
        SpawnBulletTrail(start, end);
        ShakeCamera(fireShakeIntensity, fireShakeDuration);

        if (hit.collider == null)
            return;

        SpawnHitEffect(hit.point);

        IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
        if (damageable == null)
            return;

        HitData hitData = new HitData
        {
            damage = rangedDamage,
            knockbackForce = rangedKnockback,
            sourcePosition = start,
            applyMark = true
        };

        damageable.TakeHit(hitData);
        ShowTargetHealth(damageable);
        ShakeCamera(hitShakeIntensity, hitShakeDuration);
    }

    private void ScanMeleeHitbox()
    {
        if (meleeAttackPoint == null)
            return;

        Collider2D[] overlaps = meleeHitboxShape == HitboxShape.Box
            ? Physics2D.OverlapBoxAll(
                meleeAttackPoint.position,
                meleeBoxSize,
                meleeAttackPoint.eulerAngles.z,
                enemyLayer)
            : Physics2D.OverlapCircleAll(
                meleeAttackPoint.position,
                meleeCircleRadius,
                enemyLayer);

        foreach (Collider2D overlap in overlaps)
        {
            IDamageable damageable = overlap.GetComponentInParent<IDamageable>();
            Component targetComponent = damageable as Component;
            if (damageable == null || targetComponent == null)
                continue;

            if (!hitTargets.Add(targetComponent))
                continue;

            HitData hitData = new HitData
            {
                damage = meleeDamage,
                knockbackForce = meleeKnockback,
                sourcePosition = meleeAttackPoint.position,
                applyMark = false
            };

            damageable.TakeHit(hitData);
            ShowTargetHealth(damageable);
            ShakeCamera(meleeHitShakeIntensity, meleeHitShakeDuration);
        }
    }

    private void ShowTargetHealth(IDamageable damageable)
    {
        if (targetHealthBar != null && damageable is IHealthTarget healthTarget)
            targetHealthBar.Show(healthTarget);
    }

    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null || muzzle == null)
            return;

        GameObject effect = Instantiate(muzzleFlashPrefab, muzzle.position, muzzle.rotation);
        Destroy(effect, muzzleFlashDuration);
    }

    private void SpawnHitEffect(Vector2 position)
    {
        if (hitEffectPrefab == null)
            return;

        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        Destroy(effect, hitEffectDuration);
    }

    private void SpawnBulletTrail(Vector2 start, Vector2 end)
    {
        if (bulletTrailPrefab == null)
            return;

        LineRenderer trail = Instantiate(bulletTrailPrefab, start, Quaternion.identity);
        trail.positionCount = 2;
        trail.SetPosition(0, start);
        trail.SetPosition(1, start);

        float distance = Vector2.Distance(start, end);
        float travelDuration = bulletTrailSpeed > 0f ? distance / bulletTrailSpeed : 0f;
        DOVirtual.Float(0f, 1f, travelDuration, progress =>
        {
            if (trail != null)
                trail.SetPosition(1, Vector2.Lerp(start, end, progress));
        }).SetEase(Ease.Linear).OnComplete(() =>
        {
            if (trail != null)
                Destroy(trail.gameObject, trailRemainDuration);
        });
    }

    private void ShakeCamera(float intensity, float duration)
    {
        if (CameraManager.Instance != null)
            CameraManager.Instance.Shake(intensity, duration);
    }

    private void OnDrawGizmosSelected()
    {
        if (meleeAttackPoint == null)
            return;

        Gizmos.color = Color.red;
        if (meleeHitboxShape == HitboxShape.Box)
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                meleeAttackPoint.position,
                meleeAttackPoint.rotation,
                Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, meleeBoxSize);
            Gizmos.matrix = previous;
        }
        else
        {
            Gizmos.DrawWireSphere(meleeAttackPoint.position, meleeCircleRadius);
        }
    }
}
