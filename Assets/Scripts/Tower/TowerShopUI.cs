using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// TowerShopUI is a small UI-only script that lets the player buy new towers during the match.
// It shows one button per available tower type, displays the build cost, disables buttons when
// the player cannot afford them, and tells TowerManager which tower type should enter placement mode.
public class TowerShopUI : MonoBehaviour
{
    // Singleton reference used if other UI systems want to refresh or query the build shop.
    public static TowerShopUI main { get; private set; }

    // availableTowerPrefabs defines which tower types appear in the build shop.
    [Header("Available Towers")]
    [SerializeField] private Tower[] availableTowerPrefabs;

    // These runtime-only references build and update the build-shop UI.
    private Canvas buildShopCanvas;
    private Font uiFont;
    private GameObject buildShopPanel;
    private Text titleText;
    private Text placementInfoText;
    private Text emptyStateText;

    // These flags prevent duplicate event subscriptions while waiting for managers to exist.
    private bool isSubscribedToGameManager;
    private bool isSubscribedToTowerManager;

    // towerButtons stores the generated UI entry for each buildable tower type.
    private readonly List<TowerButtonBinding> towerButtons = new List<TowerButtonBinding>();

    // Each binding keeps together the button, button label, and cost label for one tower type.
    private sealed class TowerButtonBinding
    {
        public Tower towerPrefab;
        public GameObject rootObject;
        public Button button;
        public Text buttonText;
        public Text costText;
    }

    // Runs after a scene loads to guarantee there is always one active build shop UI.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureTowerShopUiExists()
    {
        if (FindFirstObjectByType<TowerShopUI>() == null)
        {
            GameObject towerShopUiObject = new GameObject("TowerShopUI");
            towerShopUiObject.AddComponent<TowerShopUI>();
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

    // Runs once after Awake to auto-fill tower references when possible and build the UI.
    private void Start()
    {
        PopulateDefaultTowerPrefabsIfNeeded();
        EnsureUiExists();
        TrySubscribeToManagers();
        RefreshBuildShop();
    }

    // Runs every frame until the managers exist so the UI can hook into their events.
    private void Update()
    {
        TrySubscribeToManagers();
    }

    // Runs when the build shop is destroyed so subscriptions and singleton state are cleaned up.
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

        if (isSubscribedToTowerManager && TowerManager.main != null)
        {
            TowerManager.main.PlacementModeChanged -= HandlePlacementModeChanged;
            TowerManager.main.TowerPlaced -= HandleTowerPlaced;
        }
    }

    // Called when the UI should rebuild its labels and button state.
    public void RefreshBuildShop()
    {
        EnsureUiExists();

        int currentCurrency = GameManager.main != null ? GameManager.main.Currency : 0;
        bool canUseMatchEconomy = GameManager.main != null && !GameManager.main.IsGameOver;

        for (int i = 0; i < towerButtons.Count; i++)
        {
            TowerButtonBinding binding = towerButtons[i];

            if (binding.towerPrefab == null)
            {
                binding.button.interactable = false;
                binding.buttonText.text = "Missing Tower";
                binding.costText.text = "Cost: n/a";
                continue;
            }

            int placementCost = binding.towerPrefab.PlacementCost;
            binding.buttonText.text = $"Buy {GetTowerDisplayName(binding.towerPrefab)}";
            binding.costText.text = $"Cost: {placementCost} coins";
            binding.button.interactable = canUseMatchEconomy && currentCurrency >= placementCost;
        }

        bool hasConfiguredTowers = towerButtons.Count > 0;
        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(!hasConfiguredTowers);
            emptyStateText.text = "No tower prefabs configured for the build shop.";
        }

        if (placementInfoText != null)
        {
            if (TowerManager.main != null && TowerManager.main.IsPlacingTower && TowerManager.main.PendingPlacementTowerPrefab != null)
            {
                placementInfoText.text =
                    $"Placing: {GetTowerDisplayName(TowerManager.main.PendingPlacementTowerPrefab)}\n" +
                    "Left click map to place\n" +
                    "Right click to cancel";
            }
            else
            {
                placementInfoText.text =
                    "Build Shop\n" +
                    "Click a tower button, then click the map to place it.";
            }
        }
    }

