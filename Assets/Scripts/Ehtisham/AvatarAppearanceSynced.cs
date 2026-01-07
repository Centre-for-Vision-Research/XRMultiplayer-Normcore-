using UnityEngine;
using UMA;
using UMA.CharacterSystem;
using Normal.Realtime;

public class AvatarAppearanceSync : RealtimeComponent<RealtimeAvatarAppearanceModel>
{
    public DynamicCharacterAvatar dca;                // Assigned in Inspector or fetched in Awake
    public AvatarConfigData localConfig;              // Only used by local player

    private bool _hasApplied = false;

    private void Awake()
    {
        if (dca == null)
            dca = GetComponent<DynamicCharacterAvatar>();
    }

    protected override void OnRealtimeModelReplaced(RealtimeAvatarAppearanceModel previousModel, RealtimeAvatarAppearanceModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.skinToneDidChange -= OnSkinToneChanged;
            previousModel.physiqueDidChange -= OnPhysiqueChanged;
        }

        if (currentModel != null)
        {
            if (realtimeView.isOwnedLocallySelf && localConfig != null)
            {
                model.skinTone = localConfig.skinShade;
                model.physique = localConfig.physique;
            }

            currentModel.skinToneDidChange += OnSkinToneChanged;
            currentModel.physiqueDidChange += OnPhysiqueChanged;

            // Initial apply
            ApplyAllFromModel();
        }
    }

    private void OnSkinToneChanged(RealtimeAvatarAppearanceModel model, string value) => ApplySkinTone(value);
    private void OnPhysiqueChanged(RealtimeAvatarAppearanceModel model, string value) => ApplyPhysique(value);

    private void ApplyAllFromModel()
    {
        if (_hasApplied || model == null) return;
        ApplySkinTone(model.skinTone);
        ApplyPhysique(model.physique);
        _hasApplied = true;
    }

    private void ApplySkinTone(string tone)
    {
        if (dca == null) return;

        Color skinColor = tone.ToLower() switch
        {
            "light" => new Color(1.0f, 0.84f, 0.72f),
            "medium" => new Color(0.75f, 0.57f, 0.43f),
            "dark" => new Color(0.4f, 0.3f, 0.2f),
            _ => Color.white
        };

        dca.SetColor("Skin", skinColor);
        dca.BuildCharacter();
    }

    private void ApplyPhysique(string value)
    {
        float armWidth, forearmWidth, handSize;
        float neckThickness, lowerMuscle, lowerWeight, upperMuscle, upperWeight;
        float belly, waist;

        switch (value.ToLower())
        {
            case "slim":
                armWidth = 0.3f; forearmWidth = 0.3f; handSize = 0.3f;
                neckThickness = 0.3f;
                lowerMuscle = 0.3f; lowerWeight = 0.3f;
                upperMuscle = 0.3f; upperWeight = 0.35f;
                belly = 0.4f; waist = 0.4f;
                break;

            case "broad":
                armWidth = 0.75f; forearmWidth = 0.75f; handSize = 0.75f;
                neckThickness = 0.7f;
                lowerMuscle = 0.8f; lowerWeight = 0.7f;
                upperMuscle = 0.7f; upperWeight = 0.6f;
                belly = 0.6f; waist = 0.5f;
                break;

            default: // medium
                armWidth = forearmWidth = handSize = 0.5f;
                neckThickness = lowerMuscle = lowerWeight = 0.5f;
                upperMuscle = 0.5f; upperWeight = 0.5f;
                belly = waist = 0.5f;
                break;
        }

        dca.SetDNA("armWidth", armWidth);
        dca.SetDNA("forearmWidth", forearmWidth);
        dca.SetDNA("handSize", handSize);
        dca.SetDNA("neckThickness", neckThickness);
        dca.SetDNA("lowerMuscle", lowerMuscle);
        dca.SetDNA("lowerWeight", lowerWeight);
        dca.SetDNA("upperMuscle", upperMuscle);
        dca.SetDNA("upperWeight", upperWeight);
        dca.SetDNA("belly", belly);
        dca.SetDNA("waist", waist);
        
        dca.BuildCharacter();


    }
}
