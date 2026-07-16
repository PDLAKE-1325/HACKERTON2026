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

    [Header("Audio")]
    [SerializeField] private AudioClip slowAttackSound;

    private bool isActive;
    private bool canAcceptTargetClick;
    private float nextUseTime;
    private Tween timeScaleTween;
    private Tween spriteTween;

    public float FinisherDamage
    {
        get
        {
            ResonanceStack activeStack = GetActiveResonanceStack();
            return activeStack != null
                ? activeStack.CalculateSkillDamage(finisherDamage)
                : Mathf.Max(0f, finisherDamage);
        }
    }
    public bool IsActive => isActive;
    public float CooldownDuration => Mathf.Max(0f, cooldown);
    public float CooldownRemaining => Mathf.Max(0f, nextUseTime - Time.unscaledTime);
    public float CooldownProgress => CooldownDuration <= 0f
        ? 1f
        : 1f - CooldownRemaining / CooldownDuration;

    private void Awake()
    {
        GetActiveResonanceStack();
    }

    private ResonanceStack GetActiveResonanceStack()
    {
        ResonanceStack localStack = GetComponent<ResonanceStack>();
        if (localStack != null)
            resonanceStack = localStack;

        return resonanceStack;
    }

    private void Update()
    {
        if (!isActive || InputManager.Instance == null)
            return;

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

    public void Toggle()
    {
        if (isActive)
        {
            FinishAndStartCooldown();
            return;
        }

        TryActivate();
    }

    public void TryActivate()
    {
        if (isActive || Time.unscaledTime < nextUseTime || InputManager.Instance == null)
            return;

        isActive = true;
        canAcceptTargetClick = !InputManager.Instance.OnLMB();
        InputManager.Instance.SetInputAllowed(false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMuffled(true);
        ResonanceStack activeStack = GetActiveResonanceStack();
        if (activeStack != null)
            activeStack.SetSkillActive(true);

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
        ResonanceStack activeStack = GetActiveResonanceStack();
        float totalDamage = activeStack != null
            ? activeStack.CalculateSkillDamage(finisherDamage)
            : Mathf.Max(0f, finisherDamage);
        enemy.RemoveMark();
        TeleportAcross(enemyComponent.transform);

        HitData hitData = new HitData
        {
            damage = totalDamage,
            knockbackForce = 0f,
            sourcePosition = transform.position,
            applyMark = false
        };

        damageable.TakeHit(hitData);
        if (slowAttackSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(slowAttackSound);
        bool killedTarget = healthTarget.IsDead;
        if (activeStack != null)
            activeStack.ResolveSkillHit(killedTarget);

        if (CameraManager.Instance != null)
            CameraManager.Instance.Shake(hitShakeIntensity, hitShakeDuration);

        bool canContinue = killedTarget &&
            hadMark &&
            HasLivingEnemyInView(cameraToUse, enemyComponent);
        if (!canContinue)
            FinishAndStartCooldown();
    }

    private bool HasLivingEnemyInView(Camera cameraToUse, Component defeatedEnemy)
    {
        float targetDepth = Mathf.Abs(cameraToUse.transform.position.z - transform.position.z);
        Vector3 bottomLeft = cameraToUse.ViewportToWorldPoint(new Vector3(0f, 0f, targetDepth));
        Vector3 topRight = cameraToUse.ViewportToWorldPoint(new Vector3(1f, 1f, targetDepth));
        Vector2 center = (bottomLeft + topRight) * 0.5f;
        Vector2 size = new Vector2(
            Mathf.Abs(topRight.x - bottomLeft.x),
            Mathf.Abs(topRight.y - bottomLeft.y));

        Collider2D[] candidates = Physics2D.OverlapBoxAll(center, size, 0f, enemyLayer);
        foreach (Collider2D candidate in candidates)
        {
            IEnemy enemy = candidate.GetComponentInParent<IEnemy>();
            IHealthTarget healthTarget = enemy as IHealthTarget;
            Component enemyComponent = enemy as Component;
            if (enemyComponent == null ||
                enemyComponent == defeatedEnemy ||
                healthTarget == null ||
                healthTarget.IsDead ||
                !enemyComponent.gameObject.activeInHierarchy)
            {
                continue;
            }

            return true;
        }

        return false;
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
        ResonanceStack activeStack = GetActiveResonanceStack();
        if (activeStack != null)
            activeStack.SetSkillActive(false);

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
                if (AudioManager.Instance != null)
                    AudioManager.Instance.SetMuffled(false);
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
        bool hadSlowState = isActive ||
            (timeScaleTween != null && timeScaleTween.IsActive());
        timeScaleTween?.Kill();
        spriteTween?.Kill();

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMuffled(false);

        if (!hadSlowState)
            return;

        isActive = false;
        Time.timeScale = 1f;
        if (InputManager.Instance != null)
            InputManager.Instance.SetInputAllowed(true);
        ResonanceStack activeStack = GetActiveResonanceStack();
        if (activeStack != null)
            activeStack.SetSkillActive(false);
    }
}
