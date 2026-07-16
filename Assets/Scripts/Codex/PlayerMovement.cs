using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Animator animator;
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
    [SerializeField] private float wallCheckDistance = 0.15f;
    [SerializeField, Range(0.2f, 1f)] private float wallCheckHeightRatio = 0.8f;
    [SerializeField] private float wallGripDuration = 0.65f;
    [SerializeField] private float wallRegrabDelay = 0.2f;
    [SerializeField] private float wallJumpHorizontalForce = 7f;
    [SerializeField] private float wallJumpVerticalForce = 13f;
    [SerializeField] private float wallJumpControlLockDuration = 0.25f;
    [SerializeField] private float wallReleaseFallSpeed = 3f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.16f;
    [SerializeField] private float dashCooldown = 0.8f;

    [Header("Fall Respawn")]
    [SerializeField] private float fallResetY = -20f;

    [Header("Animation")]
    [SerializeField] private string moveBool = "Move";
    [SerializeField] private string wallGripBool = "WallGrip";

    private float moveInput;
    private float defaultGravityScale;
    private float wallGripRemaining;
    private float nextDashTime;
    private float nextWallGripTime;
    private float wallJumpControlLockedUntil;
    private float knockbackControlLockedUntil;
    private int facingDirection = 1;
    private int grippedWallSide;
    private bool isGrounded;
    private bool isWallGripping;
    private bool isDashing;
    private Coroutine dashCoroutine;
    private Vector3 startingPosition;

    public int FacingDirection => facingDirection;
    public bool IsDashing => isDashing;

    private void Awake()
    {
        startingPosition = transform.position;

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (body != null)
            defaultGravityScale = body.gravityScale;
    }

    private void FixedUpdate()
    {
        if (body == null)
            return;

        if (transform.position.y < fallResetY)
        {
            ReturnToStartingPosition();
            return;
        }

        UpdateEnvironmentState();

        UpdateMoveAnimation();

        if (Time.time < knockbackControlLockedUntil)
            return;

        if (isDashing)
            return;

        if (isWallGripping)
        {
            if (!TryFindWallSide(out int currentWallSide) || currentWallSide != grippedWallSide)
            {
                ReleaseWall(false);
                return;
            }

            wallGripRemaining -= Time.fixedDeltaTime;
            body.linearVelocity = Vector2.zero;

            if (wallGripRemaining <= 0f)
                ReleaseWall(false);

            return;
        }

        TryBeginWallGrip();
        if (isWallGripping)
            return;

        if (Time.time < wallJumpControlLockedUntil)
            return;

        Vector2 velocity = body.linearVelocity;
        velocity.x = moveInput * moveSpeed;
        body.linearVelocity = velocity;
    }

    public void SetMoveInput(float value)
    {
        moveInput = Mathf.Clamp(value, -1f, 1f);
        if (Mathf.Abs(moveInput) > 0.01f && Time.time >= wallJumpControlLockedUntil)
        {
            facingDirection = moveInput > 0f ? 1 : -1;

            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * facingDirection;
            transform.localScale = scale;
        }
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
        wallJumpControlLockedUntil = 0f;
        knockbackControlLockedUntil = 0f;

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

        SetMoveAnimation(false);
        SetWallGripAnimation(false);
    }

    public void ApplyKnockback(Vector2 velocity, float controlLockDuration)
    {
        if (body == null)
            return;

        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;
        isWallGripping = false;
        body.gravityScale = defaultGravityScale;
        knockbackControlLockedUntil = Time.time + Mathf.Max(0f, controlLockDuration);
        body.linearVelocity = velocity;
        SetMoveAnimation(false);
        SetWallGripAnimation(false);
    }

    private void ReturnToStartingPosition()
    {
        StopImmediately();
        transform.position = startingPosition;

        if (body == null)
            return;

        body.position = new Vector2(startingPosition.x, startingPosition.y);
        body.angularVelocity = 0f;
    }

    private void UpdateEnvironmentState()
    {
        isGrounded = groundCheck != null &&
            Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;

        if (isGrounded && isWallGripping)
            ReleaseWall(false);
    }

    private void UpdateMoveAnimation()
    {
        bool isMoving = isGrounded &&
            !isDashing &&
            !isWallGripping &&
            Time.time >= knockbackControlLockedUntil &&
            Mathf.Abs(body.linearVelocity.x) > 0.1f;
        SetMoveAnimation(isMoving);
        SetWallGripAnimation(isWallGripping);
    }

    private void SetMoveAnimation(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(moveBool))
            animator.SetBool(moveBool, value);
    }

    private void SetWallGripAnimation(bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(wallGripBool))
            animator.SetBool(wallGripBool, value);
    }

    private void TryBeginWallGrip()
    {
        if (isGrounded || Time.time < nextWallGripTime)
            return;

        if (!TryFindWallSide(out grippedWallSide))
            return;

        isWallGripping = true;
        wallGripRemaining = wallGripDuration;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        SetMoveAnimation(false);
        SetWallGripAnimation(true);
    }

    private void ReleaseWall(bool jumpUp)
    {
        if (!isWallGripping || body == null)
            return;

        isWallGripping = false;
        body.gravityScale = defaultGravityScale;
        SetWallGripAnimation(false);

        float regrabDelay = wallRegrabDelay;
        if (jumpUp)
        {
            wallJumpControlLockedUntil = Time.time + wallJumpControlLockDuration;
            regrabDelay = Mathf.Max(regrabDelay, wallJumpControlLockDuration);
        }
        nextWallGripTime = Time.time + regrabDelay;

        float horizontal = -grippedWallSide * wallJumpHorizontalForce;
        float vertical = jumpUp ? wallJumpVerticalForce : -wallReleaseFallSpeed;
        body.linearVelocity = new Vector2(horizontal, vertical);
        facingDirection = -grippedWallSide;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facingDirection;
        transform.localScale = scale;
    }

    private bool TryFindWallSide(out int wallSide)
    {
        wallSide = 0;

        Vector2 origin = wallCheckOrigin != null
            ? wallCheckOrigin.position
            : body.position;
        Vector2 castSize = Vector2.one * 0.1f;

        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            origin = bounds.center;
            castSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x * 0.9f),
                Mathf.Max(0.05f, bounds.size.y * wallCheckHeightRatio));
        }

        bool wallOnRight = Physics2D.BoxCast(
            origin,
            castSize,
            0f,
            Vector2.right,
            wallCheckDistance,
            wallLayer).collider != null;
        bool wallOnLeft = Physics2D.BoxCast(
            origin,
            castSize,
            0f,
            Vector2.left,
            wallCheckDistance,
            wallLayer).collider != null;

        if (!wallOnRight && !wallOnLeft)
            return false;

        wallSide = wallOnRight && wallOnLeft
            ? facingDirection
            : wallOnRight ? 1 : -1;
        return true;
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

        Vector3 origin = bodyCollider != null
            ? bodyCollider.bounds.center
            : wallCheckOrigin != null ? wallCheckOrigin.position : transform.position;
        Vector3 size = bodyCollider != null
            ? new Vector3(
                bodyCollider.bounds.size.x * 0.9f,
                bodyCollider.bounds.size.y * wallCheckHeightRatio,
                0f)
            : Vector3.one * 0.1f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(origin + Vector3.right * wallCheckDistance, size);
        Gizmos.DrawWireCube(origin + Vector3.left * wallCheckDistance, size);
    }
}
