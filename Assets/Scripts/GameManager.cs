using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// GameManager is the "big picture" controller for the whole match.
// It sits above the tower, enemy, and projectile scripts and answers questions like:
// - How much health does the player have left?
// - How many coins has the player earned and spent?
// - Which wave is currently active?
// - Has the player won or lost the level?
// - Is the tower shop open, and which tower is selected?
//
// In moment-to-moment gameplay, the flow usually looks like this:
// 1. EnemyManager starts a wave and spawns balloons.
// 2. Towers detect balloons, aim, and shoot projectiles.
// 3. Projectiles damage enemies.
// 4. Enemies either die and reward coins, or escape and damage the player.
// 5. GameManager updates the HUD and tower shop so the player sees the latest state.
// 6. When all waves are cleared, or health reaches zero, GameManager ends the match.
//
// This script also creates the HUD and upgrade shop at runtime so the scene does not need
// a manually built canvas to show health, coins, wave number, or tower upgrade buttons.
// Central game-state controller that also builds the HUD and tower shop at runtime.
public class GameManager : MonoBehaviour
{
    // Singleton reference used by the rest of the project to find the active GameManager quickly.
    public static GameManager main { get; private set; }

    // These serialized values define the starting state of the player when the scene begins.
    [Header("Game Settings")]
    [SerializeField] private int startingLives = 100;
    [SerializeField] private int startingCurrency = 0;

    // This links GameManager to the system that owns wave progression and enemy spawning.
    [SerializeField] private EnemyManager enemyManager;

    // These runtime-only references are created automatically when the fallback HUD is built.
    private Canvas hudCanvas;
    private Font hudFont;
    private Text livesText;
    private Text currencyText;
    private Text waveText;

    // These runtime-only references belong to the tower shop panel that appears when a tower is clicked.
    private GameObject shopPanel;
    private Text shopTitleText;
    private Text shopRangeText;
    private Text shopDamageText;
    private Text shopFireRateText;
    private Button rangeUpgradeButton;
    private Button damageUpgradeButton;
    private Button fireRateUpgradeButton;
    private Button closeShopButton;
    private Text rangeUpgradeButtonText;
    private Text damageUpgradeButtonText;
    private Text fireRateUpgradeButtonText;
    private Tower selectedTower;

    // These events are optional extension points for UI or other systems that want live updates.
    public event Action<int, int> LivesChanged;
    public event Action<int> CurrencyChanged;
    public event Action<int, int> WaveChanged;
    public event Action<bool, bool> GameStateChanged;

    // These properties expose the core match state in a safe read-only way to the rest of the project.
    public int Lives { get; private set; }
    public int Currency { get; private set; }
    public int CurrentWave => enemyManager != null ? enemyManager.CurrentWave : 0;
    public int TotalWaves => enemyManager != null ? enemyManager.TotalWaves : 0;
    public bool IsGameOver { get; private set; }
    public bool IsGameWon { get; private set; }
    public bool IsPaused => Time.timeScale <= 0f;

    // Runs after a scene loads to guarantee there is always one active GameManager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureGameManagerExists()
    {
        if (FindFirstObjectByType<GameManager>() == null)
        {
            GameObject gameManagerObject = new GameObject("GameManager");
            gameManagerObject.AddComponent<GameManager>();
        }
    }

    // Runs when this object is created and sets up the singleton reference.
    private void Awake()
    {
        if (main != null && main != this)
        {
            Destroy(gameObject);
            return;
        }

        main = this;
    }

    // Runs every frame to handle tower selection clicks for the shop.
    private void Update()
    {
        HandleTowerSelectionInput();
    }

    // Runs once after Awake to find dependencies, create the HUD, and push the starting values.
    private void Start()
    {
        // If the wave system was not linked in the Inspector, find it in the current scene automatically.
        if (enemyManager == null)
        {
            enemyManager = FindFirstObjectByType<EnemyManager>();
        }

        // Build the fallback HUD before any values are pushed into its text fields.
        EnsureHudExists();

        // Clamp the starting values so the player never starts dead or with negative coins.
        Lives = Mathf.Max(1, startingLives);
        Currency = Mathf.Max(0, startingCurrency);

        // Listen to wave events so the HUD stays synchronized with the spawning system.
        if (enemyManager != null)
        {
            enemyManager.WaveStarted += HandleWaveStarted;
            enemyManager.AllWavesCompleted += HandleAllWavesCompleted;
        }

        // Broadcast the starting state once so other scripts can initialize from the correct values.
        LivesChanged?.Invoke(Lives, startingLives);
        CurrencyChanged?.Invoke(Currency);
        WaveChanged?.Invoke(CurrentWave, TotalWaves);
        GameStateChanged?.Invoke(IsGameOver, IsGameWon);

        // Write the initial health, coin, and wave text into the HUD immediately.
        UpdateLivesHud();
        UpdateCurrencyHud();
        UpdateWaveHud(CurrentWave, TotalWaves);
    }

