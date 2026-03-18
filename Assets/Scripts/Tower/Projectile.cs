using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float maxLifetime = 3f;

    private Enemy target;
    private int damage;
    private float speed;
    private bool hasHit;

    public void Initialize(Enemy newTarget, int newDamage, float newSpeed)
    {
        target = newTarget;
        damage = newDamage;
        speed = newSpeed;

        Destroy(gameObject, maxLifetime);
    }

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

    private void HitTarget()
    {
        if (hasHit)
        {
            return;
        }

        hasHit = true;

        if (target != null)
        {
            GameManager.main?.RegisterBalloonHit();
            target.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
