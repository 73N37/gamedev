using UnityEngine;

// Enemy controls one balloon from the moment it spawns until it is removed.
// In the full gameplay loop, this script sits between the wave system and the tower system:
// - EnemyManager spawns the object and gives it health and coin values.
// - The enemy follows the shared waypoint path.
// - Towers and projectiles call TakeDamage when they hit it.
// - If it dies, GameManager rewards the player with coins.
// - If it reaches the end of the path first, GameManager removes player health instead.
// One balloon instance that follows the waypoint path until it is popped or escapes.
public class Enemy : MonoBehaviour
{
    // Movement speed decides how quickly the balloon travels between waypoints.
    [SerializeField] private float movespeed = 2f;

    // maxHealth is the default starting health stored on the prefab before EnemyManager customizes it.
    [SerializeField] private int maxHealth = 1;

    // reward is how many coins the player earns when this balloon is popped.
    [SerializeField] private int reward = 1;

    // lifePenalty is how much damage this balloon does if it escapes.
    [SerializeField] private int lifePenalty = 1;

    // The rigidbody is used to move the enemy smoothly during FixedUpdate.
    private Rigidbody2D rb;

    // This is the current waypoint the enemy is travelling toward.
    private Transform despawnpoint;

    // This is the full ordered list of waypoints supplied by EnemyManager.
    private Transform[] checkpoints;

    // currentHealth stores how much life the balloon still has during gameplay.
    private int currentHealth;

    // index points at the current waypoint inside the checkpoints array.
    private int index = 0;

    // hasBeenRemoved prevents the enemy from escaping and dying twice.
    private bool hasBeenRemoved;

    // Runs when the enemy is created to cache physics and apply its initial serialized health.
    void Awake()
    {
        // Cache the Rigidbody2D so movement can update it efficiently later.
        rb = GetComponent<Rigidbody2D>();

        // Start with at least 1 health, based on the prefab value until EnemyManager overrides it.
        currentHealth = Mathf.Max(1, maxHealth);
    }

    // Called by EnemyManager right after spawn to apply per-enemy-type health and coin rewards.
    public void Initialize(int health, int coinReward)
    {
        // Apply the health chosen by EnemyManager for this enemy type in the current wave.
        maxHealth = Mathf.Max(1, health);

        // Reset the live health to match the newly assigned maximum.
        currentHealth = maxHealth;

        // Apply the number of coins the player should earn when this balloon dies.
        reward = Mathf.Max(0, coinReward);
    }

    // Runs once after spawn so the enemy can register itself and grab the shared waypoint path.
    void Start()
    {
        if (EnemyManager.main == null || EnemyManager.main.checkpoints == null || EnemyManager.main.checkpoints.Length == 0)
        {
            Debug.LogError("EnemyManager checkpoints are not set.", this);
            enabled = false;
            return;
        }

        // Tell EnemyManager that this balloon is now alive in the scene.
        EnemyManager.main.RegisterEnemy(this);

        // Copy the shared waypoint route from EnemyManager into this enemy.
        checkpoints = EnemyManager.main.checkpoints;

        // Start by moving toward the first waypoint in the route.
        despawnpoint = checkpoints[index];
    }

    // Runs every frame to detect when the enemy reaches a waypoint or the end of the path.
    void Update()
    {
        // If the current waypoint disappeared, stop path logic for safety.
        if (despawnpoint == null)
        {
            return;
        }

        // Check whether the balloon has reached its current waypoint closely enough to advance.
        if (Vector2.Distance(despawnpoint.position, transform.position) <= 0.1f)
        {
            // Move on to the next waypoint in the route.
            index++;

            // If there are no more waypoints left, the balloon reached the end of the path.
            if (index >= checkpoints.Length)
            {
                ReachGoal();
                return;
            }

            // Otherwise switch to the next waypoint and keep moving.
            despawnpoint = checkpoints[index];
        }
    }

    // Runs on the physics step to move the enemy toward its current waypoint.
    void FixedUpdate()
    {
        // If there is no current waypoint, there is nowhere to move.
        if (despawnpoint == null)
        {
            return;
        }

        // Calculate the movement direction toward the current waypoint.
        Vector2 direction = (despawnpoint.position - transform.position).normalized;

        // Rotate the sprite so the balloon visually points along its path.
        transform.right = despawnpoint.position - transform.position;

        // Apply movement through the Rigidbody2D during the physics step.
        rb.linearVelocity = direction * movespeed;
    }

    // Called by projectiles or other damage sources when this enemy is hit.
    public void TakeDamage(int damage)
    {
        // Ignore invalid damage and ignore hits after the balloon has already been removed.
        if (damage <= 0 || hasBeenRemoved)
        {
            return;
        }

        // Subtract the incoming damage from the current health pool.
        currentHealth -= damage;

        // When health reaches zero or lower, the balloon is popped.
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    // Called when the enemy reaches the final waypoint so it damages the player and despawns.
    private void ReachGoal()
    {
        // Ignore duplicate goal handling if the balloon was already destroyed another way.
        if (hasBeenRemoved)
        {
            return;
        }

        // Mark the balloon as removed so it cannot also die and pay coins afterward.
        hasBeenRemoved = true;

        // Tell GameManager to remove player health for the escaped balloon.
        GameManager.main?.EnemyEscaped(this, lifePenalty);

        // Remove the balloon object from the scene.
        Destroy(gameObject);
    }

    // Called when the enemy's health reaches zero so the player gets coins and the enemy is removed.
    private void Die()
    {
        // Ignore duplicate death handling if the balloon has already escaped or been removed.
        if (hasBeenRemoved)
        {
            return;
        }

        // Mark the balloon as removed so it cannot trigger escape logic afterward.
        hasBeenRemoved = true;

        // Tell GameManager to award this balloon's coin reward to the player.
        GameManager.main?.EnemyDefeated(reward);

        // Remove the balloon object from the scene.
        Destroy(gameObject);
    }
    
    // Unity calls this right before the balloon object disappears from the scene.
    // EnemyManager uses this callback to stop counting this balloon as active.
    private void OnDestroy()
    {
        EnemyManager.main?.UnregisterEnemy(this);
    }
}
