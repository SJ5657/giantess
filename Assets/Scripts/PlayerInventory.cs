using UnityEngine;

// Simple item inventory for the giant, separate from GiantProgression's coins. Currently
// holds just HP Potions (bought from the Hostage Shop -- see HostageShopMenu, opened by
// left-clicking near the birdcage with an empty hand), viewed/used from the Inventory
// popup (X -- see InventoryMenu). Built as a general item-count store so more item types
// can be added later without restructuring. Not part of GiantSaveData (see SaveSystem), so
// hpPotionCount always starts fresh at startingHPPotionCount whenever the scene loads,
// whether that's a brand new game or a loaded save.
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    [Tooltip("How much HP a single potion restores when used from the Inventory menu (X).")]
    public float hpPotionHealAmount = 80f;

    [Tooltip("How many HP Potions the player has in hand right at game start.")]
    public int startingHPPotionCount = 3;

    public int hpPotionCount = 0;

    void Awake()
    {
        Instance = this;
        hpPotionCount = startingHPPotionCount;
    }

    public void AddHPPotions(int amount)
    {
        if (amount <= 0) return;
        hpPotionCount += amount;
    }

    // Consumes one HP potion and heals the given GiantHealth. Returns false (does nothing)
    // if there are none left.
    public bool UseHPPotion(GiantHealth health)
    {
        if (hpPotionCount <= 0) return false;

        hpPotionCount--;
        if (health != null)
        {
            health.Heal(hpPotionHealAmount);
        }
        return true;
    }
}