    // Runs when the manager is destroyed so subscriptions and singleton state are cleaned up.
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

    // Called by tower upgrades when the player tries to spend coins.
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
        UpdateCurrencyHud();
        RefreshTowerShop();
        return true;
    }

    // Called whenever the game awards coins so the stored value and HUD stay in sync.
    public void AddCurrency(int amount)
    {
        if (amount <= 0 || IsGameOver)
        {
            return;
        }

        Currency += amount;
        CurrencyChanged?.Invoke(Currency);
        UpdateCurrencyHud();
        RefreshTowerShop();
    }

    // Called by an Enemy when it is popped so the player receives that balloon's reward.
    public void EnemyDefeated(int reward)
    {
        AddCurrency(reward);
    }

    // Called by an Enemy when it reaches the final waypoint instead of being popped.
    public void EnemyEscaped(Enemy enemy, int lifePenalty)
    {
        if (IsGameOver)
        {
            return;
        }

        DamageBase(lifePenalty);
    }

    // Called whenever the player should lose health, usually after a balloon escapes.
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

    // Called by pause UI or input to freeze and unfreeze gameplay time.
    public void TogglePause()
    {
        if (IsGameOver)
        {
            return;
        }

        Time.timeScale = IsPaused ? 1f : 0f;
    }

    // Called by UI or end-game flow to reload the current scene from the beginning.
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Called by a Tower when the player clicks it and wants to open that tower's shop panel.
    public void OpenTowerShop(Tower tower)
    {
        if (tower == null)
        {
            return;
        }

        EnsureHudExists();
        EnsureShopExists();

        selectedTower = tower;
        shopPanel.SetActive(true);
        shopPanel.transform.SetAsLastSibling();
        RefreshTowerShop();
    }

    // Called when the player clicks away from towers or presses the close button on the shop panel.
    public void CloseTowerShop()
    {
        selectedTower = null;

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    // Called whenever currency or tower upgrade data changes so the shop stays current.
    public void RefreshTowerShop()
    {
        // If the runtime shop panel has not been built yet, there is nothing to update.
        if (shopPanel == null)
        {
            return;
        }

        // If the selected tower no longer exists, close the shop instead of showing stale data.
        if (selectedTower == null)
        {
            CloseTowerShop();
            return;
        }

        // Show the selected tower name so the player knows which tower is being upgraded.
        shopTitleText.text = $"{selectedTower.name} Shop";

        // Show the current range tier and the actual numeric range value.
        shopRangeText.text = $"Range Tier: {selectedTower.RangeUpgradeTier}/{selectedTower.MaxRangeUpgradeTier}\nCurrent Range: {selectedTower.Range:F1}";

        // Show the current damage tier and the current damage dealt per shot.
        shopDamageText.text = $"Damage Tier: {selectedTower.DamageUpgradeTier}/{selectedTower.MaxDamageUpgradeTier}\nCurrent Damage: {selectedTower.CurrentDamage}";

        // Show the current fire-rate tier and the current delay between shots.
        shopFireRateText.text = $"Fire Rate Tier: {selectedTower.FireRateUpgradeTier}/{selectedTower.MaxFireRateUpgradeTier}\nCurrent Delay: {selectedTower.CurrentFireRate:F2}s";

        if (selectedTower.CanUpgradeRange)
        {
            // Show the next range-upgrade price and enable the button only when the player can afford it.
            shopRangeText.text += $"\nNext Upgrade Cost: {selectedTower.NextRangeUpgradeCost} coins";
            rangeUpgradeButton.interactable = Currency >= selectedTower.NextRangeUpgradeCost;
            rangeUpgradeButtonText.text = "Upgrade Range";
        }
        else
        {
            // If the path is maxed, replace the price with a finished-state message.
            shopRangeText.text += "\nMax Tier Reached";
            rangeUpgradeButton.interactable = false;
            rangeUpgradeButtonText.text = "Range Maxed";
        }

        if (selectedTower.CanUpgradeDamage)
        {
            // Show the next damage-upgrade price and enable the button only when the player can afford it.
            shopDamageText.text += $"\nNext Upgrade Cost: {selectedTower.NextDamageUpgradeCost} coins";
            damageUpgradeButton.interactable = Currency >= selectedTower.NextDamageUpgradeCost;
            damageUpgradeButtonText.text = "Upgrade Damage";
        }
        else
        {
            // If the path is maxed, replace the price with a finished-state message.
            shopDamageText.text += "\nMax Tier Reached";
            damageUpgradeButton.interactable = false;
            damageUpgradeButtonText.text = "Damage Maxed";
        }

        if (selectedTower.CanUpgradeFireRate)
        {
            // Show the next fire-rate-upgrade price and enable the button only when the player can afford it.
            shopFireRateText.text += $"\nNext Upgrade Cost: {selectedTower.NextFireRateUpgradeCost} coins";
            fireRateUpgradeButton.interactable = Currency >= selectedTower.NextFireRateUpgradeCost;
            fireRateUpgradeButtonText.text = "Upgrade Fire Rate";
        }
        else
        {
            // If the path is maxed, replace the price with a finished-state message.
            shopFireRateText.text += "\nMax Tier Reached";
            fireRateUpgradeButton.interactable = false;
            fireRateUpgradeButtonText.text = "Fire Rate Maxed";
        }
    }

    // Runs when EnemyManager announces a new wave so events and HUD text stay current.
    private void HandleWaveStarted(int waveNumber, int totalWaves)
    {
        WaveChanged?.Invoke(waveNumber, totalWaves);
        UpdateWaveHud(waveNumber, totalWaves);
    }

    // Runs when the final wave is cleared and no living enemies remain.
    private void HandleAllWavesCompleted()
    {
        if (!IsGameOver)
        {
            EndGame(true);
        }
    }

    // Called internally when the player wins or loses to lock in the final game state.
    private void EndGame(bool playerWon)
    {
        IsGameOver = true;
        IsGameWon = playerWon;
        Time.timeScale = 1f;
        CloseTowerShop();
        GameStateChanged?.Invoke(IsGameOver, IsGameWon);
    }

    // Runs during startup to create the HUD automatically if the scene has none assigned.
    private void EnsureHudExists()
    {
        // If the three main HUD labels already exist, the fallback UI has already been built.
        if (livesText != null && currencyText != null && waveText != null)
        {
            return;
        }

        // Create the screen-space canvas that will hold the auto-generated HUD and shop.
        if (hudCanvas == null)
        {
            GameObject canvasObject = new GameObject("HUD Canvas");
            hudCanvas = canvasObject.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        // Load the built-in font used by the runtime text objects.
        if (hudFont == null)
        {
            hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // Create or reuse the top-left HUD background panel.
        RectTransform hudParent = EnsureHudPanel();

        if (livesText == null)
        {
            // Build the text label that shows player health.
            livesText = CreateHudText("Lives Text", hudParent, new Vector2(16f, -14f));
        }

        if (currencyText == null)
        {
            // Build the text label that shows the player's coins.
            currencyText = CreateHudText("Coins Text", hudParent, new Vector2(16f, -44f));
        }

        if (waveText == null)
        {
            // Build the text label that shows the current wave.
            waveText = CreateHudText("Wave Text", hudParent, new Vector2(16f, -74f));
        }

        // Make sure buttons in the runtime shop can receive click events.
        EnsureUiEventSystem();
    }

    // Called only while building runtime UI to create one text label with the shared HUD font.
    private Text CreateHudText(string objectName, Transform parent, Vector2 anchoredPosition)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = hudFont;
        text.fontSize = 24;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(220f, 28f);

        return text;
    }

    // Called while building the fallback HUD to reuse or create the top-left status panel.
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

    // Runs during setup to create the tower shop panel and its buttons the first time they are needed.
    private void EnsureShopExists()
    {
        if (shopPanel != null)
        {
            return;
        }

        GameObject panelObject = new GameObject("Tower Shop Panel");
        panelObject.transform.SetParent(hudCanvas.transform, false);
        shopPanel = panelObject;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.5f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-16f, -16f);
        panelRect.sizeDelta = new Vector2(320f, 340f);

        shopTitleText = CreateHudText("Shop Title", panelRect, new Vector2(16f, -14f));
        shopTitleText.fontSize = 26;

        shopRangeText = CreateHudText("Range Info", panelRect, new Vector2(16f, -54f));
        shopRangeText.fontSize = 20;
        shopRangeText.alignment = TextAnchor.UpperLeft;
        shopRangeText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);

        shopDamageText = CreateHudText("Damage Info", panelRect, new Vector2(16f, -132f));
        shopDamageText.fontSize = 20;
        shopDamageText.alignment = TextAnchor.UpperLeft;
        shopDamageText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);

        shopFireRateText = CreateHudText("Fire Rate Info", panelRect, new Vector2(16f, -210f));
        shopFireRateText.fontSize = 20;
        shopFireRateText.alignment = TextAnchor.UpperLeft;
        shopFireRateText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);

        rangeUpgradeButton = CreateShopButton("Range Upgrade Button", panelRect, new Vector2(16f, -302f), new Vector2(90f, 28f), out rangeUpgradeButtonText);
        rangeUpgradeButton.onClick.AddListener(HandleRangeUpgradeClicked);

        damageUpgradeButton = CreateShopButton("Damage Upgrade Button", panelRect, new Vector2(114f, -302f), new Vector2(90f, 28f), out damageUpgradeButtonText);
        damageUpgradeButton.onClick.AddListener(HandleDamageUpgradeClicked);

        fireRateUpgradeButton = CreateShopButton("Fire Rate Upgrade Button", panelRect, new Vector2(212f, -302f), new Vector2(92f, 28f), out fireRateUpgradeButtonText);
        fireRateUpgradeButton.onClick.AddListener(HandleFireRateUpgradeClicked);

        closeShopButton = CreateShopButton("Close Shop Button", panelRect, new Vector2(214f, -14f), new Vector2(90f, 26f), out Text closeButtonText);
        closeButtonText.text = "Close";
        closeShopButton.onClick.AddListener(CloseTowerShop);

        shopPanel.SetActive(false);
    }

    // Called by EnsureShopExists to create one clickable shop button and its text label.
    private Button CreateShopButton(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, out Text buttonText)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.15f, 0.4f, 0.2f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        buttonText = CreateHudText($"{objectName} Text", buttonRect, Vector2.zero);
        buttonText.fontSize = 16;
        buttonText.alignment = TextAnchor.MiddleCenter;

        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    // Runs during HUD setup to ensure UI buttons can receive clicks with the Input System.
    private void EnsureUiEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("UI EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    // Runs every frame to open the shop when a tower is clicked or close it when empty space is clicked.
    private void HandleTowerSelectionInput()
    {
        // Ignore clicks after the match ends, when no mouse exists, or when the left button was not just pressed.
        if (IsGameOver || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        // Ignore world selection if the player is currently clicking on UI.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Use the main camera to convert the mouse cursor from screen space into the world.
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        // Read the current mouse position on the screen.
        Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();

        // Convert the cursor position into world coordinates.
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);

        // Ask Physics2D for every collider under the cursor.
        Collider2D[] hits = Physics2D.OverlapPointAll(new Vector2(mouseWorldPosition.x, mouseWorldPosition.y));

        for (int i = 0; i < hits.Length; i++)
        {
            // Check whether this clicked collider belongs to a tower.
            Tower clickedTower = hits[i].GetComponent<Tower>();
            if (clickedTower != null)
            {
                // Open the clicked tower's shop and stop searching further hits.
                OpenTowerShop(clickedTower);
                return;
            }
        }

        // If the player clicked somewhere that is not a tower, close the shop.
        CloseTowerShop();
    }

    // Called by the range-upgrade button to buy the next range tier on the selected tower.
    private void HandleRangeUpgradeClicked()
    {
        if (selectedTower != null)
        {
            selectedTower.TryUpgradeRange();
        }
    }

    // Called by the damage-upgrade button to buy the next damage tier on the selected tower.
    private void HandleDamageUpgradeClicked()
    {
        if (selectedTower != null)
        {
            selectedTower.TryUpgradeDamage();
        }
    }

    // Called by the fire-rate-upgrade button to buy the next fire-rate tier on the selected tower.
    private void HandleFireRateUpgradeClicked()
    {
        if (selectedTower != null)
        {
            selectedTower.TryUpgradeFireRate();
        }
    }

    // Called whenever health changes so the HUD reflects the current player health.
    private void UpdateLivesHud()
    {
        if (livesText != null)
        {
            livesText.text = $"Health: {Lives}";
        }
    }

    // Called whenever coins change so the HUD shows the latest value.
    private void UpdateCurrencyHud()
    {
        if (currencyText != null)
        {
            currencyText.text = $"Coins: {Currency}";
        }
    }

    // Called whenever the active wave changes so the HUD shows current wave progress.
    private void UpdateWaveHud(int waveNumber, int totalWaves)
    {
        if (waveText != null)
        {
            waveText.text = $"Wave: {waveNumber}/{Mathf.Max(totalWaves, 1)}";
        }
    }
}
