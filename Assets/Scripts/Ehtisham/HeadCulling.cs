using UnityEngine;
using UMA.CharacterSystem;
using UMA;
using Normal.Realtime;

[RequireComponent(typeof(DynamicCharacterAvatar), typeof(RealtimeView))]
public class HeadCulling : MonoBehaviour
{
    [Tooltip("UMA slot names to hide in first‑person")]
    public string[] headSlots = {
        "M_High poly Head",       
        "MaleEyes",       
        "MaleInnerMouth",  
        "M_Eyebrow",   
        "FemaleEyelash",
        "FemaleEyes",       
        "FemaleEyelashfix",       
        "FemaleInnerMouth",  
        "F_High poly head",   
        "F_High poly Eyebrows"
    };

    DynamicCharacterAvatar _dca;
    RealtimeView          _rv;

    void Awake()
    {
        _dca = GetComponent<DynamicCharacterAvatar>();
        _rv  = GetComponent<RealtimeView>();

        // Wait until UMA has built its character
        _dca.CharacterCreated.AddListener(OnCharacterCreated);
    }

    void OnCharacterCreated(UMAData umaData)
    {
        // Only hide head for your own local avatar
        if (!_rv.isOwnedLocallySelf) return;

        // Clear each head‑related slot
        foreach (var slot in headSlots)
            _dca.ClearSlot(slot);

        // Rebuild so UMA removes those meshes
        _dca.BuildCharacter();
    }
}
