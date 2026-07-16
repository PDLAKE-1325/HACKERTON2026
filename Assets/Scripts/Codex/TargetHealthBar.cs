using UnityEngine;
using UnityEngine.UI;

public class TargetHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Image healthFillImage;
    [SerializeField] private float hideDelay = 3f;

    private IHealthTarget currentTarget;
    private float hideAt;

    private void Awake()
    {
        CacheFillImage();
        SetVisible(false);
    }

    private void Update()
    {
        if (currentTarget != null && Time.unscaledTime >= hideAt)
            Hide();
    }

    public void Show(IHealthTarget target)
    {
        if (target == null || target.IsDead ||
            (healthSlider == null && healthFillImage == null))
            return;

        if (currentTarget != target)
        {
            UnsubscribeCurrentTarget();
            currentTarget = target;
            currentTarget.HealthChanged += HandleHealthChanged;
            currentTarget.Died += HandleTargetDied;
        }

        Refresh(target);
        hideAt = Time.unscaledTime + hideDelay;
        SetVisible(true);
    }

    public void Hide()
    {
        UnsubscribeCurrentTarget();
        currentTarget = null;
        SetVisible(false);
    }

    private void HandleHealthChanged(IHealthTarget target)
    {
        if (target == currentTarget)
            Refresh(target);
    }

    private void HandleTargetDied(IHealthTarget target)
    {
        if (target == currentTarget)
            Hide();
    }

    private void Refresh(IHealthTarget target)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = target.MaxHealth;
            healthSlider.value = target.CurrentHealth;
        }

        float normalizedHealth = target.MaxHealth > 0f
            ? Mathf.Clamp01(target.CurrentHealth / target.MaxHealth)
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

    private void UnsubscribeCurrentTarget()
    {
        if (currentTarget == null)
            return;

        currentTarget.HealthChanged -= HandleHealthChanged;
        currentTarget.Died -= HandleTargetDied;
    }

    private void SetVisible(bool visible)
    {
        GameObject root = healthBarRoot != null ? healthBarRoot :
            (healthSlider != null ? healthSlider.gameObject : null);
        if (root != null)
            root.SetActive(visible);
    }

    private void OnDestroy()
    {
        UnsubscribeCurrentTarget();
    }
}
