using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Popup opened by walking the giant up to the birdcage with an EMPTY hand and left-clicking
// (see GiantController.TickAttackInput -> TryOpenHostageShop). Shows every hostage currently
// locked in the birdcage (see GiantController.CageHeldTarget / CagedHostages) as a clickable
// list, each row showing a small live 3D preview of that hostage's actual model (see
// SetupPreview) rather than just a name -- click a row to select it (click again to
// deselect; multiple rows can be selected at once), then press "판매" to sell every selected
// hostage for coins (added directly to GiantProgression.coins, the same currency the Stat
// Upgrade menu, Z, spends). Mirrors StatUpgradeMenu's open/close/pause pattern and
// procedural-UI style so it reads as part of the same UI family.
public class HostageShopMenu : MonoBehaviour
{
    public static HostageShopMenu Instance { get; private set; }

    [Header("References (auto-found if left empty)")]
    public GiantController giant;
    public GiantProgression progression;
    public Canvas targetCanvas;

    [Header("Selling")]
    public int coinsPerHostageSold = 5;

    private class HostageRowUI
    {
        public GameObject root;
        public Image bg;
        public Transform hostage;
        public GameObject previewClone;
        public Camera previewCamera;
        public RenderTexture previewTexture;
        public RawImage previewImage;
    }

    private System.Collections.Generic.List<HostageRowUI> rows = new System.Collections.Generic.List<HostageRowUI>();
    private System.Collections.Generic.HashSet<Transform> selected = new System.Collections.Generic.HashSet<Transform>();

    // Off in empty space, far from the actual playable world, where each row's preview clone
    // (see SetupPreview) is quietly instantiated and filmed by its own tiny camera -- keeps
    // the "portrait" render fully isolated from gameplay (no collisions, nothing else visible
    // behind it) without needing a dedicated render layer.
    private Transform previewStageRoot;

    private GameObject panel;
    private TMP_Text hostageCountText;
    private RectTransform contentRect;
    private TMP_Text noHostagesText;
    private TMP_Text sellButtonLabel;
    private Button sellButton;
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
        if (progression == null) progression = GiantProgression.Instance != null ? GiantProgression.Instance : FindObjectOfType<GiantProgression>();
        if (targetCanvas == null) targetCanvas = FindObjectOfType<Canvas>();

