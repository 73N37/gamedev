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

    

    void Awake()
    {
        main = this;
    }

    void Start()
    {
        StartCoroutine(SpawnWaves());
    }

private void Update()    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if  
        (enemies.Length == 0){
            basicEnemyCount += Mathf.RoundToInt(basicEnemyCount * spawnDelay);
            SpawnWaves();
        }
    }

    private IEnumerator SpawnWaves(){
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
           int index = Random.Range(0, temp.Count);
           result.Add(temp[index]);
           temp.RemoveAt(index);
       }
       return result;
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
