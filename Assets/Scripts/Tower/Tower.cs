using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private float range = 8f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float projectileSpeed = 12f;
    private float fireCooldown = 0f;

    [HideInInspector] public GameObject target;

    public float Range => range;

    private void Awake()
    {
        if (shootPoint == null)
        {
            shootPoint = transform;
        }
    }

    void Update()
    {
        if (target == null)
        {
            fireCooldown = 0f;
            return;
        }

        Enemy enemy = target.GetComponent<Enemy>();
        if (enemy == null)
        {
            target = null;
            fireCooldown = 0f;
            return;
        }

        transform.right = target.transform.position - transform.position;

        fireCooldown += Time.deltaTime;
        if (fireCooldown < fireRate)
        {
            return;
        }

        Fire(enemy);
        fireCooldown = 0f;
    }

    private void Fire(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            enemy.TakeDamage(damage);
            return;
        }

        Transform origin = shootPoint != null ? shootPoint : transform;
        Projectile projectile = Instantiate(projectilePrefab, origin.position, origin.rotation);
        projectile.Initialize(enemy, damage, projectileSpeed);
    }
}
