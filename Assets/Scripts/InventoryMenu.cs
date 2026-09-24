using UnityEngine;
using UnityEngine.UI;
using TMPro;

// X-toggled popup showing what the giant is carrying: HP Potions (bought from the Hostage
// Shop -- see HostageShopMenu) shown as an icon with a small count badge (only when the
// player actually has some), double-clicked to use, plus the current coin balance for
// reference (coins themselves are spent in the Stat Upgrade menu, Z -- see
// StatUpgradeMenu). Also holds a small hotkey panel: click a hotkey slot (E/R/F) to arm
// it, then single-click an item icon to assign that item to it. Once assigned, pressing
// E/R/F during normal gameplay (menu closed, game not paused) uses that item directly
// without opening the Inventory at all. Mirrors StatUpgradeMenu's open/close/pause pattern
// and procedural-UI style so it reads as part of the same UI family.
public class InventoryMenu : MonoBehaviour
{
    public static InventoryMenu Instance { get; private set; }

    [Header("References (auto-found if left empty)")]
    public GiantController giant;
    public PlayerInventory inventory;
    public GiantHealth giantHealth;
    public GiantProgression progression;
    public Canvas targetCanvas;

    // One entry per item type the Inventory can display. Built as a list (even though
    // there's only one item today, HP Potions) so more item types can be added later
    // without restructuring -- see PlayerInventory's own "general item-count store" note.
    // Also doubles as what a hotkey slot can point at (see hotkeySlotAssignment).
    private class ItemSlot
    {
        public System.Func<int> getCount;
        public System.Action use;
        public GameObject root;
        public TMP_Text countText;
        public string symbol;
        public Color iconColor;
    }

    private class HotkeySlotUI
    {
        public GameObject root;
        public Image bg;
        public TMP_Text symbolText;
        public Outline outline;
        public GameObject clearButton;
    }

    private System.Collections.Generic.List<ItemSlot> itemSlots = new System.Collections.Generic.List<ItemSlot>();

    // E/R/F quick-use hotkeys. Each slot either points at one of the ItemSlots above or is
    // null (unassigned). armedHotkeySlot is the slot currently waiting for an item click
    // ("assign mode"); -1 means nothing is armed.
    private static readonly KeyCode[] HotkeyCodes = { KeyCode.E, KeyCode.R, KeyCode.F };
    private static readonly string[] HotkeyLabels = { "E", "R", "F" };
    private ItemSlot[] hotkeySlotAssignment = new ItemSlot[HotkeyCodes.Length];
    private HotkeySlotUI[] hotkeySlotUIs;
    private int armedHotkeySlot = -1;

    private GameObject panel;
    private TMP_Text coinText;
    private TMP_Text hostageText;
    private Transform itemsContainer;
    private TMP_Text noItemsText;
    private bool isOpen;

    // Used by PauseMenu so Escape can close this popup instead of stacking the pause
    // menu on top of it.
    public bool IsOpen => isOpen;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (giant == null) giant = FindObjectOfType<GiantController>();
        if (inventory == null) inventory = PlayerInventory.Instance != null ? PlayerInventory.Instance : FindObjectOfType<PlayerInventory>();
        if (giantHealth == null) giantHealth = FindObjectOfType<GiantHealth>();
        if (progression == null) progression = GiantProgression.Instance != null ? GiantProgression.Instance : FindObjectOfType<GiantProgression>();
        if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();

