using UnityEngine;

public class InputManager : Singleton<InputManager>
{
    public bool InputAllowed { get; private set; } = true;

    [SerializeField] private InputKeys playerInputs = new InputKeys();

    public bool OnLMB() => Input.GetMouseButton(0);
    public bool OnRMB() => Input.GetMouseButton(1);
    public bool MoveLeft => Input.GetKey(playerInputs.MoveLeft);
    public bool MoveRight => Input.GetKey(playerInputs.MoveRight);
    public bool Jump => Input.GetKeyDown(playerInputs.Jump);
    public bool Dash => Input.GetKeyDown(playerInputs.Dash);
    public bool SpecialAbility => Input.GetKeyDown(playerInputs.SpecialAbility);
    public bool SpecialAbilityHeld => Input.GetKey(playerInputs.SpecialAbility);
    public bool SpecialAbilityReleased => Input.GetKeyUp(playerInputs.SpecialAbility);
    public Vector3 MousePosition => Input.mousePosition;

    public void SetInputAllowed(bool value) => InputAllowed = value;
}

[System.Serializable]
public class InputKeys
{
    [Header("Player Move")]
    public KeyCode MoveLeft = KeyCode.A;
    public KeyCode MoveRight = KeyCode.D;
    public KeyCode Jump = KeyCode.W;
    public KeyCode Dash = KeyCode.LeftShift;

    [Header("Player Ability")]
    public KeyCode SpecialAbility = KeyCode.Space;
}