        BuildUI();
        panel.SetActive(false);
    }

    // Escape closing this popup is now handled centrally by PauseMenu (see IsOpen below),
    // so the same Escape press isn't processed by both this script and PauseMenu.

    // Called by GiantController when the giant left-clicks near the birdcage with an empty
    // hand. Returns false (does nothing) if some other menu already has the game paused, so
    // the caller can fall back to a normal grab attempt instead.
    public bool TryOpen()
    {
        if (isOpen) return true;
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasGameStarted) return false;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return false;
        if (Time.timeScale == 0f) return false;

        Open();
        return true;
    }

    public void Open()
    {
        isOpen = true;
        panel.SetActive(true);
        selected.Clear();
        Refresh();

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        isOpen = false;
        panel.SetActive(false);
        selected.Clear();

        // Preview clones/cameras (see SetupPreview) are pure visual scaffolding for whichever
        // hostages were listed -- no reason to keep rendering them once the panel is hidden,
        // so they're torn down here rather than left running idle in the background until the
        // next Open() happens to rebuild the list.
        ClearRows();

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // This shop is uniquely opened AND closed by the same left mouse button
        // GiantController reads for grab/cage/attack input (unlike the X/Z/P/Esc-toggled
        // menus, which use keyboard keys GiantController never looks at) -- without this, the
        // exact click that closes it here is also seen by GiantController's own Update() as a
        // fresh world click and can immediately reopen the shop. See
        // GiantController.SuppressAttackInputBriefly.
        if (giant != null)
        {
            giant.SuppressAttackInputBriefly();
        }
    }

    // ---------------------------------------------------------------------------------------
    // Selection / selling
    // ---------------------------------------------------------------------------------------

    void OnRowClicked(Transform hostage)
    {
        if (hostage == null) return;

        if (selected.Contains(hostage))
        {
            selected.Remove(hostage);
        }
        else
        {
            selected.Add(hostage);
        }

        RefreshRowVisual(hostage);
        UpdateSellButtonState();
    }

    void RefreshRowVisual(Transform hostage)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].hostage != hostage) continue;
            if (rows[i].bg != null) rows[i].bg.color = selected.Contains(hostage) ? RowSelectedColor : RowUnselectedColor;
            return;
        }
    }

    void UpdateSellButtonState()
    {
        if (sellButton != null) sellButton.interactable = selected.Count > 0;
        if (sellButtonLabel != null) sellButtonLabel.text = selected.Count > 0 ? ("판매 (" + selected.Count + ")") : "판매";
    }

    void OnSellClicked()
    {
        if (giant == null || selected.Count == 0) return;

        int sold = giant.SellHostages(selected);
        if (sold > 0 && progression != null)
        {
            progression.coins += sold * coinsPerHostageSold;
            progression.UpdateUI();
        }

        selected.Clear();
        Refresh();
    }

    // ---------------------------------------------------------------------------------------
    // Refresh / list rebuilding
    // ---------------------------------------------------------------------------------------

    void Refresh()
    {
        int hostageCount = giant != null ? giant.HostageCount : 0;
        if (hostageCountText != null) hostageCountText.text = "보유 인질: " + hostageCount + "명";

        RebuildList();
    }

    void RebuildList()
    {
        ClearRows();

        System.Collections.Generic.IReadOnlyList<Transform> hostages = giant != null ? giant.CagedHostages : null;
        int count = hostages != null ? hostages.Count : 0;

        // Drop any selection that no longer points at an actually-caged hostage (sold from
        // elsewhere, etc.) so a stale selected row can't linger invisibly.
        if (selected.Count > 0)
        {
            selected.RemoveWhere(t => t == null || !ContainsHostage(hostages, t));
        }

        if (noHostagesText != null) noHostagesText.gameObject.SetActive(count == 0);

        if (hostages != null)
        {
            for (int i = 0; i < hostages.Count; i++)
            {
                Transform hostage = hostages[i];
                if (hostage == null) continue;
                rows.Add(BuildRow(hostage, i));
            }
        }

        if (contentRect != null)
        {
            float contentHeight = Mathf.Max(rows.Count * RowStep, 0f);
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
        }

        UpdateSellButtonState();
    }

    // Destroys every currently-built row, including each one's preview clone/camera/render
    // texture (see SetupPreview) -- shared by RebuildList (about to build a fresh set) and
    // Close (nothing left visible to preview until the menu opens again).
    void ClearRows()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            HostageRowUI row = rows[i];
            if (row.root != null) Destroy(row.root);
            if (row.previewClone != null) Destroy(row.previewClone);
            if (row.previewCamera != null) Destroy(row.previewCamera.gameObject);
            if (row.previewTexture != null) row.previewTexture.Release();
        }
        rows.Clear();
    }

    static bool ContainsHostage(System.Collections.Generic.IReadOnlyList<Transform> list, Transform t)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == t) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // Procedural UI construction. Same colors/helpers as StatUpgradeMenu/InventoryMenu so
    // this reads as part of the same UI family rather than a bolted-on new look.
    // ---------------------------------------------------------------------------------------

    static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color BoxColor = new Color(0.13f, 0.13f, 0.16f, 0.97f);
    static readonly Color ListPanelColor = new Color(0.09f, 0.09f, 0.11f, 1f);
    static readonly Color RowUnselectedColor = new Color(0.2f, 0.2f, 0.24f, 1f);
    static readonly Color RowSelectedColor = new Color(0.75f, 0.55f, 0.15f, 1f);
    static readonly Color SellButtonColor = new Color(0.85f, 0.35f, 0.25f, 1f);
    static readonly Color SellButtonDisabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
    static readonly Color CloseButtonColor = new Color(0.3f, 0.3f, 0.34f, 1f);
    static readonly Color PreviewBackgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);

    const float RowHeight = 64f;
    const float RowGap = 8f;
    const float RowStep = RowHeight + RowGap;
    const float ListPanelHeight = 224f;

    // Preview render setup -- see SetupPreview.
    static readonly Vector3 PreviewStageOrigin = new Vector3(0f, -4000f, 0f);
    const float PreviewStageSpacing = 30f;
    const float PreviewDistanceFactor = 2.1f;
    const float PreviewFov = 30f;
    const int PreviewTextureSize = 128;

    void BuildUI()
    {
        Transform canvasTransform = targetCanvas != null ? targetCanvas.transform : null;

        RectTransform backdrop = CreateUIObject("HostageShopPanel", canvasTransform);
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
        box.sizeDelta = new Vector2(560f, 460f);
        Image boxImg = box.gameObject.AddComponent<Image>();
        boxImg.color = BoxColor;

        RectTransform title = CreateTopAnchored(box, "Title", -20f, 50f);
        TMP_Text titleText = title.gameObject.AddComponent<TextMeshProUGUI>();
        titleText.text = "인질 목록";
        titleText.fontSize = 30f;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        RectTransform hostageRow = CreateTopAnchored(box, "HostageRow", -75f, 34f);
        hostageCountText = hostageRow.gameObject.AddComponent<TextMeshProUGUI>();
        hostageCountText.fontSize = 22f;
        hostageCountText.color = new Color(1f, 0.6f, 0.6f, 1f);
        hostageCountText.alignment = TextAlignmentOptions.Center;

        BuildListPanel(box, -120f);

        BuildBottomButtons(box);
    }

    // Scrollable list of caged hostages: a clipped panel (RectMask2D) containing a
    // ScrollRect whose content grows with the number of caged hostages, so any number of
    // hostages can be scrolled through in a fixed-height area instead of overflowing the box.
    void BuildListPanel(Transform box, float topY)
    {
        RectTransform listPanel = CreateTopAnchored(box, "ListPanel", topY, ListPanelHeight);
        Image listPanelImg = listPanel.gameObject.AddComponent<Image>();
        listPanelImg.color = ListPanelColor;
        listPanel.gameObject.AddComponent<RectMask2D>();

        ScrollRect scrollRect = listPanel.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 24f;

        RectTransform content = CreateUIObject("Content", listPanel);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        contentRect = content;

        scrollRect.content = content;
        scrollRect.viewport = null; // falls back to this ScrollRect's own RectTransform (listPanel), already clipped by RectMask2D above

        RectTransform noHostagesRect = CreateUIObject("NoHostagesText", listPanel);
        noHostagesRect.anchorMin = Vector2.zero;
        noHostagesRect.anchorMax = Vector2.one;
        noHostagesRect.sizeDelta = Vector2.zero;
        noHostagesRect.anchoredPosition = Vector2.zero;
        noHostagesText = noHostagesRect.gameObject.AddComponent<TextMeshProUGUI>();
        noHostagesText.text = "새장에 가둔 인질이 없습니다";
        noHostagesText.fontSize = 18f;
        noHostagesText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        noHostagesText.alignment = TextAlignmentOptions.Center;
        noHostagesText.raycastTarget = false;
    }

    // Builds one clickable row for a caged hostage inside the scroll content: a live 3D
    // preview thumbnail of that hostage's actual model (see SetupPreview) on the left, a
    // small "#N" index badge to its right, and the whole row clickable to toggle selection
    // (see OnRowClicked) -- selected rows turn gold, unselected stay the neutral panel color.
    HostageRowUI BuildRow(Transform hostage, int index)
    {
        RectTransform rowRect = CreateTopAnchored(contentRect, "HostageRow" + index, -index * RowStep, RowHeight);

        Image bg = rowRect.gameObject.AddComponent<Image>();
        bg.color = selected.Contains(hostage) ? RowSelectedColor : RowUnselectedColor;

        Button button = rowRect.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        Transform capturedHostage = hostage;
        button.onClick.AddListener(() => OnRowClicked(capturedHostage));

        RectTransform previewRect = CreateUIObject("Preview", rowRect);
        previewRect.anchorMin = new Vector2(0f, 0.5f);
        previewRect.anchorMax = new Vector2(0f, 0.5f);
        previewRect.pivot = new Vector2(0f, 0.5f);
        previewRect.anchoredPosition = new Vector2(4f, 0f);
        previewRect.sizeDelta = new Vector2(RowHeight - 8f, RowHeight - 8f);
        RawImage previewImage = previewRect.gameObject.AddComponent<RawImage>();
        previewImage.raycastTarget = false;
        previewImage.color = Color.white;

        RectTransform labelRect = CreateUIObject("Label", rowRect);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(RowHeight + 4f, 0f);
        labelRect.offsetMax = new Vector2(-10f, 0f);
        TMP_Text labelText = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
        labelText.text = "#" + (index + 1);
        labelText.fontSize = 16f;
        labelText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        labelText.raycastTarget = false;

        HostageRowUI rowUI = new HostageRowUI { root = rowRect.gameObject, bg = bg, hostage = hostage, previewImage = previewImage };
        SetupPreview(rowUI, hostage, index);
        return rowUI;
    }

    // Instantiates a throwaway clone of the hostage's actual model far from the playable
    // world (see PreviewStageOrigin), strips out every gameplay-only component (AI, wander,
    // colliders) so it just stands there, points a small dedicated camera at it from
    // whichever direction the clone is actually facing (so the portrait always shows its
    // front regardless of which way it happened to be wandering when the menu opened), and
    // renders that into a small RenderTexture displayed on the row's RawImage. The
    // Animator's update mode is switched to unscaled time so the preview keeps playing its
    // idle animation even while the menu has the game paused (Time.timeScale = 0).
    void SetupPreview(HostageRowUI row, Transform hostage, int index)
    {
        if (hostage == null) return;

        if (previewStageRoot == null)
        {
            previewStageRoot = new GameObject("HostagePreviewStage").transform;
        }

        Vector3 stagePos = PreviewStageOrigin + new Vector3(index * PreviewStageSpacing, 0f, 0f);

        GameObject clone = Instantiate(hostage.gameObject, previewStageRoot);
        clone.name = "HostagePreviewClone" + index;
        clone.transform.position = stagePos;
        clone.transform.rotation = hostage.rotation;
        clone.transform.localScale = hostage.lossyScale;

        StripComponent<TinyNPC>(clone);
        StripComponent<TinySoldierAI>(clone);
        StripComponent<CagedHostageWander>(clone);

        Collider[] colliders = clone.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++) Destroy(colliders[i]);

        Animator anim = clone.GetComponentInChildren<Animator>();
        if (anim != null) anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(stagePos, Vector3.one);
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        float subjectHeight = Mathf.Max(bounds.size.y, 0.3f);

        GameObject camGo = new GameObject("HostagePreviewCamera" + index);
        camGo.transform.SetParent(previewStageRoot, true);
        Camera cam = camGo.AddComponent<Camera>();

        Vector3 forward = clone.transform.forward;
        Vector3 lookTarget = bounds.center + Vector3.up * (subjectHeight * 0.05f);
        Vector3 camPos = bounds.center - forward * (subjectHeight * PreviewDistanceFactor) + Vector3.up * (subjectHeight * 0.15f);
        cam.transform.position = camPos;
        cam.transform.LookAt(lookTarget);

        cam.fieldOfView = PreviewFov;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = Mathf.Max(subjectHeight * PreviewDistanceFactor + 2f, 5f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = PreviewBackgroundColor;
        cam.cullingMask = ~0;

        RenderTexture rt = new RenderTexture(PreviewTextureSize, PreviewTextureSize, 16);
        cam.targetTexture = rt;

        row.previewClone = clone;
        row.previewCamera = cam;
        row.previewTexture = rt;
        if (row.previewImage != null) row.previewImage.texture = rt;
    }

    static void StripComponent<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        if (c != null) Destroy(c);
    }

    // "판매" (sell the current selection) and "닫기" (close), side by side at the bottom of
    // the box -- replaces the old shop's single "닫기 (Esc)" button.
    void BuildBottomButtons(Transform box)
    {
        RectTransform sellRect = CreateUIObject("SellButton", box);
        sellRect.anchorMin = new Vector2(0.5f, 0f);
        sellRect.anchorMax = new Vector2(0.5f, 0f);
        sellRect.pivot = new Vector2(0.5f, 0f);
        sellRect.anchoredPosition = new Vector2(-120f, 24f);
        sellRect.sizeDelta = new Vector2(220f, 50f);
        Image sellImg = sellRect.gameObject.AddComponent<Image>();
        sellImg.color = SellButtonColor;
        sellButton = sellRect.gameObject.AddComponent<Button>();
        sellButton.targetGraphic = sellImg;
        ColorBlock sellColors = sellButton.colors;
        sellColors.normalColor = Color.white;
        sellColors.disabledColor = SellButtonDisabledColor;
        sellButton.colors = sellColors;
        sellButton.onClick.AddListener(OnSellClicked);
        sellButton.interactable = false;

        RectTransform sellLabelRect = CreateUIObject("Label", sellRect);
        sellLabelRect.anchorMin = Vector2.zero;
        sellLabelRect.anchorMax = Vector2.one;
        sellLabelRect.sizeDelta = Vector2.zero;
        sellLabelRect.anchoredPosition = Vector2.zero;
        sellButtonLabel = sellLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
        sellButtonLabel.text = "판매";
        sellButtonLabel.fontSize = 20f;
        sellButtonLabel.color = Color.white;
        sellButtonLabel.alignment = TextAlignmentOptions.Center;

        RectTransform closeRect = CreateUIObject("CloseButton", box);
        closeRect.anchorMin = new Vector2(0.5f, 0f);
        closeRect.anchorMax = new Vector2(0.5f, 0f);
        closeRect.pivot = new Vector2(0.5f, 0f);
        closeRect.anchoredPosition = new Vector2(120f, 24f);
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
        closeLabelText.text = "닫기 (Esc)";
        closeLabelText.fontSize = 20f;
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
