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

    [Header("Upgrade Settings")]
    [SerializeField] private int[] rangeUpgradeCosts = { 10, 20, 30 };
    [SerializeField] private float[] rangeUpgradeAmounts = { 1f, 1.5f, 2f };
    [SerializeField] private int[] damageUpgradeCosts = { 15, 30, 45 };
    [SerializeField] private int[] damageUpgradeAmounts = { 1, 2, 3 };
    [SerializeField] private int[] fireRateUpgradeCosts = { 10, 20, 30 };
    [SerializeField] private float[] fireRateUpgradeReductions = { 0.1f, 0.15f, 0.2f };
    [SerializeField] private float minimumFireRate = 0.1f;

    private float fireCooldown = 0f;
    private int rangeUpgradeTier = 0;
    private int damageUpgradeTier = 0;
    private int fireRateUpgradeTier = 0;
    private TowerRange towerRange;

    [HideInInspector] public GameObject target;

    public float Range => range;
    public int CurrentDamage => damage;
    public float CurrentFireRate => fireRate;
    public int RangeUpgradeTier => rangeUpgradeTier;
    public int DamageUpgradeTier => damageUpgradeTier;
    public int FireRateUpgradeTier => fireRateUpgradeTier;
    public int MaxRangeUpgradeTier => Mathf.Min(rangeUpgradeCosts.Length, rangeUpgradeAmounts.Length);
    public int MaxDamageUpgradeTier => Mathf.Min(damageUpgradeCosts.Length, damageUpgradeAmounts.Length);
    public int MaxFireRateUpgradeTier => Mathf.Min(fireRateUpgradeCosts.Length, fireRateUpgradeReductions.Length);
    public bool CanUpgradeRange => rangeUpgradeTier < MaxRangeUpgradeTier;
    public bool CanUpgradeDamage => damageUpgradeTier < MaxDamageUpgradeTier;
    public bool CanUpgradeFireRate => fireRateUpgradeTier < MaxFireRateUpgradeTier;
    public int NextRangeUpgradeCost => CanUpgradeRange ? rangeUpgradeCosts[rangeUpgradeTier] : -1;
    public int NextDamageUpgradeCost => CanUpgradeDamage ? damageUpgradeCosts[damageUpgradeTier] : -1;
    public int NextFireRateUpgradeCost => CanUpgradeFireRate ? fireRateUpgradeCosts[fireRateUpgradeTier] : -1;

    // Runs when the tower is created to ensure it can shoot, resize, and be clicked.
    private void Awake()
    {
        towerRange = GetComponentInChildren<TowerRange>();

        if (shootPoint == null)
        {
            shootPoint = transform;
        }

        EnsureClickCollider();
    }

    // Runs every frame to face the target, count cooldown time, and shoot when ready.
    private void Update()
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

    // Called by the shop UI when the player buys the next range upgrade for this tower.
    public bool TryUpgradeRange()
    {
        if (!CanUpgradeRange || GameManager.main == null)
        {
            return false;
        }

        if (!GameManager.main.TrySpendCurrency(NextRangeUpgradeCost))
        {
            return false;
        }

        range += rangeUpgradeAmounts[rangeUpgradeTier];
        rangeUpgradeTier++;
        towerRange?.UpdateRange();
        GameManager.main.RefreshTowerShop();
        return true;
    }

    // Called by the shop UI when the player buys the next damage upgrade for this tower.
    public bool TryUpgradeDamage()
    {
        if (!CanUpgradeDamage || GameManager.main == null)
        {
            return false;
        }

        if (!GameManager.main.TrySpendCurrency(NextDamageUpgradeCost))
        {
            return false;
        }

        damage += damageUpgradeAmounts[damageUpgradeTier];
        damageUpgradeTier++;
        GameManager.main.RefreshTowerShop();
        return true;
    }

    // Called by the shop UI when the player buys the next fire-rate upgrade for this tower.
    public bool TryUpgradeFireRate()
    {
        if (!CanUpgradeFireRate || GameManager.main == null)
        {
            return false;
        }

        if (!GameManager.main.TrySpendCurrency(NextFireRateUpgradeCost))
        {
            return false;
        }

        fireRate = Mathf.Max(minimumFireRate, fireRate - fireRateUpgradeReductions[fireRateUpgradeTier]);
        fireRateUpgradeTier++;
        GameManager.main.RefreshTowerShop();
        return true;
    }

    // Runs during setup to give the tower root a small click area separate from its big range trigger.
    private void EnsureClickCollider()
    {
        if (GetComponent<Collider2D>() != null)
        {
            return;
        }

        CircleCollider2D towerCollider = gameObject.AddComponent<CircleCollider2D>();
        towerCollider.isTrigger = true;
        towerCollider.radius = 0.6f;
    }
}
