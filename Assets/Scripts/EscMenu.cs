using UnityEngine;
using UnityEngine.UI;

// Object-spawn cheat popup (respawn tiny people, summon tank/helicopter/police).
// Originally bound to Escape; moved to the P key so Escape is free for the real
// pause menu (PauseMenu.cs).
public class EscMenu : MonoBehaviour
{
    public static EscMenu Instance { get; private set; }

    [Header("References")]
    public GameObject menuPanel;
    public CityGenerator cityGenerator;
    public VehicleSpawner vehicleSpawner;
    public Button respawnTinyButton;
    public Button summonTankButton;
    public Button summonHelicopterButton;
    public Button summonPoliceButton;
    public Button closeButton;

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
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        if (respawnTinyButton != null)
        {
            respawnTinyButton.onClick.AddListener(OnRespawnTinyClicked);
        }

        if (summonTankButton != null)
        {
            summonTankButton.onClick.AddListener(OnSummonTankClicked);
        }

        if (summonHelicopterButton != null)
        {
            summonHelicopterButton.onClick.AddListener(OnSummonHelicopterClicked);
        }

        if (summonPoliceButton != null)
        {
            summonPoliceButton.onClick.AddListener(OnSummonPoliceClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseMenu);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P) && CanTogglePopup())
        {
            if (isOpen)
            {
                CloseMenu();
            }
            else
            {
                OpenMenu();
            }
        }
    }

    // Only usable once the game has actually started (past the start screen) and
    // isn't over/paused via the Esc menu, so P can't pop this up on top of those.
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

    public void OpenMenu()
    {
        isOpen = true;
        if (menuPanel != null)
        {
            menuPanel.SetActive(true);
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseMenu()
    {
        isOpen = false;
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OnRespawnTinyClicked()
    {
        if (cityGenerator != null)
        {
            cityGenerator.RespawnTinyPeople();
        }
    }

    public void OnSummonTankClicked()
    {
        if (vehicleSpawner != null)
        {
            vehicleSpawner.SpawnTank();
        }
    }

    public void OnSummonHelicopterClicked()
    {
        if (vehicleSpawner != null)
        {
            vehicleSpawner.SpawnHelicopter();
        }
    }

    public void OnSummonPoliceClicked()
    {
        if (vehicleSpawner != null)
        {
            vehicleSpawner.SpawnPolice();
        }
    }
}
