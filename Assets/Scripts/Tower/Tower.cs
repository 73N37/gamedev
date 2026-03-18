using UnityEngine;

// Tower is the offensive unit the player upgrades and clicks on.
// It does three main jobs during gameplay:
// - It receives a current target from TowerRange.
// - It rotates to face that target and fires on a cooldown.
// - It exposes upgrade paths for range, damage, and fire rate to the TowerManager shop UI.
//
// So the overall attack loop is:
// 1. TowerRange detects enemies entering the trigger area.
// 2. TowerRange chooses the nearest enemy and stores it in tower.target.
// 3. Tower.Update rotates toward that enemy.
// 4. When enough time has passed, Fire creates a projectile.
// 5. The projectile damages the enemy when it reaches it.
// Rotates toward the current target and fires projectiles on a cooldown.
public class Tower : MonoBehaviour
{
    // range controls how large the targeting trigger should be.
    [SerializeField] private float range = 8f;

    // damage is how much health each successful shot removes from an enemy.
    [SerializeField] private int damage = 1;

    // fireRate is the delay in seconds between shots.
    [SerializeField] private float fireRate = 1f;

    // projectilePrefab is the projectile spawned when the tower fires.
    [SerializeField] private Projectile projectilePrefab;

    // shootPoint is the transform where projectiles are spawned from.
    [SerializeField] private Transform shootPoint;

    // projectileSpeed controls how quickly the spawned projectile travels to its target.
    [SerializeField] private float projectileSpeed = 12f;

    // placementCost is the upfront cost used by TowerManager when this tower is placed at runtime.
    [Header("Economy Settings")]
    [SerializeField] private int placementCost = 0;

    // These arrays define the cost and effect of each upgrade tier in the shop.
    [Header("Upgrade Settings")]
    [SerializeField] private int[] rangeUpgradeCosts = { 10, 20, 30 };
    [SerializeField] private float[] rangeUpgradeAmounts = { 1f, 1.5f, 2f };
    [SerializeField] private int[] damageUpgradeCosts = { 15, 30, 45 };
    [SerializeField] private int[] damageUpgradeAmounts = { 1, 2, 3 };
    [SerializeField] private int[] fireRateUpgradeCosts = { 10, 20, 30 };
    [SerializeField] private float[] fireRateUpgradeReductions = { 0.1f, 0.15f, 0.2f };
    [SerializeField] private float minimumFireRate = 0.1f;

    // fireCooldown stores how much time has passed since the last shot.
    private float fireCooldown = 0f;

    // These tier counters track how many upgrades the player has already bought in each path.
    private int rangeUpgradeTier = 0;
    private int damageUpgradeTier = 0;
    private int fireRateUpgradeTier = 0;

    // towerRange is the helper script that detects enemies inside the tower's attack radius.
    private TowerRange towerRange;

    // target is assigned dynamically by TowerRange while enemies are inside range.
    [HideInInspector] public GameObject target;

    // These read-only properties let the shop ask the tower about its current stats and upgrade state.
    public float Range => range;
    public int CurrentDamage => damage;
    public float CurrentFireRate => fireRate;
    public int PlacementCost => Mathf.Max(0, placementCost);
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
        // Find the child range helper so upgrades can resize it later.
        towerRange = GetComponentInChildren<TowerRange>();

        // If no custom shoot point was assigned, fire from the tower's own transform.
        if (shootPoint == null)
        {
            shootPoint = transform;
        }

