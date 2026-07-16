using UnityEngine;

public class VPlayerMove : MonoBehaviour
{
    // Header : 인스펙터에 파라미터들 구분하기 쉽게 나누는거
    [Header("움직임")]
    // Range(a,b), 뒤의 변수 슬라이더 형태로 바꿔줌
    [SerializeField, Range(3, 15)] float speed; // 플레이어 속도
    [SerializeField, Range(3, 8)] float jump_power; // 플레이어 점프 높이
    [SerializeField] bool flip_x;

    [Header("바닥 레이어")]
    [SerializeField] LayerMask ground_layer; // 바닥 레이어


    // 이하 3개 플레이어 내 컴포넌트
    Animator animator;
    SpriteRenderer spriteRenderer;
    Rigidbody2D rb;
    BoxCollider2D collider;

    void Movement() // 움직임 함수
    {
        rb.linearVelocityX = Input.GetAxisRaw("Horizontal") * speed;

        if (flip_x)
            spriteRenderer.flipX = rb.linearVelocityX < 0;
        else
            spriteRenderer.flipX = rb.linearVelocityX > 0;

        rb.linearVelocityY = Input.GetKey(KeyCode.Space) && IsGround() ? jump_power : rb.linearVelocityY;
    }

    bool IsGround() // 바닥 체크
    {
        Vector3 endPos1 = transform.position + Vector3.down * (transform.localScale.y / 2 + 0.2f) + Vector3.left * (collider.size.x / 2);
        Vector3 endPos2 = transform.position + Vector3.down * (transform.localScale.y / 2 + 0.2f);
        Vector3 endPos3 = transform.position + Vector3.down * (transform.localScale.y / 2 + 0.2f) + Vector3.right * (collider.size.x / 2);

        return Physics2D.Linecast(transform.position, endPos1, ground_layer).collider != null ||
                Physics2D.Linecast(transform.position, endPos2, ground_layer).collider != null ||
                Physics2D.Linecast(transform.position, endPos3, ground_layer).collider != null;
    }

    void SetAnimationConditions()
    {
        animator.SetBool("move", rb.linearVelocityX != 0);

        animator.SetBool("jump", rb.linearVelocityY != 0);
    }

    #region Unity Method
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        collider = GetComponent<BoxCollider2D>();
    }

    void Update()
    {
        Movement();
        SetAnimationConditions();
    }
    #endregion
}
