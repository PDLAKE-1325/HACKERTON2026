using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Transform wallCheckOrigin;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 9f;
    [SerializeField] private float jumpForce = 14f;

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundCheckRadius = 0.18f;

    [Header("Wall Grip")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallCheckDistance = 0.45f;
    [SerializeField] private float wallGripDuration = 0.65f;
    [SerializeField] private float wallRegrabDelay = 0.2f;
    [SerializeField] private float wallJumpHorizontalForce = 7f;
    [SerializeField] private float wallJumpVerticalForce = 13f;
    [SerializeField] private float wallReleaseFallSpeed = 3f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.16f;
    [SerializeField] private float dashCooldown = 0.8f;

    private float moveInput;
    private float defaultGravityScale;
    private float wallGripRemaining;
    private float nextDashTime;
    private float nextWallGripTime;
    private int facingDirection = 1;
    private int grippedWallSide;
    private bool isGrounded;
    private bool isWallGripping;
    private bool isDashing;
    private Coroutine dashCoroutine;

    public int FacingDirection => facingDirection;

    private void Awake()
    {
        if (body != null)
            defaultGravityScale = body.gravityScale;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        UpdateEnvironmentState();

        if (isDashing)
            return;

        if (isWallGripping)
        {
            wallGripRemaining -= Time.fixedDeltaTime;
            body.linearVelocity = Vector2.zero;

            if (wallGripRemaining <= 0f)
                ReleaseWall(false);

            return;
        }

        TryBeginWallGrip();
        if (isWallGripping)
            return;

        Vector2 velocity = body.linearVelocity;
        velocity.x = moveInput * moveSpeed;
        body.linearVelocity = velocity;
    }

    public void SetMoveInput(float value)
    {
        moveInput = Mathf.Clamp(value, -1f, 1f);
        if (Mathf.Abs(moveInput) > 0.01f)
            facingDirection = moveInput > 0f ? 1 : -1;
    }

    public void TryJump()
    {
        if (body == null || isDashing)
            return;

        if (isWallGripping)
        {
            ReleaseWall(true);
            return;
        }

        if (!isGrounded)
            return;

        Vector2 velocity = body.linearVelocity;
        velocity.y = jumpForce;
        body.linearVelocity = velocity;
    }

    public void TryDash()
    {
        if (body == null || isDashing || Time.time < nextDashTime)
            return;

        if (isWallGripping)
            ReleaseWall(false);

        dashCoroutine = StartCoroutine(DashRoutine());
    }

    public void StopImmediately()
    {
        moveInput = 0f;
        isWallGripping = false;

        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;
        if (body != null)
        {
            body.gravityScale = defaultGravityScale;
            body.linearVelocity = Vector2.zero;
        }
    }

    private void UpdateEnvironmentState()
    {
        isGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;

        if (isGrounded && isWallGripping)
            ReleaseWall(false);
    }

    private void TryBeginWallGrip()
    {
        if (isGrounded || body.linearVelocity.y > 0.1f || Time.time < nextWallGripTime)
            return;

        Vector2 origin = wallCheckOrigin != null
            ? wallCheckOrigin.position
            : body.position;

        bool wallOnRight = Physics2D.Raycast(origin, Vector2.right, wallCheckDistance, wallLayer);
        bool wallOnLeft = Physics2D.Raycast(origin, Vector2.left, wallCheckDistance, wallLayer);

        if (!wallOnRight && !wallOnLeft)
            return;

        if (wallOnRight && wallOnLeft)
            grippedWallSide = facingDirection;
        else
            grippedWallSide = wallOnRight ? 1 : -1;

        isWallGripping = true;
        wallGripRemaining = wallGripDuration;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
    }

    private void ReleaseWall(bool jumpUp)
    {
        if (!isWallGripping || body == null)
            return;

        isWallGripping = false;
        nextWallGripTime = Time.time + wallRegrabDelay;
        body.gravityScale = defaultGravityScale;

        float horizontal = -grippedWallSide * wallJumpHorizontalForce;
        float vertical = jumpUp ? wallJumpVerticalForce : -wallReleaseFallSpeed;
        body.linearVelocity = new Vector2(horizontal, vertical);
        facingDirection = -grippedWallSide;
    }

    private IEnumerator DashRoutine()
    {
        isDashing = true;
        nextDashTime = Time.time + dashCooldown;
        float previousGravity = body.gravityScale;
        body.gravityScale = 0f;

        float direction = Mathf.Abs(moveInput) > 0.01f ? Mathf.Sign(moveInput) : facingDirection;
        body.linearVelocity = new Vector2(direction * dashSpeed, 0f);

        yield return new WaitForSeconds(dashDuration);

        body.gravityScale = previousGravity;
        body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
        isDashing = false;
        dashCoroutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        Vector3 origin = wallCheckOrigin != null ? wallCheckOrigin.position : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + Vector3.right * wallCheckDistance);
        Gizmos.DrawLine(origin, origin + Vector3.left * wallCheckDistance);
    }
}
