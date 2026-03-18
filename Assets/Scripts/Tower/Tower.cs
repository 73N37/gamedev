using UnityEngine;

// Rotates toward the current target and fires projectiles on a cooldown.
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

    // Runs when the tower is created to ensure it has a valid place to spawn projectiles from.
    private void Awake()
    {
        if (shootPoint == null)
        {
            shootPoint = transform;
        }
    }

    // Runs every frame to face the target, count cooldown time, and shoot when ready.
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

    // Called when the tower is ready to attack and has a valid enemy target.
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