        // Make sure the tower has a small collider that can be clicked to open the shop.
        EnsureClickCollider();
    }

    // Runs when the tower becomes active so TowerManager can track it as part of the tower group.
    private void OnEnable()
    {
        TowerManager.main?.RegisterTower(this);
    }

    // Runs every frame to face the target, count cooldown time, and shoot when ready.
    private void Update()
    {
        // If no enemy is targeted, reset the timer so the tower only counts cooldown while actively attacking.
        if (target == null)
        {
            fireCooldown = 0f;
            return;
        }

        // Make sure the target still has an Enemy component and has not been replaced by a stale reference.
        Enemy enemy = target.GetComponent<Enemy>();
        if (enemy == null)
        {
            target = null;
            fireCooldown = 0f;
            return;
        }

        // Rotate the tower to face the selected enemy.
        transform.right = target.transform.position - transform.position;

        // Count how much time has passed since the last shot.
        fireCooldown += Time.deltaTime;

        // Wait until the full fire-rate delay has elapsed.
        if (fireCooldown < fireRate)
        {
            return;
        }

        // Fire a shot at the active enemy.
        Fire(enemy);

        // Reset the cooldown timer so the next shot starts counting from zero.
        fireCooldown = 0f;
    }

    // Called when the tower is ready to attack and has a valid enemy target.
    private void Fire(Enemy enemy)
    {
        // Ignore firing if the target vanished between Update and this method call.
        if (enemy == null)
        {
            return;
        }

        // If no projectile prefab exists, fall back to instant damage so the tower still functions.
        if (projectilePrefab == null)
        {
            enemy.TakeDamage(damage);
            return;
        }

        // Use the assigned shoot point if one exists, otherwise fire from the tower center.
        Transform origin = shootPoint != null ? shootPoint : transform;

        // Spawn a new projectile at the firing point.
        Projectile projectile = Instantiate(projectilePrefab, origin.position, origin.rotation);

        // Give the projectile the target, damage, and travel speed it needs to complete the attack.
        projectile.Initialize(enemy, damage, projectileSpeed);
    }

    // Called by the shop UI when the player buys the next range upgrade for this tower.
    public bool TryUpgradeRange()
    {
        // Stop if the path is maxed or if there is no active GameManager to handle payment.
        if (!CanUpgradeRange || GameManager.main == null)
        {
            return false;
        }

        // Ask GameManager to pay for the upgrade before changing stats.
        if (!GameManager.main.TrySpendCurrency(NextRangeUpgradeCost))
        {
            return false;
        }

        // Increase the tower's range by the amount for the current tier.
        range += rangeUpgradeAmounts[rangeUpgradeTier];

        // Move the range tier forward so the next purchase uses the next tier values.
        rangeUpgradeTier++;

        // Resize the range trigger so targeting matches the new stat immediately.
        towerRange?.UpdateRange();

        // Refresh the tower shop so the displayed stats and cost update right away.
        TowerManager.main?.RefreshTowerShop();
        return true;
    }

    // Called by the shop UI when the player buys the next damage upgrade for this tower.
    public bool TryUpgradeDamage()
    {
        // Stop if the path is maxed or if there is no active GameManager to handle payment.
        if (!CanUpgradeDamage || GameManager.main == null)
        {
            return false;
        }

        // Ask GameManager to pay for the upgrade before changing stats.
        if (!GameManager.main.TrySpendCurrency(NextDamageUpgradeCost))
        {
            return false;
        }

        // Increase the per-shot damage by the amount for the current tier.
        damage += damageUpgradeAmounts[damageUpgradeTier];

        // Move the damage tier forward so the next purchase uses the next tier values.
        damageUpgradeTier++;

        // Refresh the tower shop so the displayed stats and cost update right away.
        TowerManager.main?.RefreshTowerShop();
        return true;
    }

    // Called by the shop UI when the player buys the next fire-rate upgrade for this tower.
    public bool TryUpgradeFireRate()
    {
        // Stop if the path is maxed or if there is no active GameManager to handle payment.
        if (!CanUpgradeFireRate || GameManager.main == null)
        {
            return false;
        }

        // Ask GameManager to pay for the upgrade before changing stats.
        if (!GameManager.main.TrySpendCurrency(NextFireRateUpgradeCost))
        {
            return false;
        }

        // Reduce the delay between shots, but never allow it to go below the configured minimum.
        fireRate = Mathf.Max(minimumFireRate, fireRate - fireRateUpgradeReductions[fireRateUpgradeTier]);

        // Move the fire-rate tier forward so the next purchase uses the next tier values.
        fireRateUpgradeTier++;

        // Refresh the tower shop so the displayed stats and cost update right away.
        TowerManager.main?.RefreshTowerShop();
        return true;
    }

    // Called by TowerManager when it needs to know how much value this tower currently holds.
    public int GetSellValue(float sellRefundPercent)
    {
        int totalInvestedCurrency = PlacementCost;

        for (int i = 0; i < rangeUpgradeTier; i++)
        {
            totalInvestedCurrency += rangeUpgradeCosts[i];
        }

        for (int i = 0; i < damageUpgradeTier; i++)
        {
            totalInvestedCurrency += damageUpgradeCosts[i];
        }

        for (int i = 0; i < fireRateUpgradeTier; i++)
        {
            totalInvestedCurrency += fireRateUpgradeCosts[i];
        }

        return Mathf.Max(0, Mathf.RoundToInt(totalInvestedCurrency * Mathf.Clamp01(sellRefundPercent)));
    }

    // Runs during setup to give the tower root a small click area separate from its big range trigger.
    private void EnsureClickCollider()
    {
        // Reuse an existing collider if the tower already has one.
        if (GetComponent<Collider2D>() != null)
        {
            return;
        }

        // Add a small trigger collider so the player can click the tower without clicking the whole range area.
        CircleCollider2D towerCollider = gameObject.AddComponent<CircleCollider2D>();
        towerCollider.isTrigger = true;
        towerCollider.radius = 0.6f;
    }

    // Runs when the tower is destroyed so TowerManager stops tracking it.
    private void OnDestroy()
    {
        TowerManager.main?.UnregisterTower(this);
    }
}
