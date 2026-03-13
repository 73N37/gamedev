using UnityEngine;

public class Enemy : MonoBehaviour
{
    [SerializeField] private float movespeed = 2f;

    private Rigidbody2D rb;
    private Transform checkpoint;
    private Transform[] checkpoints;

    private int index = 0;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (EnemyManager.main == null || EnemyManager.main.checkpoints == null || EnemyManager.main.checkpoints.Length == 0)
        {
            Debug.LogError("EnemyManager checkpoints are not set.", this);
            enabled = false;
            return;
        }

        checkpoints = EnemyManager.main.checkpoints;
        checkpoint = checkpoints[index];
    }

    void Update()
    {
        if (checkpoint == null)
        {
            return;
        }

        if (Vector2.Distance(checkpoint.position, transform.position) < 0.1f)
        {
            index++;
            if (index >= checkpoints.Length)
            {
                Destroy(gameObject);    // destroy the enemy if it reaches the end of the path
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
}