        BuildUI();
        panel.SetActive(false);
        RefreshHotkeyPanel();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.X) && CanTogglePopup())
        {
            if (isOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        TickHotkeys();
    }

    // Same gating rule as EscMenu/StatUpgradeMenu: only usable once the game has actually
    // started and isn't already over, and can't be opened on top of some other pause-driven
    // popup (Z's Stat Upgrade menu, the Hostage Shop, etc).
    bool CanTogglePopup()
    {
        if (!isOpen)
        {
            if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasGameStarted) return false;
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return false;
            if (Time.timeScale == 0f) return false;
        }
        return true;
    }

    // Fires the item assigned to a hotkey slot when its key is pressed during normal
    // gameplay. Gated the same way CanTogglePopup gates opening the Inventory itself, so
    // hotkeys naturally do nothing while any pause-driven popup (including this one) is
    // open, before the game has started, or after it's over.
    void TickHotkeys()
    {
        if (Time.timeScale == 0f) return;
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasGameStarted) return;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

        for (int i = 0; i < HotkeyCodes.Length; i++)
        {
            if (!Input.GetKeyDown(HotkeyCodes[i])) continue;

            ItemSlot slot = hotkeySlotAssignment[i];
            if (slot == null || slot.use == null) continue;
            if (slot.getCount == null || slot.getCount() <= 0) continue;

            slot.use();
        }
    }

    public void Open()
    {
        isOpen = true;
        panel.SetActive(true);
        Refresh();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        isOpen = false;
        panel.SetActive(false);

        // Cancel any in-progress hotkey assignment so a stray leftover "armed" state
        // doesn't carry over silently to next time the menu opens.
        armedHotkeySlot = -1;
        RefreshHotkeyPanel();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void UseHPPotion()
    {
        if (inventory == null) return;
        inventory.UseHPPotion(giantHealth);
        Refresh();
    }

    void Refresh()
    {
        if (coinText != null) coinText.text = "코인: " + (progression != null ? progression.coins : 0);
        if (hostageText != null) hostageText.text = "인질: " + (giant != null ? giant.HostageCount : 0) + "명";

        bool anyItems = false;
        foreach (var slot in itemSlots)
        {
            int count = slot.getCount != null ? slot.getCount() : 0;
            bool has = count > 0;
            if (has) anyItems = true;

            if (slot.root != null) slot.root.SetActive(has);
            if (has && slot.countText != null) slot.countText.text = count.ToString();
        }

        if (noItemsText != null) noItemsText.gameObject.SetActive(!anyItems);

        RefreshHotkeyPanel();
    }

    // ---------------------------------------------------------------------------------------
    // Hotkey slot interactions -- wired from BuildHotkeySlot's Button and BuildItemIcon's
    // single-click handler.
    // ---------------------------------------------------------------------------------------

    // Clicking a hotkey slot arms it (highlighted, waiting for an item click) or, if it's
    // already armed, disarms it again (cancel).
    void OnHotkeySlotClicked(int slotIndex)
    {
        armedHotkeySlot = (armedHotkeySlot == slotIndex) ? -1 : slotIndex;
        RefreshHotkeyPanel();
    }

    // Clicking the small "x" on a hotkey slot clears whatever item was assigned to it.
    void OnHotkeyClearClicked(int slotIndex)
    {
        hotkeySlotAssignment[slotIndex] = null;
        if (armedHotkeySlot == slotIndex) armedHotkeySlot = -1;
        RefreshHotkeyPanel();
    }

    // Single-clicking an item icon while a hotkey slot is armed assigns that item to it and
    // disarms. Does nothing if no slot is currently armed (so a plain single click on an
    // item, outside of assign mode, has no effect -- double-click still uses it).
    void AssignArmedHotkeySlot(ItemSlot slot)
    {
        if (armedHotkeySlot < 0) return;
        hotkeySlotAssignment[armedHotkeySlot] = slot;
        armedHotkeySlot = -1;
        RefreshHotkeyPanel();
    }

    void RefreshHotkeyPanel()
    {
        if (hotkeySlotUIs == null) return;

        for (int i = 0; i < hotkeySlotUIs.Length; i++)
        {
            HotkeySlotUI ui = hotkeySlotUIs[i];
            if (ui == null) continue;

            ItemSlot assigned = hotkeySlotAssignment[i];
            bool has = assigned != null;

            if (ui.bg != null) ui.bg.color = has ? assigned.iconColor : EmptySlotColor;
            if (ui.symbolText != null) ui.symbolText.text = has ? assigned.symbol : "";
            if (ui.clearButton != null) ui.clearButton.SetActive(has);
            if (ui.outline != null) ui.outline.enabled = (armedHotkeySlot == i);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Procedural UI construction. Same colors/helpers as StatUpgradeMenu so this reads as
    // part of the same UI family rather than a bolted-on new look.
    // ---------------------------------------------------------------------------------------

    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color BoxColor = new Color(0.13f, 0.13f, 0.16f, 0.97f);
    static readonly Color CloseButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f);
    static readonly Color CountBadgeColor = new Color(0f, 0f, 0f, 0.75f);
    static readonly Color HPPotionIconColor = new Color(0.78f, 0.22f, 0.3f, 1f);
    static readonly Color EmptySlotColor = new Color(0.2f, 0.2f, 0.24f, 1f);
    static readonly Color HotkeyArmedOutlineColor = new Color(1f, 0.85f, 0.2f, 1f);
    static readonly Color ClearButtonColor = new Color(0.5f, 0.1f, 0.1f, 1f);

    const float ItemIconSize = 72f;
    const float ItemIconSpacing = 16f;
    const float ItemsAreaLeftMargin = 24f;
    const float HotkeySlotSize = 56f;
    const float HotkeySlotSpacing = 20f;

    void BuildUI()
    {
        Transform canvasTransform = targetCanvas != null ? targetCanvas.transform : null;

        RectTransform backdrop = CreateUIObject("InventoryPanel", canvasTransform);
        panel = backdrop.gameObject;
        backdrop.anchorMin = Vector2.zero;
        backdrop.anchorMax = Vector2.one;
        backdrop.sizeDelta = Vector2.zero;
        backdrop.anchoredPosition = Vector2.zero;
        Image backdropImg = backdrop.gameObject.AddComponent<Image>();
        backdropImg.color = BackdropColor;

        RectTransform box = CreateUIObject("Box", backdrop);
        box.anchorMin = new Vector2(0.5f, 0.5f);
        box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = Vector2.zero;
        box.sizeDelta = new Vector2(480f, 420f);
        box.localScale = Vector3.one * UiScale.Menu;
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = BoxColor;

        RectTransform title = CreateTopAnchored(box, "Title", -20f, 50f);
        TMP_Text titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "인벤토리";
        titleText.fontSize = 30f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        RectTransform coinRow = CreateTopAnchored(box, "CoinRow", -75f, 34f);

        RectTransform coinTextRect = CreateUIObject("CoinText", coinRow);
        coinTextRect.anchorMin = new Vector2(0f, 0.5f);
        coinTextRect.anchorMax = new Vector2(0f, 0.5f);
        coinTextRect.pivot = new Vector2(0f, 0.5f);
        coinTextRect.anchoredPosition = new Vector2(24f, 0f);
        coinTextRect.sizeDelta = new Vector2(140f, 34f);
        coinText = coinTextRect.gameObject.AddComponent<TextMeshProUGUI>();
        coinText.fontSize = 20f;
        coinText.color = new Color(1f, 0.85f, 0.3f, 1f);
        coinText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform hostageTextRect = CreateUIObject("HostageText", coinRow);
        hostageTextRect.anchorMin = new Vector2(0f, 0.5f);
        hostageTextRect.anchorMax = new Vector2(0f, 0.5f);
        hostageTextRect.pivot = new Vector2(0f, 0.5f);
        hostageTextRect.anchoredPosition = new Vector2(24f + 140f + 12f, 0f);
        hostageTextRect.sizeDelta = new Vector2(180f, 34f);
        hostageText = hostageTextRect.gameObject.AddComponent<TextMeshProUGUI>();
        hostageText.fontSize = 20f;
        hostageText.color = new Color(1f, 0.6f, 0.6f, 1f);
        hostageText.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform itemsArea = CreateTopAnchored(box, "ItemsArea", -140f, ItemIconSize);
        itemsContainer = itemsArea;

        RectTransform noItemsRect = CreateUIObject("NoItemsText", itemsArea);
        noItemsRect.anchorMin = Vector2.zero;
        noItemsRect.anchorMax = Vector2.one;
        noItemsRect.sizeDelta = Vector2.zero;
        noItemsRect.anchoredPosition = Vector2.zero;
        noItemsText = noItemsRect.gameObject.AddComponent<TextMeshProUGUI>();
        noItemsText.text = "보유한 아이템이 없습니다";
        noItemsText.fontSize = 18f;
        noItemsText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        noItemsText.alignment = TextAlignmentOptions.MidlineLeft;

        ItemSlot potionSlot = BuildItemIcon(
            itemsArea,
            ItemsAreaLeftMargin,
            "+",
            HPPotionIconColor,
            () => inventory != null ? inventory.hpPotionCount : 0,
            UseHPPotion);
        itemSlots.Add(potionSlot);

        BuildHotkeyPanel(box, -232f);

        RectTransform closeRect = CreateUIObject("CloseButton", box);
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 24f);
        closeRect.sizeDelta = new Vector2(220f, 50f);
        Image closeImg = closeRect.gameObject.AddComponent<Image>();
        closeImg.color = CloseButtonColor;
        Button closeButton = closeRect.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImg;
        closeButton.onClick.AddListener(Close);

        RectTransform closeLabelRect = CreateUIObject("Label", closeRect);
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.sizeDelta = Vector2.zero;
        closeLabelRect.anchoredPosition = Vector2.zero;
        TMP_Text closeLabelText = closeLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        closeLabelText.text = "닫기";
        closeLabelText.fontSize = 20f;
        closeLabelText.color = Color.white;
        closeLabelText.alignment = TextAlignmentOptions.Center;
    }

    // Builds the small hotkey-assignment panel: a caption plus a centered row of E/R/F
    // slots. Each slot shows whatever item icon (symbol + color) is currently assigned to
    // it, or sits empty. Click a slot to arm it (yellow outline), then click one of the
    // item icons above to assign; click the slot's "x" to clear it.
    void BuildHotkeyPanel(Transform parent, float topY)
    {
        RectTransform hotkeyPanel = CreateTopAnchored(parent, "HotkeyPanel", topY, 106f);

        RectTransform hotkeyLabelRect = CreateUIObject("HotkeyLabel", hotkeyPanel);
        hotkeyLabelRect.anchorMin = new Vector2(0f, 1f);
        hotkeyLabelRect.anchorMax = new Vector2(1f, 1f);
        hotkeyLabelRect.pivot = new Vector2(0.5f, 1f);
        hotkeyLabelRect.anchoredPosition = Vector2.zero;
        hotkeyLabelRect.sizeDelta = new Vector2(0f, 22f);
        TMP_Text hotkeyLabelText = hotkeyLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        hotkeyLabelText.text = "단축키 (슬롯 클릭 후 아이템 클릭으로 지정)";
        hotkeyLabelText.fontSize = 15f;
        hotkeyLabelText.color = new Color(0.75f, 0.75f, 0.78f, 1f);
        hotkeyLabelText.alignment = TextAlignmentOptions.Center;
        hotkeyLabelText.raycastTarget = false;

        hotkeySlotUIs = new HotkeySlotUI[HotkeyCodes.Length];

        float totalWidth = HotkeySlotSize * HotkeyCodes.Length + HotkeySlotSpacing * (HotkeyCodes.Length - 1);
        float startX = -totalWidth / 2f + HotkeySlotSize / 2f;
        float rowY = -30f;

        for (int i = 0; i < HotkeyCodes.Length; i++)
        {
            float x = startX + i * (HotkeySlotSize + HotkeySlotSpacing);
            hotkeySlotUIs[i] = BuildHotkeySlot(hotkeyPanel, x, rowY, i, HotkeyLabels[i]);
        }
    }

    HotkeySlotUI BuildHotkeySlot(Transform parent, float x, float y, int slotIndex, string keyLabel)
    {
        RectTransform root = CreateUIObject("HotkeySlot" + keyLabel, parent);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(x, y);
        root.sizeDelta = new Vector2(HotkeySlotSize, HotkeySlotSize);

        Image bg = root.gameObject.AddComponent<Image>();
        bg.color = EmptySlotColor;

        Outline outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = HotkeyArmedOutlineColor;
        outline.effectDistance = new Vector2(3f, -3f);
        outline.enabled = false;

        int capturedIndex = slotIndex;
        Button slotButton = root.gameObject.AddComponent<Button>();
        slotButton.targetGraphic = bg;
        slotButton.onClick.AddListener(() => OnHotkeySlotClicked(capturedIndex));

        RectTransform keyLabelRect = CreateUIObject("KeyLabel", root);
        keyLabelRect.anchorMin = new Vector2(0f, 1f);
        keyLabelRect.anchorMax = new Vector2(0f, 1f);
        keyLabelRect.pivot = new Vector2(0f, 1f);
        keyLabelRect.anchoredPosition = new Vector2(3f, -2f);
        keyLabelRect.sizeDelta = new Vector2(20f, 16f);
        TMP_Text keyLabelText = keyLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        keyLabelText.text = keyLabel;
        keyLabelText.fontSize = 12f;
        keyLabelText.fontStyle = FontStyles.Bold;
        keyLabelText.color = new Color(0.85f, 0.85f, 0.85f, 0.9f);
        keyLabelText.alignment = TextAlignmentOptions.TopLeft;
        keyLabelText.raycastTarget = false;

        RectTransform symbolRect = CreateUIObject("Symbol", root);
        symbolRect.anchorMin = Vector2.zero;
        symbolRect.anchorMax = Vector2.one;
        symbolRect.sizeDelta = Vector2.zero;
        symbolRect.anchoredPosition = Vector2.zero;
        TMP_Text symbolText = symbolRect.gameObject.AddComponent<TextMeshProUGUI>();
        symbolText.fontSize = 26f;
        symbolText.fontStyle = FontStyles.Bold;
        symbolText.color = Color.white;
        symbolText.alignment = TextAlignmentOptions.Center;
        symbolText.raycastTarget = false;

        RectTransform clearRect = CreateUIObject("ClearButton", root);
        clearRect.anchorMin = new Vector2(1f, 1f);
        clearRect.anchorMax = new Vector2(1f, 1f);
        clearRect.pivot = new Vector2(1f, 1f);
        clearRect.anchoredPosition = new Vector2(2f, 2f);
        clearRect.sizeDelta = new Vector2(18f, 18f);
        Image clearImg = clearRect.gameObject.AddComponent<Image>();
        clearImg.color = ClearButtonColor;
        Button clearButton = clearRect.gameObject.AddComponent<Button>();
        clearButton.targetGraphic = clearImg;
        clearButton.onClick.AddListener(() => OnHotkeyClearClicked(capturedIndex));

        RectTransform clearLabelRect = CreateUIObject("Label", clearRect);
        clearLabelRect.anchorMin = Vector2.zero;
        clearLabelRect.anchorMax = Vector2.one;
        clearLabelRect.sizeDelta = Vector2.zero;
        clearLabelRect.anchoredPosition = Vector2.zero;
        TMP_Text clearLabelText = clearLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        clearLabelText.text = "x";
        clearLabelText.fontSize = 12f;
        clearLabelText.color = Color.white;
        clearLabelText.alignment = TextAlignmentOptions.Center;
        clearLabelText.raycastTarget = false;

        return new HotkeySlotUI
        {
            root = root.gameObject,
            bg = bg,
            symbolText = symbolText,
            outline = outline,
            clearButton = clearRect.gameObject
        };
    }

    // Builds one square item icon at horizontal offset x within an item row: a colored
    // background square, a bold symbol in the middle, and a small dark count badge in the
    // bottom-right corner. Double-clicking the icon (see DoubleClickTrigger) invokes `use`
    // instead of a separate "Use" button; single-clicking it assigns it to whichever hotkey
    // slot is currently armed (see AssignArmedHotkeySlot), if any. The whole icon is
    // shown/hidden per-frame-refresh by Refresh() based on whether `getCount` is currently
    // > 0.
    ItemSlot BuildItemIcon(Transform parent, float x, string symbol, Color iconColor, System.Func<int> getCount, System.Action use)
    {
        RectTransform root = CreateUIObject("ItemSlot", parent);
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(x, 0f);
        root.sizeDelta = new Vector2(ItemIconSize, ItemIconSize);

        Image iconImg = root.gameObject.AddComponent<Image>();
        iconImg.color = iconColor;

        DoubleClickTrigger trigger = root.gameObject.AddComponent<DoubleClickTrigger>();
        trigger.onDoubleClick = use;

        RectTransform symbolRect = CreateUIObject("Symbol", root);
        symbolRect.anchorMin = Vector2.zero;
        symbolRect.anchorMax = Vector2.one;
        symbolRect.sizeDelta = Vector2.zero;
        symbolRect.anchoredPosition = Vector2.zero;
        TMP_Text symbolText = symbolRect.gameObject.AddComponent<TextMeshProUGUI>();
        symbolText.text = symbol;
        symbolText.fontSize = 34f;
        symbolText.fontStyle = FontStyles.Bold;
        symbolText.color = Color.white;
        symbolText.alignment = TextAlignmentOptions.Center;
        symbolText.raycastTarget = false;

        RectTransform badgeRect = CreateUIObject("CountBadge", root);
        badgeRect.anchorMin = new Vector2(1f, 0f);
        badgeRect.anchorMax = new Vector2(1f, 0f);
        badgeRect.pivot = new Vector2(1f, 0f);
        badgeRect.anchoredPosition = new Vector2(-2f, 2f);
        badgeRect.sizeDelta = new Vector2(28f, 20f);
        Image badgeImg = badgeRect.gameObject.AddComponent<Image>();
        badgeImg.color = CountBadgeColor;
        badgeImg.raycastTarget = false;

        RectTransform countRect = CreateUIObject("Count", badgeRect);
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.sizeDelta = Vector2.zero;
        countRect.anchoredPosition = Vector2.zero;
        TMP_Text countText = countRect.gameObject.AddComponent<TextMeshProUGUI>();
        countText.fontSize = 14f;
        countText.fontStyle = FontStyles.Bold;
        countText.color = Color.white;
        countText.alignment = TextAlignmentOptions.Center;
        countText.raycastTarget = false;

        ItemSlot slot = new ItemSlot
        {
            getCount = getCount,
            use = use,
            root = root.gameObject,
            countText = countText,
            symbol = symbol,
            iconColor = iconColor
        };

        trigger.onSingleClick = () => AssignArmedHotkeySlot(slot);

        return slot;
    }

    RectTransform CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    RectTransform CreateTopAnchored(Transform parent, string name, float y, float height)
    {
        RectTransform rt = CreateUIObject(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(0f, height);
        return rt;
    }
}
