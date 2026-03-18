using UnityEngine;

// Projectile represents one fired shot travelling from a tower to a single enemy.
// Towers create a projectile in Fire(), then this script handles the rest:
// - remember which enemy to chase
// - move toward that enemy every frame
// - apply damage once when the projectile reaches it
// - destroy itself after the hit, or after a timeout if something goes wrong
// Simple projectile that chases one enemy, deals damage once, and then destroys itself.
public class Projectile : MonoBehaviour
{
    // maxLifetime is a safety timer so lost projectiles do not remain forever.
    [SerializeField] private float maxLifetime = 3f;

    // target is the enemy this projectile is currently chasing.
    private Enemy target;

    // damage is how much health this projectile removes when it hits.
    private int damage;

    // speed is how quickly the projectile moves through the scene.
    private float speed;

    // hasHit prevents one projectile from dealing damage more than once.
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
        // Stop processing movement after the projectile has already landed.
        if (hasHit)
        {
            return;
        }

        // If the target disappeared before impact, remove the projectile too.
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Calculate the direction from the projectile to the target.
        Vector3 direction = target.transform.position - transform.position;

        // Calculate how far the projectile can move during this frame.
        float moveDistance = speed * Time.deltaTime;

        // If the projectile can reach the target this frame, resolve the hit immediately.
        if (direction.sqrMagnitude <= moveDistance * moveDistance)
        {
            HitTarget();
            return;
        }

        // Rotate the projectile so it visually points toward the enemy.
        transform.right = direction;

        // Move the projectile toward the enemy.
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
        // Ignore duplicate hit calls from overlap and distance checks firing close together.
        if (hasHit)
        {
            return;
        }

        // Mark the projectile as finished so it cannot hit again.
        hasHit = true;

        // Apply damage if the target still exists at the moment of impact.
        if (target != null)
        {
            target.TakeDamage(damage);
        }

        // Remove the projectile from the scene after the hit is resolved.
        Destroy(gameObject);
    }
}
