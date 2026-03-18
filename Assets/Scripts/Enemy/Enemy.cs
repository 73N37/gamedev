using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private float movespeed = 2f;
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int reward = 25;
    [SerializeField] private int lifePenalty = 1;

    private Rigidbody2D rb;
    private Transform checkpoint;
    private Transform[] checkpoints;
    private int currentHealth;
    private int index = 0;
    private bool hasBeenRemoved;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = Mathf.Max(1, maxHealth);
    }

    void Start()
    {
        if (EnemyManager.main == null || EnemyManager.main.checkpoints == null || EnemyManager.main.checkpoints.Length == 0)
        {
            Debug.LogError("EnemyManager checkpoints are not set.", this);
            enabled = false;
            return;
        }

        EnemyManager.main.RegisterEnemy(this);
        checkpoints = EnemyManager.main.checkpoints;
        checkpoint = checkpoints[index];
    }

    void Update()
    {
        if (checkpoint == null)
        {
            return;
        }

        if (Vector2.Distance(checkpoint.position, transform.position) <= 0.1f)
        {
            index++;

            if (index >= checkpoints.Length)
            {
                ReachGoal();
                return;
            }

            checkpoint = checkpoints[index];
        }
    }

    void FixedUpdate()
    {
        if (checkpoint == null)
        {
            return;
        }

        Vector2 direction = (checkpoint.position - transform.position).normalized;
        transform.right = checkpoint.position - transform.position;
        rb.linearVelocity = direction * movespeed;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0 || hasBeenRemoved)
        {
            return;
        }

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void ReachGoal()
    {
        if (hasBeenRemoved)
        {
            return;
        }

        hasBeenRemoved = true;
        GameManager.main?.EnemyEscaped(this, lifePenalty);
        Destroy(gameObject);
    }

    private void Die()
    {
        if (hasBeenRemoved)
        {
            return;
        }

        hasBeenRemoved = true;
        GameManager.main?.EnemyDefeated(reward);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        EnemyManager.main?.UnregisterEnemy(this);
    }
}
