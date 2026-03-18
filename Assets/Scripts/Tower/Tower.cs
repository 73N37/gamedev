using UnityEngine;

public class Tower : MonoBehaviour
{
    [SerializeField] private float range = 8f;
    [SerializeField] private int damage = 1;
    [SerializeField] private float fireRate = 1f;
    private float fireCooldown = 0f;

    [HideInInspector] public GameObject target;

    public float Range => range;

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

        enemy.TakeDamage(damage);
        fireCooldown = 0f;
    }
}
