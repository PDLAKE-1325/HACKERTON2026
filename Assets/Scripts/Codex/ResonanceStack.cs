using UnityEngine;
using UnityEngine.UI;

public class ResonanceStack : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float bonusDamagePerStack = 10f;

    [Header("Reset")]
    [SerializeField] private float resetDelay = 4f;

    [Header("UI")]
    [SerializeField] private Text stackText;
    [SerializeField] private string labelFormat = "공명 x{0}";

    private int currentStack;
    private float resetTimeRemaining;
    private bool skillActive;

    public int CurrentStack => currentStack;
    public float BonusDamage => currentStack * bonusDamagePerStack;

    private void Start()
    {
        RefreshUI();
    }

    private void Update()
    {
        if (currentStack <= 0 || skillActive)
            return;

        resetTimeRemaining -= Time.unscaledDeltaTime;
        if (resetTimeRemaining <= 0f)
            Clear();
    }

    public void AddFromMeleeHit()
    {
        currentStack++;
        resetTimeRemaining = Mathf.Max(0f, resetDelay);
        RefreshUI();
    }

    public void SetSkillActive(bool value)
    {
        skillActive = value;
    }

    public void ResolveSkillHit(bool killedTarget)
    {
        if (!killedTarget)
            Clear();
    }

    public void Clear()
    {
        currentStack = 0;
        resetTimeRemaining = 0f;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (stackText != null)
            stackText.text = string.Format(labelFormat, currentStack);
    }
}
