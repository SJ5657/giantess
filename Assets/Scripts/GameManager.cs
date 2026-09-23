using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Button retryButton;

    [Header("Level-triggered Reinforcements")]
    [Tooltip("Spawns police cars/tanks/helicopters as the giant levels up, see the wave settings below.")]
    public VehicleSpawner vehicleSpawner;

    [Header("Police Waves")]
    [Tooltip("Level at which police car waves start being summoned.")]
    public int policeWaveStartLevel = 3;
    [Tooltip("Extra levels needed, on top of the last wave, to trigger the next police wave.")]
    public int policeWaveLevelInterval = 1;
    [Tooltip("How many police cars the very first wave summons.")]
    public int policeWaveBaseCount = 2;
    [Tooltip("Extra police cars added to each wave for every level above policeWaveStartLevel -- this is what makes waves bigger the more the giant levels up (e.g. 0.4 means +1 car every 2.5 levels past the start).")]
    public float policeWaveCountPerLevel = 0.4f;
    [Tooltip("Police cars currently alive are capped at this many — a wave that's due won't spawn cars beyond it (though the milestone still advances; the shortfall is not queued for later).")]
    public int policeMaxAlive = 14;

    [Header("Tank (armored vehicle) Waves")]
    [Tooltip("Level at which tank waves start being summoned.")]
    public int tankWaveStartLevel = 7;
    [Tooltip("Extra levels needed, on top of the last wave, to trigger the next tank wave.")]
    public int tankWaveLevelInterval = 2;
    [Tooltip("How many tanks the very first wave summons.")]
    public int tankWaveBaseCount = 1;
    [Tooltip("Extra tanks added to each wave for every level above tankWaveStartLevel.")]
    public float tankWaveCountPerLevel = 0.3f;
    [Tooltip("Tanks currently alive are capped at this many.")]
    public int tankMaxAlive = 8;

    [Header("Helicopter Waves")]
    [Tooltip("Level at which helicopter waves start being summoned.")]
    public int heliWaveStartLevel = 10;
    [Tooltip("Extra levels needed, on top of the last wave, to trigger the next helicopter wave.")]
    public int heliWaveLevelInterval = 2;
    [Tooltip("How many helicopters the very first wave summons.")]
    public int heliWaveBaseCount = 1;
    [Tooltip("Extra helicopters added to each wave for every level above heliWaveStartLevel.")]
    public float heliWaveCountPerLevel = 0.25f;
    [Tooltip("Helicopters currently alive are capped at this many.")]
    public int heliMaxAlive = 6;

    private bool isGameOver;
    public bool IsGameOver => isGameOver;

    // Level threshold the NEXT wave of each type fires at. Starts at that type's
    // WaveStartLevel and advances by WaveLevelInterval every time a wave actually triggers,
    // so waves keep repeating for as long as the giant keeps leveling up (not a one-shot).
    private int nextPoliceWaveLevel;
    private int nextTankWaveLevel;
    private int nextHeliWaveLevel;

    // How many of each are alive right now, so a due wave only spawns up to the
    // remaining headroom under that type's MaxAlive cap. Destroyed vehicles report
    // back via NotifyXDestroyed() below so this stays accurate as the fight goes on.
    private int policeAliveCount;
    private int tankAliveCount;
    private int heliAliveCount;

    void Awake()
    {
        Instance = this;
        nextPoliceWaveLevel = policeWaveStartLevel;
        nextTankWaveLevel = tankWaveStartLevel;
        nextHeliWaveLevel = heliWaveStartLevel;

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RestartGame);
        }
    }

    // Called by GiantProgression right after the giant's level increases (once per level,
    // even if a single big XP gain crosses several levels at once -- GiantProgression calls
    // this once per level in that case). Each wave type repeats independently every
    // WaveLevelInterval levels once its WaveStartLevel is reached, and the number of enemies
    // per wave grows the higher the giant's level is (see policeWaveCountPerLevel etc.) --
    // this is what makes the fight scale up the more the player levels up.
    public void OnLevelUp(int level)
    {
        if (vehicleSpawner == null) return;

        while (level >= nextPoliceWaveLevel)
        {
            int count = ComputeWaveCount(policeWaveBaseCount, policeWaveCountPerLevel, level, policeWaveStartLevel);
            SpawnWave(count, policeMaxAlive, ref policeAliveCount, vehicleSpawner.SpawnPolice);
            nextPoliceWaveLevel += policeWaveLevelInterval;
        }

        while (level >= nextTankWaveLevel)
        {
            int count = ComputeWaveCount(tankWaveBaseCount, tankWaveCountPerLevel, level, tankWaveStartLevel);
            SpawnWave(count, tankMaxAlive, ref tankAliveCount, vehicleSpawner.SpawnTank);
            nextTankWaveLevel += tankWaveLevelInterval;
        }

        while (level >= nextHeliWaveLevel)
        {
            int count = ComputeWaveCount(heliWaveBaseCount, heliWaveCountPerLevel, level, heliWaveStartLevel);
            SpawnWave(count, heliMaxAlive, ref heliAliveCount, vehicleSpawner.SpawnHelicopter);
            nextHeliWaveLevel += heliWaveLevelInterval;
        }
    }

    // How many enemies a wave summons at the given level: a fixed base amount plus more for
    // every level past that type's own start level, so a wave triggered at a high level
    // brings more enemies than the same type's very first wave did.
    static int ComputeWaveCount(int baseCount, float countPerLevel, int level, int startLevel)
    {
        int extra = Mathf.FloorToInt(Mathf.Max(0, level - startLevel) * countPerLevel);
        return baseCount + extra;
    }

    // Spawns up to `count` more of a vehicle type, but never past `maxAlive` currently
    // alive. If the cap is already full (or nearly), fewer than `count` spawn this wave
    // — the milestone still advances (see OnLevelUp), it just doesn't queue
    // the shortfall for later.
    static void SpawnWave(int count, int maxAlive, ref int aliveCount, System.Action spawnOne)
    {
        int room = maxAlive - aliveCount;
        int toSpawn = Mathf.Clamp(count, 0, room);
        for (int i = 0; i < toSpawn; i++)
        {
            spawnOne();
            aliveCount++;
        }
    }

    // Called by PoliceCarAI when a car explodes, so the next due wave knows there's
    // room again under the concurrent-alive cap.
    public void NotifyPoliceDestroyed()
    {
        policeAliveCount = Mathf.Max(0, policeAliveCount - 1);
    }

    public void NotifyTankDestroyed()
    {
        tankAliveCount = Mathf.Max(0, tankAliveCount - 1);
    }

    public void NotifyHelicopterDestroyed()
    {
        heliAliveCount = Mathf.Max(0, heliAliveCount - 1);
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj != null)
        {
            GiantController controller = giantObj.GetComponent<GiantController>();
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Fast-forwards the wave level-thresholds to stay consistent with a restored level (e.g.
    // after loading a save slot) without spawning any waves immediately -- mirrors the old
    // score-based LoadScore's behavior, just keyed on level instead of score.
    public void SyncWaveThresholdsToLevel(int level)
    {
        nextPoliceWaveLevel = policeWaveStartLevel;
        while (level >= nextPoliceWaveLevel) nextPoliceWaveLevel += policeWaveLevelInterval;

        nextTankWaveLevel = tankWaveStartLevel;
        while (level >= nextTankWaveLevel) nextTankWaveLevel += tankWaveLevelInterval;

        nextHeliWaveLevel = heliWaveStartLevel;
        while (level >= nextHeliWaveLevel) nextHeliWaveLevel += heliWaveLevelInterval;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
