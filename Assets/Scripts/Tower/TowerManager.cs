using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// TowerManager owns systems that apply to towers as a group rather than one individual tower.
// It keeps track of placed towers, handles tower selection input, shows the tower shop UI,
// and exposes placement and selling methods for runtime tower economy.
public class TowerManager : MonoBehaviour
{
    // Singleton reference used by towers and other systems to find the active TowerManager quickly.
    public static TowerManager main { get; private set; }

    // sellRefundPercent decides how much of a tower's invested currency comes back when it is sold.
    [Header("Economy Settings")]
    [SerializeField] [Range(0f, 1f)] private float sellRefundPercent = 0.5f;

    // These runtime-only references build and update the tower shop UI.
    private Canvas towerUiCanvas;
    private Font uiFont;
    private GameObject shopPanel;
    private Text shopTitleText;
    private Text shopRangeText;
    private Text shopDamageText;
    private Text shopFireRateText;
    private Text shopEconomyText;
    private Button rangeUpgradeButton;
    private Button damageUpgradeButton;
    private Button fireRateUpgradeButton;
    private Button sellTowerButton;
    private Button closeShopButton;
    private Text rangeUpgradeButtonText;
    private Text damageUpgradeButtonText;
    private Text fireRateUpgradeButtonText;
    private Text sellTowerButtonText;

    // selectedTower stores which tower is currently highlighted in the shop.
    private Tower selectedTower;

    // trackedTowers stores every tower the manager currently knows about in the scene.
    private readonly HashSet<Tower> trackedTowers = new HashSet<Tower>();

    // This tracks whether the manager has already hooked into GameManager events.
    private bool isSubscribedToGameManager;

    // These events allow future UI or gameplay systems to react to tower-focused actions.
    public event Action<Tower> TowerSelected;
    public event Action<Tower> TowerPlaced;
    public event Action<Tower, int> TowerSold;

    // These read-only properties expose the manager state safely to the rest of the game.
    public Tower SelectedTower => selectedTower;
    public int TrackedTowerCount => trackedTowers.Count;
    public float SellRefundPercent => sellRefundPercent;

