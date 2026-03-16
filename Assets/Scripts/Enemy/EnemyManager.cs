using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager main;
    public Transform[] checkpoints;
    public Transform spawnPoint;

    [Header("Basic Enemy settings")]
    [SerializeField] private GameObject basicEnemy;
    [SerializeField] private int basicEnemyCount = 2;

    [Header("Fast Enemy settings")]
    [SerializeField] private GameObject fastEnemy;
    [SerializeField] private int fastEnemyCount = 2;

    [Header("Tank Enemy settings")]
    [SerializeField] private GameObject tankEnemy;
    [SerializeField] private int tankEnemyCount = 1;

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

    void Awake()
    {
        main = this;
    }

    void Start()
    {
        StartNextWave();
    }

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

    private IEnumerator SpawnWaveRoutine(int waveNumber)
    {
        isSpawning = true;

        if (waveNumber > 0)
        {
            yield return new WaitForSeconds(timeBetweenWaves);
        }

        List<GameObject> waveSet = Shuffle(BuildWave(waveNumber));
        yield return StartCoroutine(SpawnWave(waveSet));

        isSpawning = false;
    }

    private List<GameObject> BuildWave(int waveNumber)
    {
        int currentBasicEnemyCount = basicEnemyCount + waveNumber;
        int currentFastEnemyCount = fastEnemyCount + waveNumber;
        int currentTankEnemyCount = tankEnemyCount + waveNumber;
        List<GameObject> waveSet = new List<GameObject>();

        for (int i = 0; i < currentBasicEnemyCount; i++) {
            waveSet.Add(basicEnemy);
        }

        for (int i = 0; i < currentFastEnemyCount; i++) {
            waveSet.Add(fastEnemy);
        }

        for (int i = 0; i < currentTankEnemyCount; i++) {
            waveSet.Add(tankEnemy);
        }

        return waveSet;
    }

    private List<GameObject> Shuffle(List<GameObject> waveSet)
    {
       List<GameObject> temp = new List<GameObject>();
       List<GameObject> result = new List<GameObject>();
       temp.AddRange(waveSet);
       for (int i =0; i < waveSet.Count; i++)
       {
           int index = UnityEngine.Random.Range(0, temp.Count);
           result.Add(temp[index]);
           temp.RemoveAt(index);
       }
       return result;
    }

    private IEnumerator SpawnWave(List<GameObject> waveSet)
    {
        if (spawnPoint == null || checkpoints == null || checkpoints.Length == 0)
        {
            Debug.LogError("EnemyManager spawn point or checkpoints are not set.", this);
            yield break;
        }

        for (int i = 0; i < waveSet.Count; i++)
        {
            if (waveSet[i] == null)
            {
                continue;
            }

            Instantiate(waveSet[i], spawnPoint.position, Quaternion.identity);
            yield return new WaitForSeconds(spawnDelay);
        }
    }

    public void RegisterEnemy(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        if (enemy == null)
        {
            return;
        }

        activeEnemies.Remove(enemy);
    }
}
