using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private float range = 8f;
    [SerializeField] private float fireRate = 1.2f;
    [SerializeField] private Transform towerRoot;

    [SerializeField] private int damage = 37;
    [SerializeField] private Transform rotatePart;

    public float Range => range;

    public GameObject target;
    private float cooldown = 0.3f;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Rigidbody2D rb;

    void Awake()
    {
        if (towerRoot == null)
        {
            towerRoot = transform;
        }

        startPosition = transform.position;
        startRotation = transform.rotation;
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Static;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    void Update()
    {
        startPosition = towerRoot.position;
        transform.position = startPosition;
        transform.rotation = startRotation;

        if (target != null && rotatePart != null)
        {
            Vector3 direction = target.transform.position - rotatePart.position;
            rotatePart.right = direction;
        }
    }

    void LateUpdate()
    {
        transform.position = startPosition;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }
}
