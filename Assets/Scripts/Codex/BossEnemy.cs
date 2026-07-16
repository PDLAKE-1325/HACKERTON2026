using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BossEnemy : EnemyBase
{
    [Header("Boss Patterns")]
    [SerializeField] private BossRockProjectile rockPrefab;
    [SerializeField] private Transform rockSpawnPoint;
    [SerializeField] private GameObject normalMobPrefab;
    [SerializeField] private Transform minionSpawnPoint;
    [SerializeField] private float initialPatternDelay = 1.5f;
    [SerializeField] private float timeBetweenPatterns = 3f;
    [SerializeField] private float rockSpeed = 9f;
    [SerializeField] private float rockUpwardBoost = 4f;

    private PlayerHealth targetPlayer;
    private Coroutine patternRoutine;

    protected override void Awake()
    {
        base.Awake();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        targetPlayer = FindAnyObjectByType<PlayerHealth>();
    }

    private void OnEnable()
    {
        patternRoutine = StartCoroutine(PatternLoop());
    }

    protected override void FixedUpdate()
    {
        if (IsDead || body == null)
            return;

        body.linearVelocity = Vector2.zero;
    }

    private IEnumerator PatternLoop()
    {
        yield return new WaitForSeconds(initialPatternDelay);

        while (!IsDead)
        {
            ThrowRock();
            yield return new WaitForSeconds(timeBetweenPatterns);

            if (IsDead)
                yield break;

            SummonNormalMob();
            yield return new WaitForSeconds(timeBetweenPatterns);
        }
    }

    private void ThrowRock()
    {
        if (rockPrefab == null || rockSpawnPoint == null)
            return;

        if (targetPlayer == null || targetPlayer.IsDead)
            targetPlayer = FindAnyObjectByType<PlayerHealth>();
        if (targetPlayer == null || targetPlayer.IsDead)
            return;

        BossRockProjectile rock = Instantiate(
            rockPrefab,
            rockSpawnPoint.position,
            Quaternion.identity);
        Vector2 direction = ((Vector2)targetPlayer.transform.position -
            (Vector2)rockSpawnPoint.position).normalized;
        Vector2 velocity = direction * rockSpeed + Vector2.up * rockUpwardBoost;
        rock.Launch(velocity, transform);
    }

    private void SummonNormalMob()
    {
        if (normalMobPrefab == null || minionSpawnPoint == null)
            return;

        Instantiate(normalMobPrefab, minionSpawnPoint.position, Quaternion.identity);
    }

    protected override void Die()
    {
        if (IsDead)
            return;

        if (patternRoutine != null)
        {
            StopCoroutine(patternRoutine);
            patternRoutine = null;
        }


        EndingYeah.Instance.kk();
        base.Die();
    }



    private void OnDisable()
    {
        if (patternRoutine == null)
            return;

        StopCoroutine(patternRoutine);
        patternRoutine = null;
    }
}
