using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// EnemyManager controls the wave loop for the whole level.
// It answers the questions:
// - Which wave are we on?
// - Which balloon types should spawn in this wave?
// - How many of each type should appear as waves get harder?
// - How many enemies are still alive right now?
//
// The main flow is:
// 1. Start the first wave automatically.
// 2. Build a list of balloons for the current wave.
// 3. Spawn them over time from spawnPoint.
// 4. Let each Enemy register itself as active.
// 5. Wait until no active enemies remain.
// 6. Start the next wave or announce that all waves are complete.
// Builds wave contents, spawns enemies over time, and tracks when each wave is cleared.
public class EnemyManager : MonoBehaviour
{
    // Temporary per-spawn data used while a wave is being built and instantiated.
    [Serializable]
    private struct EnemySpawnInfo
    {
        public GameObject prefab;
        public int health;
        public int coinReward;
    }

    // Singleton reference used by Enemy objects to find the active wave manager.
    public static EnemyManager main;

    // These waypoints describe the route every balloon follows through the map.
    public Transform[] checkpoints;

    // This is the position where newly spawned balloons appear.
    public Transform spawnPoint;

    // These serialized values define the prefab, count, health, and reward for the basic balloon type.
    [Header("Basic Enemy settings")]
    [SerializeField] private GameObject basicEnemy;
    [SerializeField] private int basicEnemyCount = 2;
    [SerializeField] private int basicEnemyHealth = 1;
    [SerializeField] private int basicEnemyCoinReward = 1;

    // These serialized values define the prefab, count, health, and reward for the fast balloon type.
    [Header("Fast Enemy settings")]
    [SerializeField] private GameObject fastEnemy;
    [SerializeField] private int fastEnemyCount = 2;
    [SerializeField] private int fastEnemyHealth = 2;
    [SerializeField] private int fastEnemyCoinReward = 2;

    // These serialized values define the prefab, count, health, and reward for the tank balloon type.
    [Header("Tank Enemy settings")]
    [SerializeField] private GameObject tankEnemy;
    [SerializeField] private int tankEnemyCount = 1;
    [SerializeField] private int tankEnemyHealth = 5;
    [SerializeField] private int tankEnemyCoinReward = 3;

    // These values control the overall pace and difficulty growth of the wave system.
    [Header("Wave settings")]
    [SerializeField] private int totalWaves = 20;
    [SerializeField] private float spawnDelay = 0.5f;
    [SerializeField] private float timeBetweenWaves = 2f;
    [SerializeField] private int basicEnemyGrowthPerWave = 1;
    [SerializeField] private int fastEnemyGrowthPerWave = 1;
    [SerializeField] private int tankEnemyGrowthPerWave = 1;

    // currentWave stores the human-facing wave number that is currently active.
    private int currentWave = 0;

    // isSpawning is true while the current wave is still being instantiated.
    private bool isSpawning;

    // activeEnemies tracks every enemy that is still alive in the scene.
    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();

    // This prevents the "all waves complete" event from firing more than once.
    private bool allWavesCompletedRaised;

    // Other systems subscribe to these events to react when a wave starts or the match is won.
    public event Action<int, int> WaveStarted;
    public event Action AllWavesCompleted;

    // These read-only properties expose the current wave status to the rest of the game.
    public int CurrentWave => currentWave;
    public int TotalWaves => totalWaves;
    public int ActiveEnemyCount => activeEnemies.Count;
    public bool IsSpawning => isSpawning;

    // Runs when the scene creates this manager and stores the singleton used by enemies.
    void Awake()
    {
        main = this;
    }

    // Runs once at scene start to begin the first wave automatically.
    void Start()
    {
        StartNextWave();
    }

    // Runs every frame to decide when to start the next wave and when all waves are complete.
    private void Update()
    {
        // Do not start a new wave while the current one is still being spawned.
        if (isSpawning)
        {
            return;
        }

        // When every spawned enemy has been removed, start the next wave if any remain.
        if (currentWave < totalWaves && activeEnemies.Count == 0)
        {
            StartNextWave();
        }

        // When all configured waves have finished and no enemies remain, announce victory once.
        if (currentWave >= totalWaves && activeEnemies.Count == 0 && !allWavesCompletedRaised)
        {
            allWavesCompletedRaised = true;
            AllWavesCompleted?.Invoke();
        }
    }

    // Called internally when the next wave should begin.
    private void StartNextWave()
    {
        // Stop if all configured waves have already been started.
        if (currentWave >= totalWaves)
        {
            return;
        }

        // Advance to the next human-readable wave number.
        currentWave++;

        // Notify the rest of the game that a new wave just began.
        WaveStarted?.Invoke(currentWave, totalWaves);

        // Start the coroutine that builds and spawns this wave over time.
        StartCoroutine(SpawnWaveRoutine(currentWave - 1));
    }

