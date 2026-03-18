using UnityEngine;

// Simple projectile that chases one enemy, deals damage once, and then destroys itself.
public class Projectile : MonoBehaviour
{
    [SerializeField] private float maxLifetime = 3f;

    private Enemy target;
    private int damage;
    private float speed;
    private bool hasHit;

    // Called immediately after spawn so the projectile knows what to chase and how hard to hit.
    public void Initialize(Enemy newTarget, int newDamage, float newSpeed)
    {
        target = newTarget;
        damage = newDamage;
        speed = newSpeed;

        Destroy(gameObject, maxLifetime);
    }

    // Runs every frame to move the projectile toward its target or remove it if the target disappears.
    private void Update()
    {
        if (hasHit)
        {
            return;
        }

        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 direction = target.transform.position - transform.position;
        float moveDistance = speed * Time.deltaTime;

        if (direction.sqrMagnitude <= moveDistance * moveDistance)
        {
            HitTarget();
            return;
        }

        transform.right = direction;
        transform.position += direction.normalized * moveDistance;
    }

    // Called by Unity if the projectile trigger overlaps its target before the manual distance check.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasHit)
        {
            return;
        }

        Enemy enemy = collision.GetComponent<Enemy>();
        if (enemy != null && enemy == target)
        {
            HitTarget();
        }
    }

    // Called once when the projectile reaches its target so it can apply damage and disappear.
    private void HitTarget()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        if (target != null)
        {
            target.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
