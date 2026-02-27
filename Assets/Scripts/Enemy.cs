using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [SerializeField] private float movespeed = 2f;

    private Rigidbody2D rb;
    [SerializeField] private Transform checkpoint;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        
    }

    void Update()
    {
        if (Vector2.Distance(checkpoint.position, transform.position) < 0.1f)
        {
            Debug.Log("Checkpoint reached!");
        }
    }

    void FixedUpdate()
    {
        Vector2 direction = (checkpoint.position - transform.position).normalized;
        rb.linearVelocity = direction * movespeed;
    }
}