    // Coroutine started for each wave so delays and spawns can happen over time.
    private IEnumerator SpawnWaveRoutine(int waveNumber)
    {
        // Mark spawning as active so Update will not start another wave at the same time.
        isSpawning = true;

        // Wait between waves, but skip the delay before the very first wave.
        if (waveNumber > 0)
        {
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        // Build the full enemy list for this wave and randomize the order.
        List<EnemySpawnInfo> waveSet = Shuffle(BuildWave(waveNumber));

        // Spawn the wave contents one by one over time.
        yield return StartCoroutine(SpawnWave(waveSet));

        // Mark spawning as finished so the manager can watch for the wave being cleared.
        isSpawning = false;
    }

    // Called at the start of a wave to build the list of enemies and stats for that wave.
    private List<EnemySpawnInfo> BuildWave(int waveNumber)
    {
        // Create a fresh list that will hold every spawn entry for this wave.
        List<EnemySpawnInfo> waveSet = new List<EnemySpawnInfo>();

        // Add the basic enemy entries for this wave.
        AddEnemiesForWave(waveSet, basicEnemy, basicEnemyCount, basicEnemyGrowthPerWave, basicEnemyHealth, basicEnemyCoinReward, waveNumber);

        // Add the fast enemy entries for this wave.
        AddEnemiesForWave(waveSet, fastEnemy, fastEnemyCount, fastEnemyGrowthPerWave, fastEnemyHealth, fastEnemyCoinReward, waveNumber);

        // Add the tank enemy entries for this wave.
        AddEnemiesForWave(waveSet, tankEnemy, tankEnemyCount, tankEnemyGrowthPerWave, tankEnemyHealth, tankEnemyCoinReward, waveNumber);

        // Return the completed list so the coroutine can spawn it.
        return waveSet;
    }

    // Called by BuildWave to scale one enemy type upward each wave so later waves get harder.
    private void AddEnemiesForWave(
        List<EnemySpawnInfo> waveSet,
        GameObject prefab,
        int baseCount,
        int growthPerWave,
        int health,
        int coinReward,
        int waveNumber)
    {
        // Scale this enemy type upward based on the wave number so later waves contain more enemies.
        int enemyCount = Mathf.Max(0, baseCount + (waveNumber * growthPerWave));

        for (int i = 0; i < enemyCount; i++)
        {
            // Add one spawn entry containing the prefab and runtime stats that should be applied after instantiation.
            waveSet.Add(new EnemySpawnInfo
            {
                prefab = prefab,
                health = health,
                coinReward = coinReward
            });
        }
    }

    // Called before spawning to randomize the order of enemies in the wave.
    private List<EnemySpawnInfo> Shuffle(List<EnemySpawnInfo> waveSet)
    {
       // temp starts as a copy of the original wave list so items can be removed safely while shuffling.
       List<EnemySpawnInfo> temp = new List<EnemySpawnInfo>();

       // result will hold the randomized wave order.
       List<EnemySpawnInfo> result = new List<EnemySpawnInfo>();

       // Copy all original entries into the temporary list.
       temp.AddRange(waveSet);

       for (int i =0; i < waveSet.Count; i++)
       {
           // Pick a random remaining entry.
           int index = UnityEngine.Random.Range(0, temp.Count);

           // Move that entry into the result list.
           result.Add(temp[index]);

           // Remove it from temp so it cannot be selected again.
           temp.RemoveAt(index);
       }

       // Return the randomized spawn order.
       return result;
    }

    // Coroutine that instantiates enemies one by one and applies their per-type stats.
    private IEnumerator SpawnWave(List<EnemySpawnInfo> waveSet)
    {
        // Stop and warn if the scene is missing the core references needed to spawn enemies.
        if (spawnPoint == null || checkpoints == null || checkpoints.Length == 0)
        {
            Debug.LogError("EnemyManager spawn point or checkpoints are not set.", this);
            yield break;
        }

        for (int i = 0; i < waveSet.Count; i++)
        {
            // Skip any missing prefab entries instead of crashing the whole wave.
            if (waveSet[i].prefab == null)
            {
                continue;
            }

            // Create the enemy at the configured spawn point.
            GameObject enemyObject = Instantiate(waveSet[i].prefab, spawnPoint.position, Quaternion.identity);

            // Grab the Enemy script so runtime stats can be applied.
            Enemy enemy = enemyObject.GetComponent<Enemy>();
            if (enemy != null)
            {
                // Override the prefab defaults with the wave-specific health and coin reward.
                enemy.Initialize(waveSet[i].health, waveSet[i].coinReward);
            }

            // Wait before spawning the next balloon in the same wave.
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    // Called by each Enemy in Start so the manager can count living enemies in the current wave.
    public void RegisterEnemy(Enemy enemy)
    {
        // Ignore invalid registrations.
        if (enemy == null)
        {
            return;
        }

        // Add the enemy to the active set so the manager knows this wave is still alive.
        activeEnemies.Add(enemy);
    }

    // Called by each Enemy in OnDestroy so the manager knows when a wave has been cleared.
    public void UnregisterEnemy(Enemy enemy)
    {
        // Ignore invalid removals.
        if (enemy == null)
        {
            return;
        }

        // Remove the enemy from the active set when it dies or escapes.
        activeEnemies.Remove(enemy);
    }
}
