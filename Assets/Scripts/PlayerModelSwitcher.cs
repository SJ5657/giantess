using UnityEngine;

// Which body the player is using. Female is the original (and the default for old saves,
// since JsonUtility leaves a missing int field at 0).
public enum PlayerGender
{
    Female = 0,
    Male = 1
}

// Lives on the Giant. Both playable bodies are children of the Giant; this turns on the one
// matching the gender picked on the start screen (GameFlowManager) or stored in a save slot
// (SaveSystem), and tells GiantController which Animator to drive.
public class PlayerModelSwitcher : MonoBehaviour
{
    // The gender currently in use. Reset to Female every time the scene loads, because the
    // scene itself always boots with the female body active.
    public static PlayerGender Current { get; private set; } = PlayerGender.Female;

    [Tooltip("Body used for the female character (the Giant's original Model child).")]
    public GameObject femaleModel;
    [Tooltip("Body used for the male character (GiantessMan). Starts inactive.")]
    public GameObject maleModel;
    [Tooltip("Auto-found on this object if left empty.")]
    public GiantController giant;

    void Awake()
    {
        Current = PlayerGender.Female;
        if (giant == null) giant = GetComponent<GiantController>();
        SetVisible(PlayerGender.Female);
    }

    public void Apply(PlayerGender gender)
    {
        // No male body assigned -> just stay female instead of ending up with no body at all.
        if (gender == PlayerGender.Male && maleModel == null) gender = PlayerGender.Female;

        Current = gender;
        SetVisible(gender);

        GameObject active = gender == PlayerGender.Male ? maleModel : femaleModel;
        if (active == null) return;

        Animator anim = active.GetComponentInChildren<Animator>(true);
        if (giant != null && anim != null)
        {
            giant.SetModelAnimator(anim);
        }
    }

    void SetVisible(PlayerGender gender)
    {
        bool male = gender == PlayerGender.Male && maleModel != null;
        if (femaleModel != null) femaleModel.SetActive(!male);
        if (maleModel != null) maleModel.SetActive(male);
    }
}
