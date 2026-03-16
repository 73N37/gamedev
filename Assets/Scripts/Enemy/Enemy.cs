using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private int health = 1;
    [SerializeField] private float movespeed = 2f;

    private Rigidbody2D rb;
    private Transform checkpoint;

    private int index = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        checkpoint = EnemyManager.main.checkpoints[index];
    }

    void Update()
    {
        checkpoint = EnemyManager.main.checkpoints[index];

        if (Vector2.Distance(checkpoint.transform.position, transform.position) <= 0.1f)
        {
            index++;

            if (index >= EnemyManager.main.checkpoints.Length)
            {
                Destroy(gameObject);    // destroy the enemy if it reaches the end of the path
            }
        }
    }

    void FixedUpdate()
    {
        Vector2 direction = (checkpoint.transform.position - transform.position).normalized;
        transform.right = checkpoint.transform.position - transform.position;
        rb.linearVelocity = direction * movespeed;
    }

    public void TakeDamage(int damage)
    {
        health -= damage;

        if (health <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Destroy(gameObject);
    }
}
