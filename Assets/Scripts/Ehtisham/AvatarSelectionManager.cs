using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using UMA;
using UMA.CharacterSystem; 

public class AvatarSelectionManager : MonoBehaviour
{
    [Header("Avatar References in Scene")]
    public GameObject maleAvatar;
    public GameObject femaleAvatar;

    [Header("UI Toggle Groups")]
    public ToggleGroup bodyTypeGroup;
    public ToggleGroup ethnicAppearanceGroup;
    public ToggleGroup skinToneGroup;
    public ToggleGroup physiqueGroup;

    [Header("Scriptable Object to Store Config")]
    public AvatarConfigData avatarConfig;

    private GameObject currentAvatar;

    void Start()
    {
        // Set defaults from toggle states
        ApplyCurrentBodyType();
        ApplyCurrentAppearance();
    }

    // Called by OnValueChanged on all toggles (bool passed is isOn)
    public void OnAnyToggleChanged()
    {
        ApplyCurrentBodyType();
        ApplyCurrentAppearance();
    }

    public void OnConfirmChoice()
    {
        Debug.Log("Avatar config saved: " + avatarConfig.bodyType + ", " + avatarConfig.ethnicAppearance + ", " + avatarConfig.skinShade + ", " + avatarConfig.physique);
        gameObject.SetActive(false); // Hide the canvas
        // Optionally load next scene here
    }

    void ApplyCurrentBodyType()
    {
        string selectedGender = GetSelectedLabel(bodyTypeGroup);
        avatarConfig.bodyType = selectedGender;

        // Enable one avatar and disable the other
        maleAvatar.SetActive(selectedGender == "Male");
        femaleAvatar.SetActive(selectedGender == "Female");

        currentAvatar = selectedGender == "Male" ? maleAvatar : femaleAvatar;
    }

    void ApplyCurrentAppearance()
    {
        avatarConfig.ethnicAppearance = GetSelectedLabel(ethnicAppearanceGroup);
        avatarConfig.skinShade = GetSelectedLabel(skinToneGroup);
        avatarConfig.physique = GetSelectedLabel(physiqueGroup);

        ApplyEthnicAppearance(currentAvatar, avatarConfig.ethnicAppearance);
        ApplySkinShade(currentAvatar, avatarConfig.skinShade);
        ApplyPhysique(currentAvatar, avatarConfig.physique);
    }

    string GetSelectedLabel(ToggleGroup group)
    {
        var toggle = group.ActiveToggles().FirstOrDefault();
        return toggle != null ? toggle.GetComponentInChildren<Text>().text.Trim() : "Unknown";
    }

    // These are placeholders to plug in your material/mesh/dna logic
    void ApplyEthnicAppearance(GameObject avatar, string value)
    {
        Debug.Log("Apply Ethnic Appearance: " + value);
        // TODO: Swap materials/textures
    }

    void ApplySkinShade(GameObject avatar, string value)
    {
        var dca = avatar.GetComponent<UMA.CharacterSystem.DynamicCharacterAvatar>();
        if (dca == null) return;

        Color skinColor;

        switch (value.ToLower())
        {
            case "light":
                skinColor = new Color(1.0f, 0.84f, 0.72f);
                break;
            case "medium":
                skinColor = new Color(0.75f, 0.57f, 0.43f);
                break;
            case "dark":
                skinColor = new Color(0.4f, 0.3f, 0.2f);
                break;
            default:
                skinColor = Color.white;
                break;
        }

        dca.SetColor("Skin", skinColor);
        dca.BuildCharacter(); // Apply changes
    }


    void ApplyPhysique(GameObject avatar, string value)
    {
        var dca = avatar.GetComponent<DynamicCharacterAvatar>();
        if (dca == null) return;

        float armWidth, forearmWidth, handSize;
        float neckThickness, lowerMuscle, lowerWeight;
        float belly, waist;

        switch (value.ToLower())
        {
            case "slim":
                armWidth = 0.3f; forearmWidth = 0.3f; handSize = 0.3f;
                neckThickness = 0.3f;
                lowerMuscle = 0.3f; lowerWeight = 0.3f;
                belly = 0.2f; waist = 0.3f;
                break;

            case "broad":
                armWidth = 0.75f; forearmWidth = 0.75f; handSize = 0.75f;
                neckThickness = 0.7f;
                lowerMuscle = 0.8f; lowerWeight = 0.7f;
                belly = 0.8f; waist = 0.7f;
                break;

            default: // medium
                armWidth = forearmWidth = handSize = 0.5f;
                neckThickness = lowerMuscle = lowerWeight = 0.5f;
                belly = waist = 0.5f;
                break;
        }

        dca.SetDNA("armWidth", armWidth);
        dca.SetDNA("forearmWidth", forearmWidth);
        dca.SetDNA("handSize", handSize);
        dca.SetDNA("neckThickness", neckThickness);
        dca.SetDNA("lowerMuscle", lowerMuscle);
        dca.SetDNA("lowerWeight", lowerWeight);
        dca.SetDNA("belly", belly);
        dca.SetDNA("waist", waist);

        dca.BuildCharacter(); // Rebuild mesh
    }


}
