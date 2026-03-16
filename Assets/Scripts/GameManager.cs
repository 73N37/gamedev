using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager main { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private int startingLives = 20;
    [SerializeField] private int startingCurrency = 500;
    [SerializeField] private EnemyManager enemyManager;

    public event Action<int, int> LivesChanged;
    public event Action<int> CurrencyChanged;
    public event Action<int, int> WaveChanged;
    public event Action<bool, bool> GameStateChanged;

    public int Lives { get; private set; }
    public int Currency { get; private set; }
    public int CurrentWave => enemyManager != null ? enemyManager.CurrentWave : 0;
    public int TotalWaves => enemyManager != null ? enemyManager.TotalWaves : 0;
    public bool IsGameOver { get; private set; }
    public bool IsGameWon { get; private set; }
    public bool IsPaused => Time.timeScale <= 0f;

    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }

        main = this;
    }

    private void Start()
    {
        if (enemyManager == null)
        {
            enemyManager = FindFirstObjectByType<EnemyManager>();
        }

        Lives = Mathf.Max(1, startingLives);
        Currency = Mathf.Max(0, startingCurrency);

        if (enemyManager != null)
        {
            enemyManager.WaveStarted += HandleWaveStarted;
            enemyManager.AllWavesCompleted += HandleAllWavesCompleted;
        }

        LivesChanged?.Invoke(Lives, startingLives);
        CurrencyChanged?.Invoke(Currency);
        WaveChanged?.Invoke(CurrentWave, TotalWaves);
        GameStateChanged?.Invoke(IsGameOver, IsGameWon);
    }

    private void OnDestroy()
    {
        if (main == this)
        {
            main = null;
        }

        if (enemyManager != null)
        {
            enemyManager.WaveStarted -= HandleWaveStarted;
            enemyManager.AllWavesCompleted -= HandleAllWavesCompleted;
        }
    }

    public bool TrySpendCurrency(int amount)
    {
        if (amount < 0)
        {
            Debug.LogWarning("Tried to spend a negative amount of currency.", this);
            return false;
        }

        if (Currency < amount || IsGameOver)
        {
            return false;
        }

        Currency -= amount;
        CurrencyChanged?.Invoke(Currency);
        return true;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0 || IsGameOver)
        {
            return;
        }

        Currency += amount;
        CurrencyChanged?.Invoke(Currency);
    }

    public void EnemyDefeated(int reward)
    {
        AddCurrency(reward);
    }

    public void EnemyEscaped(Enemy enemy, int lifePenalty)
    {
        if (IsGameOver)
        {
            return;
        }

        DamageBase(lifePenalty);
    }

    public void DamageBase(int amount)
    {
        if (amount <= 0 || IsGameOver)
        {
            return;
        }

        Lives = Mathf.Max(0, Lives - amount);
        LivesChanged?.Invoke(Lives, startingLives);

        if (Lives == 0)
        {
            EndGame(false);
        }
    }

    public void TogglePause()
    {
        if (IsGameOver)
        {
            return;
        }

        Time.timeScale = IsPaused ? 1f : 0f;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void HandleWaveStarted(int waveNumber, int totalWaves)
    {
        WaveChanged?.Invoke(waveNumber, totalWaves);
    }

    private void HandleAllWavesCompleted()
    {
        if (!IsGameOver)
        {
            EndGame(true);
        }
    }

    private void EndGame(bool playerWon)
    {
        IsGameOver = true;
        IsGameWon = playerWon;
        Time.timeScale = 1f;
        GameStateChanged?.Invoke(IsGameOver, IsGameWon);
    }
}