    // Called by the build buttons when the player selects a tower type to place.
    private void HandleBuyTowerClicked(Tower towerPrefab)
    {
        if (towerPrefab == null)
        {
            return;
        }

        TowerManager.main?.BeginTowerPlacement(towerPrefab);
        RefreshBuildShop();
    }

    // Runs during startup to create the UI if the scene does not already provide one.
    private void EnsureUiExists()
    {
        if (buildShopPanel != null)
        {
            return;
        }

        if (buildShopCanvas == null)
        {
            GameObject canvasObject = new GameObject("Tower Build Shop Canvas");
            buildShopCanvas = canvasObject.AddComponent<Canvas>();
            buildShopCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            buildShopCanvas.sortingOrder = 9;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        EnsureUiEventSystem();
        EnsureBuildShopPanel();
        RebuildTowerButtons();
    }

    // Creates the root panel for the build shop HUD.
    private void EnsureBuildShopPanel()
    {
        if (buildShopPanel != null)
        {
            return;
        }

        GameObject panelObject = new GameObject("Tower Build Shop Panel");
        panelObject.transform.SetParent(buildShopCanvas.transform, false);
        buildShopPanel = panelObject;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.5f);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(0f, 0f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.anchoredPosition = new Vector2(16f, 16f);
        panelRect.sizeDelta = new Vector2(260f, 220f);

        titleText = CreateUiText("Build Shop Title", panelRect, new Vector2(16f, -14f), 24, TextAnchor.MiddleLeft, new Vector2(220f, 28f));
        titleText.text = "Tower Build Shop";

        placementInfoText = CreateUiText("Build Shop Info", panelRect, new Vector2(16f, -44f), 18, TextAnchor.UpperLeft, new Vector2(220f, 52f));
        emptyStateText = CreateUiText("Build Shop Empty State", panelRect, new Vector2(16f, -104f), 18, TextAnchor.UpperLeft, new Vector2(220f, 36f));
    }

    // Rebuilds the list of buy buttons from the configured tower prefabs.
    private void RebuildTowerButtons()
    {
        for (int i = 0; i < towerButtons.Count; i++)
        {
            if (towerButtons[i].rootObject != null)
            {
                Destroy(towerButtons[i].rootObject);
            }
        }

        towerButtons.Clear();

        if (availableTowerPrefabs == null)
        {
            availableTowerPrefabs = Array.Empty<Tower>();
        }

        RectTransform panelRect = buildShopPanel.GetComponent<RectTransform>();
        float startY = -108f;
        float entryHeight = 50f;

        for (int i = 0; i < availableTowerPrefabs.Length; i++)
        {
            Tower towerPrefab = availableTowerPrefabs[i];
            if (towerPrefab == null)
            {
                continue;
            }

            TowerButtonBinding binding = CreateTowerButtonBinding(panelRect, towerPrefab, startY - (i * entryHeight));
            towerButtons.Add(binding);
        }

        float panelHeight = Mathf.Max(220f, 132f + (towerButtons.Count * entryHeight));
        panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, panelHeight);
    }

    // Creates one button row for a tower type in the build shop.
    private TowerButtonBinding CreateTowerButtonBinding(RectTransform panelRect, Tower towerPrefab, float yPosition)
    {
        TowerButtonBinding binding = new TowerButtonBinding();
        binding.towerPrefab = towerPrefab;

        GameObject rowObject = new GameObject($"{GetTowerDisplayName(towerPrefab)} Build Row");
        rowObject.transform.SetParent(panelRect, false);
        binding.rootObject = rowObject;

        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(16f, yPosition);
        rowRect.sizeDelta = new Vector2(220f, 44f);

        binding.button = CreateButton($"{GetTowerDisplayName(towerPrefab)} Buy Button", rowRect, Vector2.zero, new Vector2(220f, 26f), out binding.buttonText);

        Tower capturedTowerPrefab = towerPrefab;
        binding.button.onClick.AddListener(() => HandleBuyTowerClicked(capturedTowerPrefab));

        binding.costText = CreateUiText($"{GetTowerDisplayName(towerPrefab)} Cost Text", rowRect, new Vector2(0f, -30f), 16, TextAnchor.UpperLeft, new Vector2(220f, 18f));
        binding.costText.color = new Color(0.85f, 0.85f, 0.85f, 1f);

        return binding;
    }