    // Runs after a scene loads to guarantee there is always one active TowerManager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureTowerManagerExists()
    {
        if (FindFirstObjectByType<TowerManager>() == null)
        {
            GameObject towerManagerObject = new GameObject("TowerManager");
            towerManagerObject.AddComponent<TowerManager>();
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

    // Runs once after Awake to discover towers, create the UI, and subscribe to match-wide events.
    private void Start()
    {
        RegisterExistingTowers();
        EnsureUiExists();
        TrySubscribeToGameManager();
    }

    // Runs every frame so the player can click towers in the world to open or close the shop.
    private void Update()
    {
        TrySubscribeToGameManager();
        HandleTowerSelectionInput();
    }

    // Runs when the manager is destroyed so subscriptions and singleton state are cleaned up.
    private void OnDestroy()
    {
        if (main == this)
        {
            main = null;
        }

        if (isSubscribedToGameManager && GameManager.main != null)
        {
            GameManager.main.CurrencyChanged -= HandleCurrencyChanged;
            GameManager.main.GameStateChanged -= HandleGameStateChanged;
        }
    }

    // Called by towers or startup logic so the manager can track one tower in the scene.
    public void RegisterTower(Tower tower)
    {
        if (tower == null)
        {
            return;
        }

        trackedTowers.Add(tower);
    }

    // Called when a tower is removed so the manager's tower list stays accurate.
    public void UnregisterTower(Tower tower)
    {
        if (tower == null)
        {
            return;
        }

        trackedTowers.Remove(tower);

        if (selectedTower == tower)
        {
            CloseTowerShop();
        }
    }

    // Called by runtime placement systems to spawn a tower using its default orientation.
    public Tower PlaceTower(Tower towerPrefab, Vector3 position)
    {
        return PlaceTower(towerPrefab, position, Quaternion.identity, true);
    }

    // Called by runtime placement systems to spawn a tower and optionally spend its build cost.
    public Tower PlaceTower(Tower towerPrefab, Vector3 position, Quaternion rotation, bool spendCurrency)
    {
        if (towerPrefab == null)
        {
            return null;
        }

        if (GameManager.main != null && GameManager.main.IsGameOver)
        {
            return null;
        }

        int placementCost = towerPrefab.PlacementCost;

        if (spendCurrency && placementCost > 0)
        {
            if (GameManager.main == null || !GameManager.main.TrySpendCurrency(placementCost))
            {
                return null;
            }
        }

        Tower placedTower = Instantiate(towerPrefab, position, rotation);
        RegisterTower(placedTower);
        TowerPlaced?.Invoke(placedTower);
        return placedTower;
    }

    // Called when the player clicks a tower and wants to inspect or upgrade it.
    public void OpenTowerShop(Tower tower)
    {
        if (tower == null)
        {
            return;
        }

        RegisterTower(tower);
        EnsureUiExists();

        selectedTower = tower;
        shopPanel.SetActive(true);
        shopPanel.transform.SetAsLastSibling();
        RefreshTowerShop();
        TowerSelected?.Invoke(tower);
    }

    // Called when the player clicks away, sells the tower, or closes the panel.
    public void CloseTowerShop()
    {
        selectedTower = null;

        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }
    }

    // Called whenever the selected tower changes or its stats/cost state should be redrawn.
    public void RefreshTowerShop()
    {
        if (shopPanel == null)
        {
            return;
        }

        if (selectedTower == null)
        {
            CloseTowerShop();
            return;
        }

        int currentCurrency = GameManager.main != null ? GameManager.main.Currency : 0;
        bool canUseMatchEconomy = GameManager.main != null && !GameManager.main.IsGameOver;

        shopTitleText.text = $"{selectedTower.name} Shop";
        shopRangeText.text = $"Range Tier: {selectedTower.RangeUpgradeTier}/{selectedTower.MaxRangeUpgradeTier}\nCurrent Range: {selectedTower.Range:F1}";
        shopDamageText.text = $"Damage Tier: {selectedTower.DamageUpgradeTier}/{selectedTower.MaxDamageUpgradeTier}\nCurrent Damage: {selectedTower.CurrentDamage}";
        shopFireRateText.text = $"Fire Rate Tier: {selectedTower.FireRateUpgradeTier}/{selectedTower.MaxFireRateUpgradeTier}\nCurrent Delay: {selectedTower.CurrentFireRate:F2}s";
        shopEconomyText.text = $"Build Cost: {selectedTower.PlacementCost} coins\nSell Value: {selectedTower.GetSellValue(sellRefundPercent)} coins";

        if (selectedTower.CanUpgradeRange)
        {
            shopRangeText.text += $"\nNext Upgrade Cost: {selectedTower.NextRangeUpgradeCost} coins";
            rangeUpgradeButton.interactable = canUseMatchEconomy && currentCurrency >= selectedTower.NextRangeUpgradeCost;
            rangeUpgradeButtonText.text = "Upgrade Range";
        }
        else
        {
            shopRangeText.text += "\nMax Tier Reached";
            rangeUpgradeButton.interactable = false;
            rangeUpgradeButtonText.text = "Range Maxed";
        }

        if (selectedTower.CanUpgradeDamage)
        {
            shopDamageText.text += $"\nNext Upgrade Cost: {selectedTower.NextDamageUpgradeCost} coins";
            damageUpgradeButton.interactable = canUseMatchEconomy && currentCurrency >= selectedTower.NextDamageUpgradeCost;
            damageUpgradeButtonText.text = "Upgrade Damage";
        }
        else
        {
            shopDamageText.text += "\nMax Tier Reached";
            damageUpgradeButton.interactable = false;
            damageUpgradeButtonText.text = "Damage Maxed";
        }

        if (selectedTower.CanUpgradeFireRate)
        {
            shopFireRateText.text += $"\nNext Upgrade Cost: {selectedTower.NextFireRateUpgradeCost} coins";
            fireRateUpgradeButton.interactable = canUseMatchEconomy && currentCurrency >= selectedTower.NextFireRateUpgradeCost;
            fireRateUpgradeButtonText.text = "Upgrade Fire Rate";
        }
        else
        {
            shopFireRateText.text += "\nMax Tier Reached";
            fireRateUpgradeButton.interactable = false;
            fireRateUpgradeButtonText.text = "Fire Rate Maxed";
        }

        sellTowerButton.interactable = canUseMatchEconomy;
        sellTowerButtonText.text = $"Sell Tower ({selectedTower.GetSellValue(sellRefundPercent)})";
    }

    // Called by gameplay or UI when a tower should be sold back for coins.
    public bool SellTower(Tower tower)
    {
        if (tower == null)
        {
            return false;
        }

        if (GameManager.main != null && GameManager.main.IsGameOver)
        {
            return false;
        }

        int sellValue = tower.GetSellValue(sellRefundPercent);

        CloseTowerShop();
        UnregisterTower(tower);

        if (sellValue > 0)
        {
            GameManager.main?.AddCurrency(sellValue);
        }

        TowerSold?.Invoke(tower, sellValue);
        Destroy(tower.gameObject);
        return true;
    }

    // Called by Start so the manager knows about towers that were already placed in the scene.
    private void RegisterExistingTowers()
    {
        Tower[] towers = FindObjectsByType<Tower>(FindObjectsSortMode.None);

        for (int i = 0; i < towers.Length; i++)
        {
            RegisterTower(towers[i]);
        }
    }

    // Runs during startup to build the runtime UI if the scene does not already provide one.
    private void EnsureUiExists()
    {
        if (shopPanel != null)
        {
            return;
        }

        if (towerUiCanvas == null)
        {
            GameObject canvasObject = new GameObject("Tower UI Canvas");
            towerUiCanvas = canvasObject.AddComponent<Canvas>();
            towerUiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            towerUiCanvas.sortingOrder = 10;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        EnsureUiEventSystem();
        EnsureShopExists();
    }

    // Called only while building runtime UI to create one text label with the shared shop font.
    private Text CreateUiText(string objectName, Transform parent, Vector2 anchoredPosition)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
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

    // Runs during setup to create the tower shop panel and its buttons the first time they are needed.
    private void EnsureShopExists()
    {
        if (shopPanel != null)
        {
            return;
        }

        GameObject panelObject = new GameObject("Tower Shop Panel");
        panelObject.transform.SetParent(towerUiCanvas.transform, false);
        shopPanel = panelObject;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.5f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-16f, -16f);
        panelRect.sizeDelta = new Vector2(320f, 430f);

        shopTitleText = CreateUiText("Shop Title", panelRect, new Vector2(16f, -14f));
        shopTitleText.fontSize = 26;

        shopRangeText = CreateUiText("Range Info", panelRect, new Vector2(16f, -54f));
        shopRangeText.fontSize = 20;
        shopRangeText.alignment = TextAnchor.UpperLeft;
        shopRangeText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);

        shopDamageText = CreateUiText("Damage Info", panelRect, new Vector2(16f, -132f));
        shopDamageText.fontSize = 20;
        shopDamageText.alignment = TextAnchor.UpperLeft;
        shopDamageText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);

