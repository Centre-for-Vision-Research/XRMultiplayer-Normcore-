using UnityEngine;
using Unity.XR.CoreUtils;
using Normal.Realtime;

[System.Serializable]
public class VRMap {
    public Transform vrTarget; // Assigned at runtime
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

    public Vector3 headBodyPositionOffset; // Editable in Inspector
    public float headBodyYawOffset;
    public float avatarHeight = 1.7f; // Default height of the avatar

    private bool vrTargetsAssigned = false;
    private bool calibrationDone = false;

    private XROrigin xrOrigin;
    private float groundY; // y position of ground. we need to account for it if its not 0.

    private RealtimeView realtimeView; // For Normcore ownership checks

    void Start() {
        // Get the RealtimeView component for ownership checks
        realtimeView = GetComponent<RealtimeView>();
        if (realtimeView == null) {
            Debug.LogError("RealtimeView component not found! Please add it to the GameObject.");
            return;
        }

        // Perform initialization only if this is the local player
        if (!realtimeView.isOwnedLocally) {
            return;
        }

        xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null) {
            Debug.LogError("XR Origin not found!");
            return;
        }

        // Reset camera offset for local player
        xrOrigin.CameraYOffset = 0f;

        // Find ground Y position
        GameObject floorObject = GameObject.FindGameObjectWithTag("Floor");
        if (floorObject != null) {
            groundY = floorObject.transform.position.y;
        } else {
            Debug.LogError("No GameObject with tag 'Floor' found! Defaulting groundY to 0.");
        }

        StartCalibration();
    }

    void Update() {
        // Only proceed if this is the local player
        if (!realtimeView.isOwnedLocally)
            return;

        if (!vrTargetsAssigned && calibrationDone) {
            AssignVRTargets();
        }
    }

    void LateUpdate() {
        // Only proceed if this is the local player
        if (!realtimeView.isOwnedLocally)
            return;

        if (!vrTargetsAssigned || !calibrationDone)
            return;

        transform.position = head.ikTarget.position + headBodyPositionOffset;
        float yaw = head.vrTarget.eulerAngles.y;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), turnSmoothness);

        head.Map();
        leftHand.Map();
        rightHand.Map();
    }

    void StartCalibration() {
        // Start calibration routine only for the local player
        if (!realtimeView.isOwnedLocally)
            return;

        StartCoroutine(CalibrationRoutine());
    }

    System.Collections.IEnumerator CalibrationRoutine() {
        Debug.Log("Starting calibration...");

        // Wait for 1 second to stabilize VR camera position
        yield return new WaitForSeconds(1f);

        // Directly get the main camera's Y position
        float userHeight = Camera.main.transform.position.y;

        // Calculate the required offset
        float requiredOffset = avatarHeight - (userHeight - groundY);
        xrOrigin.CameraYOffset = requiredOffset;

        Debug.Log($"Calibration complete: User Height = {userHeight}, Ground Y = {groundY}, Required Offset = {requiredOffset}, Final Camera Y Offset = {xrOrigin.CameraYOffset}");

        calibrationDone = true;
    }

    void AssignVRTargets() {
        // Only assign VR targets for the local player
        if (!realtimeView.isOwnedLocally)
            return;

        GameObject headTarget = GameObject.Find("Head Camera Target");
        GameObject leftHandTarget = GameObject.Find("Left Cont Target");
        GameObject rightHandTarget = GameObject.Find("Right Cont Target");

        if (headTarget != null && leftHandTarget != null && rightHandTarget != null) {
            head.vrTarget = headTarget.transform;
            leftHand.vrTarget = leftHandTarget.transform;
            rightHand.vrTarget = rightHandTarget.transform;
            vrTargetsAssigned = true;
            Debug.Log("VR Targets successfully assigned.");
        }
    }
}






// using UnityEngine;
// using Unity.XR.CoreUtils;


// [System.Serializable]
// public class VRMap {
//     public Transform vrTarget; // Will assign at runtime
//     public Transform ikTarget; // Assigned in the prefab
//     public Vector3 trackingPositionOffset;
//     public Vector3 trackingRotationOffset;

//     public void Map() {
//         if (vrTarget == null || ikTarget == null)
//             return;

//         ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
//         ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
//     }
// }

// public class IKTargetFollowVRRig : MonoBehaviour {
//     [Range(0, 1)]
//     public float turnSmoothness = 0.1f;

//     public VRMap head;
//     public VRMap leftHand;
//     public VRMap rightHand;

//     public Vector3 headBodyPositionOffset;
//     public float headBodyYawOffset;

//     private bool vrTargetsAssigned = false;

//     void Start() {

//     }


//     void Update() {
//         // Attempt to assign vrTargets if not already assigned
//         if (!vrTargetsAssigned) {
//             AssignVRTargets();
//         }
//     }

//     void LateUpdate() {
//         if (!vrTargetsAssigned)
//             return;

//         transform.position = head.ikTarget.position + headBodyPositionOffset;
//         float yaw = head.vrTarget.eulerAngles.y;
//         transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), turnSmoothness);

//         head.Map();
//         leftHand.Map();
//         rightHand.Map();
//     }

//     void AssignVRTargets() {
//         GameObject headTarget = GameObject.Find("Head Camera Target");
//         GameObject leftHandTarget = GameObject.Find("Left Cont Target");
//         GameObject rightHandTarget = GameObject.Find("Right Cont Target");

//         if (headTarget != null && leftHandTarget != null && rightHandTarget != null) {
//             head.vrTarget = headTarget.transform;
//             leftHand.vrTarget = leftHandTarget.transform;
//             rightHand.vrTarget = rightHandTarget.transform;
//             vrTargetsAssigned = true;
//         }
//     }
// }