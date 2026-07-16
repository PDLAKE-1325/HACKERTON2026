using UnityEngine;
using UnityEngine.SceneManagement;

public class VPlayerMove : MonoBehaviour
{
    // Header : 인스펙터에 파라미터들 구분하기 쉽게 나누는거

    [Header("움직임")]
    // Range(a,b), 뒤의 변수 슬라이더 형태로 바꿔줌
    [SerializeField, Range(3, 15)] float speed; // 플레이어 속도
    [SerializeField] bool flip_x;


    // 이하 3개 플레이어 내 컴포넌트
    Animator animator;
    SpriteRenderer spriteRenderer;
    Rigidbody2D rb;

    void Movement() // 움직임 함수
    {
        rb.linearVelocityX = Input.GetAxisRaw("Horizontal") * speed;

        if (flip_x)
            spriteRenderer.flipX = rb.linearVelocityX < 0;
        else
            spriteRenderer.flipX = rb.linearVelocityX > 0;
    }

    void SetAnimationConditions()
    {
        animator.SetBool("Move", rb.linearVelocityX != 0);

    }
    [SerializeField] string nextSceneName;
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.name == "Door")
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    #region Unity Method
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        Movement();
        SetAnimationConditions();
    }
    #endregion
}
