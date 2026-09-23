using UnityEngine;
using UnityEngine.UI;

// Esc-triggered pause menu. Separate from EscMenu (the object-spawn cheat popup,
// rebound to the P key so the two don't collide), and only ever offers Options
// (currently a no-op) and Exit (returns to the start screen via GameFlowManager).
public class PauseMenu : MonoBehaviour
{
    public GameObject menuPanel;
    [Tooltip("Intentionally left with no click behavior for now.")]
    public Button optionsButton;
    public Button exitButton;

    private bool isPaused;

    void Start()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitClicked);
        }

        // optionsButton: no listener wired on purpose — clicking it does nothing yet.
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // Esc's first job is closing whatever popup is currently open -- it should never
        // stack the pause menu on top of the Inventory (X), Stat Upgrade (Z), Hostage
        // Shop (left-click near the birdcage), or EscMenu cheat popup (P). Each of those
        // already refuses to open while another popup has Time.timeScale paused, so at
        // most one of them can be open at a time.
        if (InventoryMenu.Instance != null && InventoryMenu.Instance.IsOpen)
        {
            InventoryMenu.Instance.Close();
            return;
        }
        if (StatUpgradeMenu.Instance != null && StatUpgradeMenu.Instance.IsOpen)
        {
            StatUpgradeMenu.Instance.Close();
            return;
        }
        if (HostageShopMenu.Instance != null && HostageShopMenu.Instance.IsOpen)
        {
            HostageShopMenu.Instance.Close();
            return;
        }
        if (EscMenu.Instance != null && EscMenu.Instance.IsOpen)
        {
            EscMenu.Instance.CloseMenu();
            return;
        }
        // SaveSlotPanel (opened from the SavePoint in the yard, in Save mode) was missing from
        // this list -- Esc pressed while it was open fell straight through to the pause-toggle
        // logic below and opened the pause menu stacked on top of it instead of closing it.
        if (SaveSlotPanel.Instance != null && SaveSlotPanel.Instance.IsOpen)
        {
            SaveSlotPanel.Instance.Close();
            return;
        }

        if (!CanTogglePause()) return;

        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    // Pausing only makes sense once the game is actually in progress: past the start
    // screen and not already showing the game-over panel (both of which already own
    // Time.timeScale/cursor state themselves).
    bool CanTogglePause()
    {
        if (GameFlowManager.Instance != null && !GameFlowManager.Instance.HasGameStarted) return false;
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return false;
        return true;
    }

    public void Pause()
    {
        isPaused = true;
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        isPaused = false;
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnExitClicked()
    {
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ReturnToStartScreen();
        }
    }
}
