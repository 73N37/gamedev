using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager main;
    public Transform[] checkpoints;
    public Transform spawnPoint;

    [SerializeField] private GameObject enemy;
    [SerializeField] private GameObject fastEnemy;
    [SerializeField] private GameObject tankEnemy;
    [SerializeField] private int enemyCount = 6;
    private int fastEnemyCount;
    private int tankEnemyCount;
    private List<GameObject> waveset;

    private float enemyRate = 0.5f;
    private float fastEnemyRate = 0.7f;
    private float tankEnemyRate = 0.3f;
    
    void Awake()
    {
        main = this;
    }

    void Start()
    {
        SetWave();
    }

    private void SetWave()
    {
        enemyCount = Mathf.RoundToInt(enemyRate + enemyCount);
        fastEnemyCount = Mathf.RoundToInt(fastEnemyRate + enemyCount);
        tankEnemyCount = Mathf.RoundToInt(tankEnemyRate + enemyCount);

        waveset = new List<GameObject>();
        for (int i = 0; i < enemyCount; i++)
        {
            waveset.Add(enemy);
        }
        for (int i = 0; i < fastEnemyCount; i++)
        {
            waveset.Add(fastEnemy);
        }
        for (int i = 0; i < tankEnemyCount; i++)
        {
            waveset.Add(tankEnemy);
        }

        StartCoroutine(Spawn());
    }   

IEnumerator Spawn()
    {
        for (int i = 0; i < waveset.Count; i++)
        {
            Instantiate(waveset[i], spawnPoint.position, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
