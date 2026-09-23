using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Owns the game's top-level flow: the start screen shown on launch, and returning to
// it from the pause menu's Exit button. Esc-driven pause/resume itself lives in
// PauseMenu, which asks this manager whether the game has actually started yet.
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Start Screen")]
    public GameObject startScreenPanel;
    public Button startGameButton;
    [Tooltip("Repurposed from the original placeholder \"Options\" button: opens the save-slot picker in Load mode.")]
    public Button startOptionsButton;
    [Tooltip("Intentionally left with no click behavior for now.")]
    public Button startExitButton;

    [Header("Save / Load")]
    [Tooltip("Auto-found if left empty.")]
    public SaveSlotPanel saveSlotPanel;

    public bool HasGameStarted { get; private set; }

    void Awake()
    {
        Instance = this;

        // The game boots straight into the start screen: gameplay is frozen
        // (Time.timeScale = 0, so physics/movement/animation all stay still) and the
        // cursor is freed so the player can actually click the menu buttons, since the
        // in-game camera normally locks/hides it. Nothing here touches the object-spawn
        // popup (EscMenu, now on P) or the pause menu (PauseMenu, on Esc) — both simply
        // won't do anything useful until HasGameStarted flips true below.
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(true);
        }

        if (saveSlotPanel == null)
        {
            saveSlotPanel = FindObjectOfType<SaveSlotPanel>();
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(StartGame);
        }

        if (startOptionsButton != null)
        {
            startOptionsButton.onClick.AddListener(OpenLoadPanel);
        }

        // startExitButton: no listener is wired on purpose — per the current request,
        // clicking it does nothing yet.
    }

    void OpenLoadPanel()
    {
        if (saveSlotPanel != null)
        {
            saveSlotPanel.Open(SaveSlotPanel.Mode.Load);
        }
    }

    public void StartGame()
    {
        HasGameStarted = true;

        if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        TeleportGiantToYardSpawn();
    }

    // Called by SaveSlotPanel when a populated slot is chosen from the start screen's Load
    // flow. Same start-up sequence as StartGame(), except the giant's position/stats/
    // progression are overwritten by the saved data instead of spawning fresh in the yard.
    public void StartGameFromSave(int slot)
    {
        HasGameStarted = true;

        if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SaveSystem.LoadFromSlot(slot);
    }

    // Moves the giant to the yard's SpawnPoint (built by GiantHouseBuilder) for a fresh,
    // non-loaded run.
    void TeleportGiantToYardSpawn()
    {
        GameObject spawnObj = GameObject.Find("GiantHouse/SpawnPoint");
        if (spawnObj == null) return;

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj == null) return;

        CharacterController cc = giantObj.GetComponent<CharacterController>();
        bool wasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        giantObj.transform.position = spawnObj.transform.position;
        giantObj.transform.rotation = spawnObj.transform.rotation;

        if (cc != null) cc.enabled = wasEnabled;
    }

    // Called by PauseMenu's Exit button. Reloading the scene is what actually resets
    // all game state (giant HP, score, spawned enemies/vehicles, tiny people, etc.)
    // rather than trying to manually restore every system by hand, and lands back on
    // the start screen automatically via this same Awake() path on scene load.
    public void ReturnToStartScreen()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
