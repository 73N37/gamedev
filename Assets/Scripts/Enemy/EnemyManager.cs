using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public static EnemyManager main;
    public Transform[] checkpoints;
    public Transform spawnPoint;

    [Header("Basic Enemy settings")]
    [SerializeField] private GameObject basicEnemy;
    [SerializeField] private int basicEnemyCount = 2;
    [SerializeField] private int basicEnemyHealth = 1;
    [SerializeField] private int basicEnemyCoinReward = 1;

    [Header("Fast Enemy settings")]
    [SerializeField] private GameObject fastEnemy;
    [SerializeField] private int fastEnemyCount = 2;
    [SerializeField] private int fastEnemyHealth = 2;
    [SerializeField] private int fastEnemyCoinReward = 2;

    [Header("Tank Enemy settings")]
    [SerializeField] private GameObject tankEnemy;
    [SerializeField] private int tankEnemyCount = 1;
    [SerializeField] private int tankEnemyHealth = 5;
    [SerializeField] private int tankEnemyCoinReward = 3;

    [Header("Wave settings")]
    [SerializeField] private int totalWaves = 3;
    [SerializeField] private float spawnDelay = 0.5f;
    [SerializeField] private float timeBetweenWaves = 2f;
    private int currentWave = 0;
    private bool isSpawning;
    private readonly HashSet<Enemy> activeEnemies = new HashSet<Enemy>();
    private bool allWavesCompletedRaised;

    public event Action<int, int> WaveStarted;
    public event Action AllWavesCompleted;

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
        if (isSpawning)
        {
            return;
        }

        if (currentWave < totalWaves && activeEnemies.Count == 0)
        {
            StartNextWave();
        }

        if (currentWave >= totalWaves && activeEnemies.Count == 0 && !allWavesCompletedRaised)
        {
            allWavesCompletedRaised = true;
            AllWavesCompleted?.Invoke();
        }
    }

    // Called internally when the next wave should begin.
    private void StartNextWave()
    {
        if (currentWave >= totalWaves)
        {
            return;
        }

        currentWave++;
        WaveStarted?.Invoke(currentWave, totalWaves);
        StartCoroutine(SpawnWaveRoutine(currentWave - 1));
    }

    // Coroutine started for each wave so delays and spawns can happen over time.
    private IEnumerator SpawnWaveRoutine(int waveNumber)
    {
        isSpawning = true;

        if (waveNumber > 0)
        {
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        List<EnemySpawnInfo> waveSet = Shuffle(BuildWave(waveNumber));
        yield return StartCoroutine(SpawnWave(waveSet));

        isSpawning = false;
    }

    // Called at the start of a wave to build the list of enemies and stats for that wave.
    private List<EnemySpawnInfo> BuildWave(int waveNumber)
    {
        int currentBasicEnemyCount = basicEnemyCount + waveNumber;
        int currentFastEnemyCount = fastEnemyCount + waveNumber;
        int currentTankEnemyCount = tankEnemyCount + waveNumber;
        List<EnemySpawnInfo> waveSet = new List<EnemySpawnInfo>();

        for (int i = 0; i < currentBasicEnemyCount; i++)
        {
            waveSet.Add(new EnemySpawnInfo
            {
                prefab = basicEnemy,
                health = basicEnemyHealth,
                coinReward = basicEnemyCoinReward
            });
        }

        for (int i = 0; i < currentFastEnemyCount; i++)
        {
            waveSet.Add(new EnemySpawnInfo
            {
                prefab = fastEnemy,
                health = fastEnemyHealth,
                coinReward = fastEnemyCoinReward
            });
        }

        for (int i = 0; i < currentTankEnemyCount; i++)
        {
            waveSet.Add(new EnemySpawnInfo
            {
                prefab = tankEnemy,
                health = tankEnemyHealth,
                coinReward = tankEnemyCoinReward
            });
        }

        return waveSet;
    }

    // Called before spawning to randomize the order of enemies in the wave.
    private List<EnemySpawnInfo> Shuffle(List<EnemySpawnInfo> waveSet)
    {
       List<EnemySpawnInfo> temp = new List<EnemySpawnInfo>();
       List<EnemySpawnInfo> result = new List<EnemySpawnInfo>();
       temp.AddRange(waveSet);
       for (int i =0; i < waveSet.Count; i++)
       {
           int index = UnityEngine.Random.Range(0, temp.Count);
           result.Add(temp[index]);
           temp.RemoveAt(index);
       }
       return result;
    }

    // Coroutine that instantiates enemies one by one and applies their per-type stats.
    private IEnumerator SpawnWave(List<EnemySpawnInfo> waveSet)
    {
        if (spawnPoint == null || checkpoints == null || checkpoints.Length == 0)
        {
            Debug.LogError("EnemyManager spawn point or checkpoints are not set.", this);
            yield break;
        }

        for (int i = 0; i < waveSet.Count; i++)
        {
            if (waveSet[i].prefab == null)
            {
                continue;
            }

            GameObject enemyObject = Instantiate(waveSet[i].prefab, spawnPoint.position, Quaternion.identity);
            Enemy enemy = enemyObject.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.Initialize(waveSet[i].health, waveSet[i].coinReward);
            }

            yield return new WaitForSeconds(spawnDelay);
        }
    }

    // Called by each Enemy in Start so the manager can count living enemies in the current wave.
    public void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        activeEnemies.Add(enemy);
    }

    // Called by each Enemy in OnDestroy so the manager knows when a wave has been cleared.
    public void UnregisterEnemy(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        activeEnemies.Remove(enemy);
    }
}