        shopFireRateText = CreateUiText("Fire Rate Info", panelRect, new Vector2(16f, -210f));
        shopFireRateText.fontSize = 20;
        shopFireRateText.alignment = TextAnchor.UpperLeft;
        shopFireRateText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 72f);

        shopEconomyText = CreateUiText("Tower Economy Info", panelRect, new Vector2(16f, -288f));
        shopEconomyText.fontSize = 20;
        shopEconomyText.alignment = TextAnchor.UpperLeft;
        shopEconomyText.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 48f);

        rangeUpgradeButton = CreateShopButton("Range Upgrade Button", panelRect, new Vector2(16f, -346f), new Vector2(90f, 28f), out rangeUpgradeButtonText);
        rangeUpgradeButton.onClick.AddListener(HandleRangeUpgradeClicked);

        damageUpgradeButton = CreateShopButton("Damage Upgrade Button", panelRect, new Vector2(114f, -346f), new Vector2(90f, 28f), out damageUpgradeButtonText);
        damageUpgradeButton.onClick.AddListener(HandleDamageUpgradeClicked);

        fireRateUpgradeButton = CreateShopButton("Fire Rate Upgrade Button", panelRect, new Vector2(212f, -346f), new Vector2(92f, 28f), out fireRateUpgradeButtonText);
        fireRateUpgradeButton.onClick.AddListener(HandleFireRateUpgradeClicked);

        sellTowerButton = CreateShopButton("Sell Tower Button", panelRect, new Vector2(16f, -382f), new Vector2(288f, 30f), out sellTowerButtonText);
        sellTowerButton.onClick.AddListener(HandleSellTowerClicked);

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

        buttonText = CreateUiText($"{objectName} Text", buttonRect, Vector2.zero);
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

    // Runs during UI setup to ensure the new Input System can route clicks into the shop buttons.
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
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (GameManager.main != null && GameManager.main.IsGameOver)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenPosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(new Vector2(mouseWorldPosition.x, mouseWorldPosition.y));

        for (int i = 0; i < hits.Length; i++)
        {
            Tower clickedTower = hits[i].GetComponent<Tower>();

            if (clickedTower == null)
            {
                clickedTower = hits[i].GetComponentInParent<Tower>();
            }

            if (clickedTower != null)
            {
                OpenTowerShop(clickedTower);
                return;
            }
        }

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

    // Called by the sell button when the player wants to remove the selected tower and get coins back.
    private void HandleSellTowerClicked()
    {
        if (selectedTower != null)
        {
            SellTower(selectedTower);
        }
    }

    // Called whenever the player's coins change so the shop button states stay accurate.
    private void HandleCurrencyChanged(int currency)
    {
        RefreshTowerShop();
    }

    // Called when the match ends so the shop cannot remain open over a finished game state.
    private void HandleGameStateChanged(bool isGameOver, bool isGameWon)
    {
        if (isGameOver)
        {
            CloseTowerShop();
        }
    }

    // Runs until GameManager exists so the tower systems can react to match-wide state and coin changes.
    private void TrySubscribeToGameManager()
    {
        if (isSubscribedToGameManager || GameManager.main == null)
        {
            return;
        }

        GameManager.main.CurrencyChanged += HandleCurrencyChanged;
        GameManager.main.GameStateChanged += HandleGameStateChanged;
        isSubscribedToGameManager = true;
    }
}
