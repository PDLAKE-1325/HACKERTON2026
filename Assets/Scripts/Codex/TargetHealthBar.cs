using UnityEngine;
using UnityEngine.UI;

public class TargetHealthBar : MonoBehaviour
{
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private float hideDelay = 3f;

    private IHealthTarget currentTarget;
    private float hideAt;

    private void Awake()
    {
        SetVisible(false);
    }

    private void Update()
    {
        if (currentTarget != null && Time.unscaledTime >= hideAt)
            Hide();
    }

    public void Show(IHealthTarget target)
    {
        if (target == null || target.IsDead || healthSlider == null)
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
        healthSlider.maxValue = target.MaxHealth;
        healthSlider.value = target.CurrentHealth;
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
