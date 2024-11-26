using UnityEngine;

[System.Serializable]
public class VRMap {
    public Transform vrTarget; // Will assign at runtime
    public Transform ikTarget; // Assigned in the prefab
    public Vector3 trackingPositionOffset;
    public Vector3 trackingRotationOffset;

    public void Map() {
        if (vrTarget == null || ikTarget == null)
            return;

        ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
        ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
    }
}

public class IKTargetFollowVRRig : MonoBehaviour {
    [Range(0, 1)]
    public float turnSmoothness = 0.1f;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    public Vector3 headBodyPositionOffset;
    public float headBodyYawOffset;

    private bool vrTargetsAssigned = false;

    public Transform LeftHandAttach;
    public Transform RightHandAttach;


    void Update() {
        // Attempt to assign vrTargets if not already assigned
        if (!vrTargetsAssigned) {
            AssignVRTargets();
        }
    }

    void LateUpdate() {
        if (!vrTargetsAssigned)
            return;

        transform.position = head.ikTarget.position + headBodyPositionOffset;
        float yaw = head.vrTarget.eulerAngles.y;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), turnSmoothness);

        head.Map();
        leftHand.Map();
        rightHand.Map();
    }

    void AssignVRTargets() {
        GameObject headTarget = GameObject.Find("Head Camera Target");
        GameObject leftHandTarget = GameObject.Find("Left Cont Target");
        GameObject rightHandTarget = GameObject.Find("Right Cont Target");

        if (headTarget != null && leftHandTarget != null && rightHandTarget != null) {
            head.vrTarget = headTarget.transform;
            leftHand.vrTarget = leftHandTarget.transform;
            rightHand.vrTarget = rightHandTarget.transform;
            vrTargetsAssigned = true;
        }
    }
}



// V1: NON Multiplayer version
// using UnityEngine;

// [System.Serializable]
// public class VRMap
// {
//     public Transform vrTarget;
//     public Transform ikTarget;
//     public Vector3 trackingPositionOffset;
//     public Vector3 trackingRotationOffset;
//     public void Map()
//     {
//         ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
//         ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
//     }
// }

// public class IKTargetFollowVRRig : MonoBehaviour
// {
//     [Range(0,1)]
//     public float turnSmoothness = 0.1f;
//     public VRMap head;
//     public VRMap leftHand;
//     public VRMap rightHand;

//     public Vector3 headBodyPositionOffset;
//     public float headBodyYawOffset;

//     // Update is called once per frame
//     void LateUpdate()
//     {
        // transform.position = head.ikTarget.position + headBodyPositionOffset;
        // float yaw = head.vrTarget.eulerAngles.y;
        // transform.rotation = Quaternion.Lerp(transform.rotation,Quaternion.Euler(transform.eulerAngles.x, yaw, transform.eulerAngles.z),turnSmoothness);

        // head.Map();
        // leftHand.Map();
        // rightHand.Map();
//     }
// }
