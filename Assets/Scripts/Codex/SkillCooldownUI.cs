using UnityEngine;
using UnityEngine.UI;

public class SkillCooldownUI : MonoBehaviour
{
    [SerializeField] private PlayerSkill playerSkill;
    [SerializeField] private Image cooldownFill;
    [SerializeField] private Text statusText;
    [SerializeField] private Color readyColor = new Color(0.15f, 0.9f, 1f, 0.9f);
    [SerializeField] private Color cooldownColor = new Color(0.15f, 0.45f, 0.65f, 0.8f);

    private void Update()
    {
        if (playerSkill == null)
            return;

        if (playerSkill.IsActive)
        {
            SetDisplay(1f, readyColor, "사용 중");
            return;
        }

        float remaining = playerSkill.CooldownRemaining;
        if (remaining > 0f)
        {
            SetDisplay(playerSkill.CooldownProgress, cooldownColor, $"{remaining:0.0}s");
            return;
        }

        SetDisplay(1f, readyColor, "준비");
    }

    private void SetDisplay(float fillAmount, Color color, string status)
    {
        if (cooldownFill != null)
        {
            cooldownFill.fillAmount = Mathf.Clamp01(fillAmount);
            cooldownFill.color = color;
        }

        if (statusText != null)
            statusText.text = status;
    }
}
