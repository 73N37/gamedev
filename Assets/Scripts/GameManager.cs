using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager main { get; private set; }

    [Header("Game Settings")]
    [SerializeField] private int startingLives = 100;
    [SerializeField] private int startingCurrency = 0;
    [SerializeField] private EnemyManager enemyManager;

    [Header("HUD")]
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Text livesText;
    [SerializeField] private Text currencyText;
    [SerializeField] private Text waveText;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameManagerExists()
    {
        if (FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();
        }
    }

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

        EnsureHudExists();

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

        UpdateLivesHud();
        UpdateCurrencyHud();
        UpdateWaveHud(CurrentWave, TotalWaves);
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
        UpdateCurrencyHud();
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
        UpdateLivesHud();

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
        UpdateWaveHud(waveNumber, totalWaves);
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

    private void EnsureHudExists()
    {
        if (livesText != null && currencyText != null && waveText != null)
        {
            return;
        }

        if (hudCanvas == null)
        {
            GameObject canvasObject = new GameObject("HUD Canvas");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Font hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform hudParent = EnsureHudPanel();

        if (livesText == null)
        {
            livesText = CreateHudText("Lives Text", hudParent, hudFont, new Vector2(16f, -14f));
        }

        if (currencyText == null)
        {
            currencyText = CreateHudText("Coins Text", hudParent, hudFont, new Vector2(16f, -44f));
        }

        if (waveText == null)
        {
            waveText = CreateHudText("Wave Text", hudParent, hudFont, new Vector2(16f, -74f));
        }
    }

    private Text CreateHudText(string objectName, Transform parent, Font font, Vector2 anchoredPosition)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(220f, 28f);

        return text;
    }

    private RectTransform EnsureHudPanel()
    {
        Transform existingPanel = hudCanvas.transform.Find("HUD Panel");
        if (existingPanel != null)
        {
            return existingPanel as RectTransform;
        }

        GameObject panelObject = new GameObject("HUD Panel");
        panelObject.transform.SetParent(hudCanvas.transform, false);

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.35f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(16f, -16f);
        panelRect.sizeDelta = new Vector2(260f, 110f);

        return panelRect;
    }

    private void UpdateLivesHud()
    {
        if (livesText != null)
        {
            livesText.text = $"Health: {Lives}";
        }
    }

    private void UpdateCurrencyHud()
    {
        if (currencyText != null)
        {
            currencyText.text = $"Coins: {Currency}";
        }
    }

    private void UpdateWaveHud(int waveNumber, int totalWaves)
    {
        if (waveText != null)
        {
            waveText.text = $"Wave: {waveNumber}/{Mathf.Max(totalWaves, 1)}";
        }
    }
}
