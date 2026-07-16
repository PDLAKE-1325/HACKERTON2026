using DG.Tweening;
using UnityEngine;

public class PlayerSkill : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer skillSprite;
    [SerializeField] private ResonanceStack resonanceStack;

    [Header("Target")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float teleportOffset = 1.25f;
    [SerializeField] private float finisherDamage = 30f;

    [Header("Timing")]
    [SerializeField] private float cooldown = 5f;
    [SerializeField, Range(0.01f, 1f)] private float skillTimeScale = 0.08f;
    [SerializeField] private float slowDownDuration = 0.18f;
    [SerializeField] private float restoreDuration = 0.18f;
    [SerializeField] private float spriteFadeDuration = 0.18f;

    [Header("Hit Shake")]
    [SerializeField] private float hitShakeIntensity = 2f;
    [SerializeField] private float hitShakeDuration = 0.16f;

    private bool isActive;
    private bool canAcceptTargetClick;
    private float nextUseTime;
    private Tween timeScaleTween;
    private Tween spriteTween;

    public float FinisherDamage => finisherDamage +
        (resonanceStack != null ? resonanceStack.BonusDamage : 0f);
    public bool IsActive => isActive;

    private void Update()
    {
        if (!isActive || InputManager.Instance == null)
            return;

        if (!InputManager.Instance.SpecialAbilityHeld)
        {
            FinishAndStartCooldown();
            return;
        }

        bool leftMouseHeld = InputManager.Instance.OnLMB();
        if (!leftMouseHeld)
        {
            canAcceptTargetClick = true;
            return;
        }

        if (!canAcceptTargetClick)
            return;

        canAcceptTargetClick = false;
        TryExecuteTargetAtMouse();
    }

    public void TryActivate()
    {
        if (isActive || Time.unscaledTime < nextUseTime || InputManager.Instance == null)
            return;

        isActive = true;
        canAcceptTargetClick = !InputManager.Instance.OnLMB();
        InputManager.Instance.SetInputAllowed(false);
        if (resonanceStack != null)
            resonanceStack.SetSkillActive(true);

        timeScaleTween?.Kill();
        timeScaleTween = DOTween.To(
                () => Time.timeScale,
                value => Time.timeScale = value,
                skillTimeScale,
                slowDownDuration)
            .SetUpdate(true)
            .SetEase(Ease.OutQuad);

        if (skillSprite != null)
        {
            skillSprite.gameObject.SetActive(true);
            skillSprite.color = new Color(0f, 0f, 0f, 0f);

            spriteTween?.Kill();
            spriteTween = skillSprite.DOFade(1f, spriteFadeDuration)
                .SetUpdate(true);
        }
    }

    private void TryExecuteTargetAtMouse()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null)
            return;

        Vector3 mouseWorld = cameraToUse.ScreenToWorldPoint(InputManager.Instance.MousePosition);
        Collider2D targetCollider = Physics2D.OverlapPoint(mouseWorld, enemyLayer);
        if (targetCollider == null)
            return;

        IEnemy enemy = targetCollider.GetComponentInParent<IEnemy>();
        IDamageable damageable = enemy as IDamageable;
        IHealthTarget healthTarget = enemy as IHealthTarget;
        Component enemyComponent = enemy as Component;
        if (enemy == null || damageable == null || healthTarget == null || enemyComponent == null)
            return;

        bool hadMark = enemy.HasMark;
        enemy.RemoveMark();
        TeleportAcross(enemyComponent.transform);

        HitData hitData = new HitData
        {
            damage = FinisherDamage,
            knockbackForce = 0f,
            sourcePosition = transform.position,
            applyMark = false
        };

        damageable.TakeHit(hitData);
        bool killedTarget = healthTarget.IsDead;
        if (resonanceStack != null)
            resonanceStack.ResolveSkillHit(killedTarget);

        if (CameraManager.Instance != null)
            CameraManager.Instance.Shake(hitShakeIntensity, hitShakeDuration);

        if (!killedTarget || !hadMark)
            FinishAndStartCooldown();
    }

    private void TeleportAcross(Transform enemyTransform)
    {
        float currentSide = Mathf.Sign(transform.position.x - enemyTransform.position.x);
        if (Mathf.Approximately(currentSide, 0f))
            currentSide = 1f;

        Vector3 destination = enemyTransform.position;
        destination.x -= currentSide * teleportOffset;
        destination.z = transform.position.z;
        transform.position = destination;
    }

    private void FinishAndStartCooldown()
    {
        if (!isActive)
            return;

        isActive = false;
        nextUseTime = Time.unscaledTime + cooldown;
        if (resonanceStack != null)
            resonanceStack.SetSkillActive(false);

        timeScaleTween?.Kill();
        timeScaleTween = DOTween.To(
                () => Time.timeScale,
                value => Time.timeScale = value,
                1f,
                restoreDuration)
            .SetUpdate(true)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (InputManager.Instance != null)
                    InputManager.Instance.SetInputAllowed(true);
            });

        if (skillSprite != null)
        {
            spriteTween?.Kill();
            spriteTween = skillSprite.DOFade(0f, spriteFadeDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (skillSprite != null)
                        skillSprite.gameObject.SetActive(false);
                });
        }
    }

    private void OnDestroy()
    {
        timeScaleTween?.Kill();
        spriteTween?.Kill();

        if (!isActive)
            return;

        isActive = false;
        Time.timeScale = 1f;
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputAllowed(true);
        if (resonanceStack != null)
            resonanceStack.SetSkillActive(false);
    }
}
