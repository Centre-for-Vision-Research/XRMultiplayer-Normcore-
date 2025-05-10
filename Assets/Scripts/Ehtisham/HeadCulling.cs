using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UMA;
using UMA.CharacterSystem;
using Normal.Realtime;


public class HeadCulling : MonoBehaviour
{
    // Bones to scale down (visual head parts only)
    private readonly string[] headBones = {
        "Head", "Neck", "Jaw", "UpperJaw", "LowerJaw", "EyeLeft", "EyeRight", "NoseBase", "NoseBridge", "Teeth", "Tongue"
    };

    void Start()
    {
        var avatar = GetComponent<DynamicCharacterAvatar>();
        var realtimeView = GetComponent<RealtimeView>();

        if (avatar == null || realtimeView == null || !realtimeView.isOwnedLocallySelf)
            return;

        avatar.CharacterCreated.AddListener((umaData) => HideHeadBones(umaData));
    }

    void HideHeadBones(UMAData umaData)
    {
        foreach (string boneName in headBones)
        {
            var bone = umaData.skeleton.GetBoneTransform(boneName);
            if (bone != null)
                bone.localScale = Vector3.zero;
        }
    }
}
