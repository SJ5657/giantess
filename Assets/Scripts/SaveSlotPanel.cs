using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Procedurally-built 4-slot save/load picker, matching StatUpgradeMenu/EscMenu's dark-panel
// visual style. Used two different ways from two different callers:
//  - SavePoint (in the giant's yard) opens it in Save mode to write the current run into a
//    chosen slot.
//  - GameFlowManager's start-screen "불러오기" button opens it in Load mode to start a new
//    run restored from a chosen slot.
public class SaveSlotPanel : MonoBehaviour
{
    public enum Mode { Save, Load }

    // Matches the Instance/IsOpen pattern the other popups (InventoryMenu, StatUpgradeMenu,
    // HostageShopMenu, EscMenu) already use, so PauseMenu's Esc handling can check this panel
    // the exact same way it checks those -- see the fix in PauseMenu.Update() for why this was
    // needed: Esc pressed while this panel was open used to fall straight through to opening
    // the pause menu on top of it instead of closing it.
    public static SaveSlotPanel Instance { get; private set; }
    public bool IsOpen => isOpen;

    public Canvas targetCanvas;

    private class SlotRow
    {
        public TMP_Text infoText;
        public TMP_Text buttonLabel;
        public Button button;
    }

    private GameObject panel;
    private TMP_Text titleText;
    private SlotRow[] rows;
    private Mode mode;
    private bool isOpen;

    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.7f);
    static readonly Color BoxColor = new Color(0.13f, 0.13f, 0.16f, 0.97f);
    static readonly Color ButtonColor = new Color(0.25f, 0.45f, 0.75f, 1f);
    static readonly Color ButtonDisabledColor = new Color(0.35f, 0.35f, 0.35f, 1f);
    static readonly Color CloseButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f);

    void Awake()
    {
        Instance = this;
        if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();
        BuildUI();
        panel.SetActive(false);
    }

    public void Open(Mode newMode)
    {
        mode = newMode;
        isOpen = true;
        panel.SetActive(true);
        titleText.text = mode == Mode.Save ? "게임 저장" : "게임 불러오기";
        RefreshRows();

        // Only Save mode needs to pause/free the cursor itself -- Load mode is opened from
        // the start screen, which already owns that state (Time.timeScale is already 0 and
        // the cursor is already free).
        if (mode == Mode.Save)
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void Close()
    {
        isOpen = false;
        panel.SetActive(false);

        if (mode == Mode.Save)
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void RefreshRows()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            int displaySlot = i + 1;
            GiantSaveData data = SaveSystem.ReadSlot(i);
            bool hasSave = data != null && data.used;

            rows[i].infoText.text = hasSave
                ? "슬롯 " + displaySlot + "\n" + data.saveDateTime
                : "슬롯 " + displaySlot + "\n(비어있음)";

            if (mode == Mode.Save)
            {
                rows[i].buttonLabel.text = hasSave ? "덮어쓰기" : "저장";
                rows[i].button.interactable = true;
            }
            else
            {
                rows[i].buttonLabel.text = "불러오기";
                rows[i].button.interactable = hasSave;
            }
        }
    }

    void OnSlotClicked(int slotIndex)
    {
        if (mode == Mode.Save)
        {
            SaveSystem.SaveToSlot(slotIndex);
            RefreshRows();
        }
        else
        {
            Close();
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.StartGameFromSave(slotIndex);
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // Procedural UI construction (colors/sizes match StatUpgradeMenu's existing style).
    // ---------------------------------------------------------------------------------------

    void BuildUI()
    {
        Transform canvasTransform = targetCanvas != null ? targetCanvas.transform : null;

        RectTransform backdrop = CreateUIObject("SaveSlotPanel", canvasTransform);
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
        box.sizeDelta = new Vector2(520f, 460f);
        box.localScale = Vector3.one * UiScale.Menu;
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = BoxColor;

        RectTransform title = CreateTopAnchored(box, "Title", -20f, 44f);
        titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "게임 저장";
        titleText.fontSize = 28f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        rows = new SlotRow[SaveSystem.SlotCount];
        float rowStartY = -84f;
        float rowSpacing = 78f;

        for (int i = 0; i < SaveSystem.SlotCount; i++)
        {
            float y = rowStartY - rowSpacing * i;
            RectTransform row = CreateTopAnchored(box, "SlotRow_" + i, y, 66f);

            RectTransform label = CreateUIObject("Info", row);
            label.anchorMin = new Vector2(0f, 0.5f);
            label.anchorMax = new Vector2(0f, 0.5f);
            label.pivot = new Vector2(0f, 0.5f);
            label.anchoredPosition = new Vector2(24f, 0f);
            label.sizeDelta = new Vector2(300f, 60f);
            TMP_Text infoText = label.gameObject.AddComponent<TextMeshProUGUI>();
            infoText.fontSize = 18f;
            infoText.color = Color.white;
            infoText.alignment = TextAlignmentOptions.MidlineLeft;
            infoText.text = "슬롯 " + (i + 1);

            RectTransform buttonRect = CreateUIObject("SlotButton", row);
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-24f, 0f);
            buttonRect.sizeDelta = new Vector2(150f, 46f);
            Image buttonImg = buttonRect.gameObject.AddComponent<Image>();
            buttonImg.color = ButtonColor;
            Button button = buttonRect.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.disabledColor = ButtonDisabledColor;
            button.colors = colors;

            RectTransform buttonLabelRect = CreateUIObject("Label", buttonRect);
            buttonLabelRect.anchorMin = Vector2.zero;
            buttonLabelRect.anchorMax = Vector2.one;
            buttonLabelRect.sizeDelta = Vector2.zero;
            buttonLabelRect.anchoredPosition = Vector2.zero;
            TMP_Text buttonLabelText = buttonLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
            buttonLabelText.fontSize = 18f;
            buttonLabelText.color = Color.white;
            buttonLabelText.alignment = TextAlignmentOptions.Center;

            int capturedIndex = i; // local copy for the closure, one per loop iteration
            button.onClick.AddListener(() => OnSlotClicked(capturedIndex));

            rows[i] = new SlotRow { infoText = infoText, buttonLabel = buttonLabelText, button = button };
        }

        RectTransform closeRect = CreateUIObject("CloseButton", box);
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(0f, 20f);
        closeRect.sizeDelta = new Vector2(200f, 46f);
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
        closeLabelText.fontSize = 18f;
        closeLabelText.color = Color.white;
        closeLabelText.alignment = TextAlignmentOptions.Center;
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
