using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    // Singleton instance to allow easy access from other scripts (e.g., GameManager.main.AddCurrency(10))
    public static GameManager main { get; private set; }

    [Header("Game Settings")]
    // Initial amount of health/lives the player starts with
    [SerializeField] private int startingLives = 20;
    // Initial amount of money/currency the player starts with to buy towers
    [SerializeField] private int startingCurrency = 500;
    // Reference to the EnemyManager which handles wave spawning logic
    [SerializeField] private EnemyManager enemyManager;

    // Events that other scripts (like UI managers) can subscribe to in order to react to game state changes without tight coupling.
    // Triggered when lives change (current lives, starting lives)
    public event Action<int, int> LivesChanged;
    // Triggered when currency changes (current currency)
    public event Action<int> CurrencyChanged;
    // Triggered when the wave changes (current wave, total waves)
    public event Action<int, int> WaveChanged;
    // Triggered when the game ends (is game over, did player win)
    public event Action<bool, bool> GameStateChanged;

    // Current amount of lives remaining. The 'private set' ensures it can only be modified from within this script.
    public int Lives { get; private set; }
    // Current amount of currency available.
    public int Currency { get; private set; }
    // Fetches the current wave number from the EnemyManager, safely handling null references
    public int CurrentWave => enemyManager != null ? enemyManager.CurrentWave : 0;
    // Fetches the total number of waves from the EnemyManager, safely handling null references
    public int TotalWaves => enemyManager != null ? enemyManager.TotalWaves : 0;
    // Flags to track the current state of the game
    public bool IsGameOver { get; private set; }
    public bool IsGameWon { get; private set; }
    // Helper property to check if the game is paused by checking Unity's Time.timeScale (0 means paused, > 0 means running)
    public bool IsPaused => Time.timeScale <= 0f;

    private void Awake()
    {
        // Singleton pattern implementation:
        // If an instance already exists and it's not this one, destroy this duplicate to enforce a single GameManager.
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }

        // Set the static reference to this instance
        main = this;
    }

    private void Start()
    {
        // Automatically find the EnemyManager in the scene if it wasn't assigned in the inspector
        if (enemyManager == null)
        {
            enemyManager = FindFirstObjectByType<EnemyManager>();
        }

        // Initialize game values, ensuring they don't start below their minimum logical values (at least 1 life, at least 0 money)
        Lives = Mathf.Max(1, startingLives);
        Currency = Mathf.Max(0, startingCurrency);

        // Subscribe to events from the EnemyManager to know when waves start and end
        if (enemyManager != null)
        {
            enemyManager.WaveStarted += HandleWaveStarted;
            enemyManager.AllWavesCompleted += HandleAllWavesCompleted;
        }

        // Invoke initial events so that UI elements listening can initialize their display values immediately on start
        LivesChanged?.Invoke(Lives, startingLives);
        CurrencyChanged?.Invoke(Currency);
        WaveChanged?.Invoke(CurrentWave, TotalWaves);
        GameStateChanged?.Invoke(IsGameOver, IsGameWon);
    }

    private void OnDestroy()
    {
        // Only clear the main reference if this instance is the current main. 
        // This prevents accidentally clearing the reference if a duplicate GameManager was destroyed in Awake.
        if (main == this)
        {
            main = null;
        }

        // Unsubscribe from events to prevent memory leaks or calling methods on destroyed objects
        if (enemyManager != null) 
        {
            enemyManager.WaveStarted -= HandleWaveStarted;
            enemyManager.AllWavesCompleted -= HandleAllWavesCompleted;
        }
    }

    // Attempts to deduct currency, typically called when buying a tower or upgrading
    public bool TrySpendCurrency(int amount)
    {
        // Safety check to prevent gaining money by attempting to spend a negative amount
        if (amount < 0)
        {
            Debug.LogWarning("Tried to spend a negative amount of currency.", this);
            return false;
        }

        if (Currency < amount || IsGameOver)
        {
            return false;
        }

        // Deduct the amount and notify listeners (like the UI) of the new total
        Currency -= amount;
        CurrencyChanged?.Invoke(Currency);
        return true; // Successfully spent
    }

    // Adds money to the player's balance
    public void AddCurrency(int amount)
    {
        // Safety checks: ignore non-positive amounts and don't add money if the game is over
        if (amount <= 0 || IsGameOver)
        {
            return;
        }

        // Increase currency and notify listeners
        Currency += amount;
        CurrencyChanged?.Invoke(Currency);
    }

    // Called when a tower kills an enemy. Grants a reward.
    public void EnemyDefeated(int reward)
    {
        AddCurrency(reward);
    } 
    

    // Called when an enemy reaches the end of the path
    public void EnemyEscaped(Enemy enemy, int lifePenalty)
    {
        // Ignore if the game has already ended
        if (IsGameOver)
        {
            return;
        }

        // Deduct lives based on the enemy's penalty value
        DamageBase(lifePenalty);
    }

    // Reduces player lives, typically called when enemies leak
    public void DamageBase(int amount)
    {
        // Ignore non-positive damage or damage after the game ends
        if (amount <= 0 || IsGameOver)
        {
            return;
        }

        // Reduce lives, clamping to 0 so we don't display negative lives
        Lives = Mathf.Max(0, Lives - amount);
        // Notify listeners (UI) that lives have changed
        LivesChanged?.Invoke(Lives, startingLives);

        // Check for loss condition
        if (Lives == 0)
        {
            EndGame(false); // Game over, player lost
        }
    }

    // Pauses or unpauses the game
    public void TogglePause()
    {
        // Don't allow pausing/unpausing if the game is already over
        if (IsGameOver)
        {
            return;
        }

        // Toggle Time.timeScale: 0 pauses game physics and updates, 1 resumes normal speed
        Time.timeScale = IsPaused ? 1f : 0f;
    }

    // Reloads the current scene to restart the game
    public void RestartLevel()
    {
        // Ensure time scale is reset to normal speed before loading, otherwise the new scene might start frozen
        Time.timeScale = 1f;
        // Reload the scene that is currently active
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Event handler for when a new wave starts in the EnemyManager
    private void HandleWaveStarted(int waveNumber, int totalWaves)
    {
        // Forward the event to GameManager listeners (like UI wave counters)
        WaveChanged?.Invoke(waveNumber, totalWaves);
    }

    // Event handler for when all waves have been cleared
    private void HandleAllWavesCompleted()
    {
        // If the game isn't already lost, trigger a win
        if (!IsGameOver)
        {
            EndGame(true); // Game over, player won
        }
    }

    // Handles the end of game logic (both win and loss scenarios)
    private void EndGame(bool playerWon)
    {
        IsGameOver = true;
        IsGameWon = playerWon;
        
        // Reset time scale to 1 in case the game ended while the player had it paused
        Time.timeScale = 1f;
        
        // Notify listeners (like Game Over UI screens) of the result
        GameStateChanged?.Invoke(IsGameOver, IsGameWon);
    }
}
