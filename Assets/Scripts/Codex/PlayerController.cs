using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private PlayerSkill skill;

    private void Update()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        if (skill != null && input.SpecialAbility)
            skill.Toggle();

        if (!input.InputAllowed)
        {
            if (movement != null)
                movement.SetMoveInput(0f);
            return;
        }

        float horizontal = 0f;
        if (input.MoveLeft)
            horizontal -= 1f;
        if (input.MoveRight)
            horizontal += 1f;

        if (movement != null)
        {
            movement.SetMoveInput(horizontal);

            if (input.Jump)
                movement.TryJump();

            if (input.Dash)
                movement.TryDash();
        }

        if (combat != null)
        {
            if (input.OnLMB())
                combat.TryMeleeAttack();

            if (input.OnRMB())
                combat.TryRangedAttack();
        }
    }

    private void OnDisable()
    {
        if (movement != null)
            movement.SetMoveInput(0f);
    }
}