    // Creates one reusable UI Text element with the runtime font.
    private Text CreateUiText(string objectName, Transform parent, Vector2 anchoredPosition, int fontSize, TextAnchor alignment, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = uiFont;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        return text;
    }

    // Creates one buy button with centered text.
    private Button CreateButton(string objectName, Transform parent, Vector2 anchoredPosition, Vector2 size, out Text buttonText)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.2f, 0.35f, 0.55f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        buttonText = CreateUiText($"{objectName} Text", buttonRect, Vector2.zero, 16, TextAnchor.MiddleCenter, Vector2.zero);

        RectTransform textRect = buttonText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;

        return button;
    }

    // Ensures there is an EventSystem in the scene so runtime UI buttons can be clicked.
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

    // Hooks the UI into GameManager and TowerManager once they are available.
    private void TrySubscribeToManagers()
    {
        if (!isSubscribedToGameManager && GameManager.main != null)
        {
            GameManager.main.CurrencyChanged += HandleCurrencyChanged;
            GameManager.main.GameStateChanged += HandleGameStateChanged;
            isSubscribedToGameManager = true;
        }

        if (!isSubscribedToTowerManager && TowerManager.main != null)
        {
            TowerManager.main.PlacementModeChanged += HandlePlacementModeChanged;
            TowerManager.main.TowerPlaced += HandleTowerPlaced;
            isSubscribedToTowerManager = true;
        }
    }

    // Refreshes the shop whenever the player's coins change.
    private void HandleCurrencyChanged(int currency)
    {
        RefreshBuildShop();
    }

    // Refreshes the shop when the match ends so buttons disable correctly.
    private void HandleGameStateChanged(bool isGameOver, bool isGameWon)
    {
        RefreshBuildShop();
    }

    // Refreshes the placement instructions whenever placement mode changes.
    private void HandlePlacementModeChanged(Tower towerPrefab)
    {
        RefreshBuildShop();
    }

    // Refreshes the shop after a tower is placed so the next placement starts from a clean state.
    private void HandleTowerPlaced(Tower tower)
    {
        RefreshBuildShop();
    }

    // Generates a player-friendly tower name for buttons and placement text.
    private string GetTowerDisplayName(Tower towerPrefab)
    {
        if (towerPrefab == null)
        {
            return "Missing Tower";
        }

        return towerPrefab.name.Replace("(Clone)", string.Empty).Trim();
    }

    // In the Unity Editor, auto-fill the build shop with tower prefabs from Assets/Prefab/Towers when none are assigned yet.
    private void PopulateDefaultTowerPrefabsIfNeeded()
    {
#if UNITY_EDITOR
        bool hasAssignedPrefab = false;

        if (availableTowerPrefabs != null)
        {
            for (int i = 0; i < availableTowerPrefabs.Length; i++)
            {
                if (availableTowerPrefabs[i] != null)
                {
                    hasAssignedPrefab = true;
                    break;
                }
            }
        }

        if (hasAssignedPrefab)
        {
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/Towers" });
        List<Tower> discoveredPrefabs = new List<Tower>();

        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabObject == null)
            {
                continue;
            }

            Tower towerPrefab = prefabObject.GetComponent<Tower>();
            if (towerPrefab != null)
            {
                discoveredPrefabs.Add(towerPrefab);
            }
        }

        availableTowerPrefabs = discoveredPrefabs.ToArray();
#endif
    }
}
