using UnityEngine;
using System.IO;

// Everything captured for one save slot. Originally this deliberately did NOT include the
// city layout, spawned enemies, tiny people or pickups -- those regenerate fresh every time
// the scene loads (see CityGenerator's own comments) -- but that meant a Load could show a
// different map than the one the player actually saved in, and any caged hostages vanished
// (the fresh regeneration doesn't know they existed). citySeed/hasCitySeed and
// cagedHostageCount fix that: a Load now regenerates the city deterministically from the
// saved seed (CityGenerator.RegenerateFromSeed) and re-cages the right number of hostages
// (GiantController.RestoreCagedHostages), instead of leaving the scene's fresh random layout
// in place.
[System.Serializable]
public class GiantSaveData
{
    public bool used;
    public string saveDateTime;

    public float posX, posY, posZ;
    public float rotY;

    public float maxHP, currentHP, damageReductionPercent;
    public float moveSpeed, attackDamage, maxStamina, currentStamina;

    public int level;
    public float currentXP;
    public int coins;
    public int skillPoints;

    public int hpLevel, speedLevel, attackLevel, defenseLevel, staminaLevel;

    // The Random seed CityGenerator used to build the map/citizens at the moment this slot
    // was saved (see CityGenerator.LastUsedSeed), and how many hostages were caged at that
    // moment. hasCitySeed is false for saves made before this field existed, so an old save
    // just keeps the previous behavior (whatever random city the scene happens to have)
    // instead of regenerating from a seed of 0.
    public bool hasCitySeed;
    public int citySeed;
    public int cagedHostageCount;
}

// Simple JSON-file-based save/load for up to 4 slots, written under
// Application.persistentDataPath so saves survive between play sessions independent of the
// Unity project itself. Used by SavePoint (in the giant's yard, for saving) and by
// GameFlowManager's "불러오기" button on the start screen (for loading).
public static class SaveSystem
{
    public const int SlotCount = 4;

    static string SlotPath(int slot)
    {
        return Path.Combine(Application.persistentDataPath, "save_slot_" + slot + ".json");
    }

    public static bool HasSave(int slot)
    {
        return File.Exists(SlotPath(slot));
    }

    public static GiantSaveData ReadSlot(int slot)
    {
        string path = SlotPath(slot);
        if (!File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonUtility.FromJson<GiantSaveData>(json);
        }
        catch
        {
            return null;
        }
    }

    // Gathers the current live game state and writes it to the given slot, overwriting
    // whatever was there before.
    public static void SaveToSlot(int slot)
    {
        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj == null) return;

        GiantController giant = giantObj.GetComponent<GiantController>();
        GiantHealth health = giantObj.GetComponent<GiantHealth>();
        GiantProgression progression = GiantProgression.Instance;
        StatUpgradeMenu statMenu = Object.FindObjectOfType<StatUpgradeMenu>();

        GiantSaveData data = new GiantSaveData();
        data.used = true;
        data.saveDateTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        Vector3 pos = giantObj.transform.position;
        data.posX = pos.x;
        data.posY = pos.y;
        data.posZ = pos.z;
        data.rotY = giantObj.transform.eulerAngles.y;

        if (health != null)
        {
            data.maxHP = health.maxHP;
            data.currentHP = health.currentHP;
            data.damageReductionPercent = health.damageReductionPercent;
        }

        if (giant != null)
        {
            data.moveSpeed = giant.moveSpeed;
            data.attackDamage = giant.attackDamage;
            data.maxStamina = giant.maxStamina;
            data.currentStamina = giant.currentStamina;

            data.cagedHostageCount = giant.HostageCount;
        }

        if (progression != null)
        {
            data.level = progression.level;
            data.currentXP = progression.currentXP;
            data.coins = progression.coins;
            data.skillPoints = progression.skillPoints;
        }

        if (statMenu != null)
        {
            int[] levels = statMenu.GetStatLevels();
            if (levels != null && levels.Length >= 5)
            {
                data.hpLevel = levels[0];
                data.speedLevel = levels[1];
                data.attackLevel = levels[2];
                data.defenseLevel = levels[3];
                data.staminaLevel = levels[4];
            }
        }

        data.hasCitySeed = true;
        data.citySeed = CityGenerator.LastUsedSeed;

        string json = JsonUtility.ToJson(data);
        File.WriteAllText(SlotPath(slot), json);
    }

    // Applies a previously-saved slot onto the live scene. Call this only after the game has
    // actually started (the giant, GiantProgression, GameManager etc. must already exist).
    public static void LoadFromSlot(int slot)
    {
        GiantSaveData data = ReadSlot(slot);
        if (data == null || !data.used) return;

        GameObject giantObj = GameObject.FindGameObjectWithTag("Player");
        if (giantObj == null) return;

        GiantController giant = giantObj.GetComponent<GiantController>();

        // Rebuild the exact map/citizens this slot was saved with FIRST -- before touching the
        // giant's position at all -- then re-cage the right number of hostages. This order
        // matters: destroying/reinstantiating dozens of buildings and citizens takes a
        // noticeable moment, and doing it AFTER the giant had already been dropped at ground
        // level let that one slow frame's inflated Time.deltaTime apply a big burst of gravity
        // right as the giant landed, which was enough to tunnel it straight through the thin
        // ground plane collider and fall forever. Doing the heavy rebuild first means that
        // slow moment happens while the giant is still wherever it was before the load, and
        // the actual teleport-and-resume-gravity below lands on a normal-length frame. Skipped
        // for saves made before this feature existed (hasCitySeed false) -- those fall back to
        // the old behavior of just leaving the scene's freshly-generated random city as-is.
        if (data.hasCitySeed)
        {
            CityGenerator cityGen = Object.FindObjectOfType<CityGenerator>();
            if (cityGen != null)
            {
                cityGen.RegenerateFromSeed(data.citySeed);
            }

            if (giant != null)
            {
                giant.RestoreCagedHostages(data.cagedHostageCount);
            }
        }

        CharacterController cc = giantObj.GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        giantObj.transform.position = new Vector3(data.posX, data.posY, data.posZ);
        giantObj.transform.rotation = Quaternion.Euler(0f, data.rotY, 0f);

        if (cc != null) cc.enabled = ccWasEnabled;

        if (giant != null)
        {
            // Belt-and-suspenders alongside the reordering above: whatever vertical fall
            // speed the giant had going into this Load must not survive the teleport, or
            // gravity resuming next frame could still yank it down through the ground.
            giant.ResetFallVelocity();
        }

        GiantHealth health = giantObj.GetComponent<GiantHealth>();
        if (health != null)
        {
            health.maxHP = data.maxHP;
            health.currentHP = data.currentHP;
            health.damageReductionPercent = data.damageReductionPercent;
            health.UpdateUI();
        }

        if (giant != null)
        {
            giant.moveSpeed = data.moveSpeed;
            giant.attackDamage = data.attackDamage;
            giant.maxStamina = data.maxStamina;
            giant.currentStamina = data.currentStamina;
        }

        GiantProgression progression = GiantProgression.Instance;
        if (progression != null)
        {
            progression.level = data.level;
            progression.currentXP = data.currentXP;
            progression.coins = data.coins;
            progression.skillPoints = data.skillPoints;
            progression.UpdateUI();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SyncWaveThresholdsToLevel(data.level);
        }

        StatUpgradeMenu statMenu = Object.FindObjectOfType<StatUpgradeMenu>();
        if (statMenu != null)
        {
            statMenu.SetStatLevels(new int[] { data.hpLevel, data.speedLevel, data.attackLevel, data.defenseLevel, data.staminaLevel });
        }
    }
}
