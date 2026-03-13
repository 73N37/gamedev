using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager main;
    public Transform[] checkpoints;
    public Transform spawnPoint;

    [Header("Fast Enemy settings")]
    [SerializeField] private GameObject fastEnemy;
    [SerializeField] private int fastEnemyCount = 2;

    [Header("Basic Enemy settings")]
    [SerializeField] private GameObject enemy;
    [SerializeField] private int enemyCount = 2;

    [Header("Tank Enemy settings")]
    [SerializeField] private GameObject tankEnemy;
    [SerializeField] private int tankEnemyCount = 1;

    [Header("Wave settings")]
    [SerializeField] private int totalWaves = 3;
    [SerializeField] private float spawnDelay = 0.5f;
    [SerializeField] private float timeBetweenWaves = 2f;

    

    void Awake()
    {
        main = this;
    }

    void Start()
    {
        StartCoroutine(SpawnWaves());
    }

private void Update()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        if  
        (
            enemies.Length == 0
        )
        {
            enemyCount += Mathf.RoundToInt(enemyCount * spawnDelay);
            SpawnWaves();
        }
    }

    private IEnumerator SpawnWaves()
    {
        for (int waveNumber = 0; waveNumber < totalWaves; waveNumber++)
        {
            List<GameObject> waveSet = BuildWave(waveNumber);
            yield return StartCoroutine(SpawnWave(waveSet));

            if (waveNumber < totalWaves - 1)
            {
                yield return new WaitForSeconds(timeBetweenWaves);
            }
        }
    }

    private List<GameObject> BuildWave(int waveNumber)
    {
        int currentEnemyCount = enemyCount + waveNumber;
        int currentFastEnemyCount = fastEnemyCount + waveNumber;
        int currentTankEnemyCount = tankEnemyCount + waveNumber;

        List<GameObject> waveSet = new List<GameObject>();

        for (int i = 0; i < currentEnemyCount; i++) {
            waveSet.Add(enemy);
        }

        for (int i = 0; i < currentFastEnemyCount; i++) {
            waveSet.Add(fastEnemy);
        }

        for (int i = 0; i < currentTankEnemyCount; i++) {
            waveSet.Add(tankEnemy);
        }

        return waveSet;
    }

    private IEnumerator SpawnWave(List<GameObject> waveSet)
    {
        if (spawnPoint == null)
        {
            Debug.LogError("Spawn point is not set on EnemyManager.", this);
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
}
