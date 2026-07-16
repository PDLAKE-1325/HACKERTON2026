using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour, IDamageable, IHealthTarget
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private GameObject gameOverSprite;

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Death")]
    [SerializeField] private float deathSlowDuration = 0.8f;
    [SerializeField] private float gameOverShakeIntensity = 2.5f;
    [SerializeField] private float gameOverShakeDuration = 0.3f;

    private float currentHealth;
    private bool isDead;
    private Tween deathTween;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    public event Action<IHealthTarget> HealthChanged;
    public event Action<IHealthTarget> Died;

    private void Awake()
    {
        currentHealth = maxHealth;
        CacheFillImage();
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        UpdateHealthUi();

        if (gameOverSprite != null)
            gameOverSprite.SetActive(false);
    }

    public void TakeHit(HitData hitData)
    {
        if (isDead || hitData.damage <= 0f)
            return;

        Vector2 knockbackDirection = ((Vector2)transform.position - hitData.sourcePosition).normalized;
        if (knockbackDirection.sqrMagnitude < 0.01f)
            knockbackDirection = Vector2.up;

        currentHealth = Mathf.Max(0f, currentHealth - hitData.damage);
        UpdateHealthUi();
        HealthChanged?.Invoke(this);

        if (DamageTextManager.Instance != null)
            DamageTextManager.Instance.Show(transform.position, hitData.damage, knockbackDirection);

        if (body != null && hitData.knockbackForce > 0f)
            body.AddForce(knockbackDirection * hitData.knockbackForce, ForceMode2D.Impulse);

        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
            animator.SetTrigger(hitTrigger);

        if (currentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        Died?.Invoke(this);

        if (InputManager.Instance != null)
            InputManager.Instance.SetInputAllowed(false);

        if (movement != null)
            movement.StopImmediately();

        if (animator != null && !string.IsNullOrEmpty(deathTrigger))
            animator.SetTrigger(deathTrigger);

        deathTween?.Kill();
        deathTween = DOTween.To(
                () => Time.timeScale,
                value => Time.timeScale = value,
                0f,
                deathSlowDuration)
            .SetUpdate(true)
            .SetEase(Ease.InQuad)
            .OnComplete(ShowGameOver);
    }

    private void ShowGameOver()
    {
        if (gameOverSprite != null)
            gameOverSprite.SetActive(true);

        if (CameraManager.Instance != null)
            CameraManager.Instance.Shake(gameOverShakeIntensity, gameOverShakeDuration);
    }

    private void UpdateHealthUi()
    {
        if (healthSlider != null)
            healthSlider.value = currentHealth;

        float normalizedHealth = maxHealth > 0f
            ? Mathf.Clamp01(currentHealth / maxHealth)
            : 0f;
        UpdateFillImage(normalizedHealth);
    }

    private void CacheFillImage()
    {
        if (healthSlider != null)
        {
            if (healthFillImage == null && healthSlider.fillRect != null)
                healthFillImage = healthSlider.fillRect.GetComponent<Image>();

            healthSlider.fillRect = null;
        }

        if (healthFillImage == null)
            return;

        healthFillImage.type = Image.Type.Simple;
    }

    private void UpdateFillImage(float normalizedHealth)
    {
        if (healthFillImage == null)
            return;

        healthFillImage.enabled = normalizedHealth > 0f;

        RectTransform fillRect = healthFillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(normalizedHealth, 1f);
        fillRect.offsetMin = new Vector2(4f, 4f);
        fillRect.offsetMax = new Vector2(-4f, -4f);
    }

    private void OnDestroy()
    {
        deathTween?.Kill();
    }
}
