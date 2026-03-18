using UnityEngine;

// One balloon instance that follows the waypoint path until it is popped or escapes.
public class Enemy : MonoBehaviour
{
    [SerializeField] private float movespeed = 2f;
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int reward = 1;
    [SerializeField] private int lifePenalty = 1;

    private Rigidbody2D rb;
    private Transform checkpoint;
    private Transform[] checkpoints;
    private int currentHealth;
    private int index = 0;
    private bool hasBeenRemoved;

    // Runs when the enemy is created to cache physics and apply its initial serialized health.
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = Mathf.Max(1, maxHealth);
    }

    // Called by EnemyManager right after spawn to apply per-enemy-type health and coin rewards.
    public void Initialize(int health, int coinReward)
    {
        maxHealth = Mathf.Max(1, health);
        currentHealth = maxHealth;
        reward = Mathf.Max(0, coinReward);
    }

    // Runs once after spawn so the enemy can register itself and grab the shared waypoint path.
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

    // Runs every frame to detect when the enemy reaches a waypoint or the end of the path.
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

    // Runs on the physics step to move the enemy toward its current waypoint.
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

    // Called by projectiles or other damage sources when this enemy is hit.
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

    // Called when the enemy reaches the final waypoint so it damages the player and despawns.
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

    // Called when the enemy's health reaches zero so the player gets coins and the enemy is removed.
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
