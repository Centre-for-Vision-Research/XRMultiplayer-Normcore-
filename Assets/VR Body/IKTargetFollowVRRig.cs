using UnityEngine;
using Unity.XR.CoreUtils;
using Normal.Realtime;

[System.Serializable]
public class VRMap
{
    public Transform vrTarget;   // XR target (VR) OR NetTarget (MR)
    public Transform ikTarget;   // Constraint target (never networked)
    public Vector3 trackingPositionOffset;
    public Vector3 trackingRotationOffset;

    public void Map()
    {
        if (vrTarget == null || ikTarget == null) return;

        ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
        ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
    }
}

public class IKTargetFollowVRRig : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Enable ONLY in MR/Passthrough scenes.")]
    public bool isMRScene = false;

    [Header("MR NetTargets")]
    public string netTargetsRootName = "NetTargets";
    public string headNetName = "HeadNet";
    public string leftNetName = "LeftHandNet";
    public string rightNetName = "RightHandNet";

    [Header("VR Target Names")]
    public string headXRName = "Head Camera Target";
    public string leftXRName = "Left Cont Target";
    public string rightXRName = "Right Cont Target";

    [Header("VR")]
    [Range(0f, 1f)]
    public float turnSmoothness = 0.1f;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    [Header("Body")]
    public Vector3 headBodyPositionOffset;
    public float headBodyYawOffset;
    public float avatarHeight = 1.7f;

    [Header("Debug")]
    public bool verboseLogs = true;
    public float logEverySeconds = 2f;

    private bool targetsAssigned = false;

    // VR-only calibration flags
    private bool calibrationDone = false;
    private XROrigin xrOrigin;
    private float groundY = 0f;

    // MR-only references (local owner)
    private Transform xrHead;

    private RealtimeView realtimeView;

    private float nextLogTime = 0f;

    void Start()
    {
        realtimeView = GetComponent<RealtimeView>();
        if (realtimeView == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] Missing RealtimeView on avatar root.");
            return;
        }

        // ------------------------------------------------------------
        // MR PATH (leave VR calibration untouched)
        // ------------------------------------------------------------
        // if (isMRScene)
        // {
        //     AssignMRNetTargets(); // assign for both local and remote (NetTargets exist on prefab)

        //     if (realtimeView.isOwnedLocallySelf)
        //     {
        //         xrHead = GameObject.Find(headXRName)?.transform;
        //         if (xrHead == null)
        //         {
        //             Debug.LogError($"[IKTargetFollowVRRig] MR: Could not find '{headXRName}' for local owner.");
        //         }
        //     }

        //     calibrationDone = true; // MR does not use CameraYOffset
        //     return;
        // }

        // ------------------------------------------------------------
        // VR PATH (keep your original behavior)
        // ------------------------------------------------------------

        if (!realtimeView.isOwnedLocallySelf)
        {
            if (verboseLogs)
                Debug.Log("[IKTargetFollowVRRig] VR remote avatar: skipping calibration/mapping.");
            return;
        }

        xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] XR Origin not found!");
            return;
        }

        xrOrigin.CameraYOffset = 0f;

        GameObject floorObject = GameObject.FindGameObjectWithTag("Floor");
        if (floorObject != null)
        {
            groundY = floorObject.transform.position.y;
        }
        else
        {
            groundY = 0f;
            Debug.LogWarning("[IKTargetFollowVRRig] No 'Floor' tag found. Defaulting groundY=0.");
        }

        StartCoroutine(CalibrationRoutine_VR());
    }

    void Update()
    {
        if (!realtimeView.isOwnedLocallySelf) return;
        if (!calibrationDone) return;

        if (!targetsAssigned)
        {
            AssignVRTargets(); // keep trying each frame until found
        }
        return;

        // VR: keep trying to assign XR targets after calibration until success
        // if (!isMRScene)
        // {
        //     if (!realtimeView.isOwnedLocallySelf) return;
        //     if (!calibrationDone) return;

        //     if (!targetsAssigned)
        //     {
        //         AssignVRTargets(); // keep trying each frame until found
        //     }
        //     return;
        // }

        // MR: targets assigned in Start; nothing required here
    }

    void LateUpdate()
    {
        if (!realtimeView.isOwnedLocallySelf) return;
        if (!targetsAssigned || !calibrationDone)
        {
            DebugLogStatus();
            return;
        }

        // IMPORTANT CHANGE: Map FIRST so head.ikTarget is current frame, not prefab default
        head.Map();
        leftHand.Map();
        rightHand.Map();

        // Then move avatar root based on mapped head IK target (your original behavior)
        transform.position = head.ikTarget.position + headBodyPositionOffset;

        float yaw = head.vrTarget.eulerAngles.y + headBodyYawOffset;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), turnSmoothness);

        DebugLogStatus();
        return;


        // // ------------------------
        // // VR behavior (same intent, safer order)
        // // ------------------------
        // if (!isMRScene)
        // {
        //     if (!realtimeView.isOwnedLocallySelf) return;
        //     if (!targetsAssigned || !calibrationDone)
        //     {
        //         DebugLogStatus();
        //         return;
        //     }

        //     // IMPORTANT CHANGE: Map FIRST so head.ikTarget is current frame, not prefab default
        //     head.Map();
        //     leftHand.Map();
        //     rightHand.Map();

        //     // Then move avatar root based on mapped head IK target (your original behavior)
        //     transform.position = head.ikTarget.position + headBodyPositionOffset;

        //     float yaw = head.vrTarget.eulerAngles.y + headBodyYawOffset;
        //     transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), turnSmoothness);

        //     DebugLogStatus();
        //     return;
        // }

        // // ------------------------
        // // MR behavior
        // // ------------------------
        // if (!targetsAssigned)
        // {
        //     DebugLogStatus();
        //     return;
        // }

        // // Do NOT move avatar root in MR.
        // // Root stays in NetworkAvatars under the shared anchor.
        // // Just map NetTargets -> IK targets.
        // head.Map();
        // leftHand.Map();
        // rightHand.Map();

        // DebugLogStatus();
    }

    private System.Collections.IEnumerator CalibrationRoutine_VR()
    {
        yield return new WaitForSeconds(1f);

        float userHeight = Camera.main != null ? Camera.main.transform.position.y : 0f;
        float requiredOffset = avatarHeight - (userHeight - groundY);

        xrOrigin.CameraYOffset = requiredOffset;
        calibrationDone = true;

        if (verboseLogs)
        {
            Debug.Log($"[IKTargetFollowVRRig] VR calibration done. userHeight={userHeight:F3}, groundY={groundY:F3}, CameraYOffset={requiredOffset:F3}");
        }
    }

    private void AssignVRTargets()
    {
        Transform h = GameObject.Find(headXRName)?.transform;
        Transform l = GameObject.Find(leftXRName)?.transform;
        Transform r = GameObject.Find(rightXRName)?.transform;

        if (h == null || l == null || r == null)
        {
            if (verboseLogs && Time.time >= nextLogTime)
            {
                nextLogTime = Time.time + logEverySeconds;
                Debug.LogWarning(
                    $"[IKTargetFollowVRRig] VR: XR targets missing. " +
                    $"headFound={h != null}, leftFound={l != null}, rightFound={r != null}. " +
                    $"Names: '{headXRName}', '{leftXRName}', '{rightXRName}'."
                );
            }
            return;
        }

        head.vrTarget = h;
        leftHand.vrTarget = l;
        rightHand.vrTarget = r;

        targetsAssigned = true;

        if (verboseLogs)
        {
            Debug.Log("[IKTargetFollowVRRig] VR: XR targets assigned successfully.");
        }
    }

    private void AssignMRNetTargets()
    {
        Transform netRoot = transform.Find(netTargetsRootName);
        if (netRoot == null)
        {
            Debug.LogError($"[IKTargetFollowVRRig] MR: '{netTargetsRootName}' not found under avatar root.");
            return;
        }

        Transform h = netRoot.Find(headNetName);
        Transform l = netRoot.Find(leftNetName);
        Transform r = netRoot.Find(rightNetName);

        if (h == null || l == null || r == null)
        {
            Debug.LogError(
                $"[IKTargetFollowVRRig] MR: Missing NetTargets. head={h != null} left={l != null} right={r != null}. " +
                $"Names: {headNetName}, {leftNetName}, {rightNetName}"
            );
            return;
        }

        head.vrTarget = h;
        leftHand.vrTarget = l;
        rightHand.vrTarget = r;

        targetsAssigned = true;

        if (verboseLogs)
        {
            Debug.Log("[IKTargetFollowVRRig] MR: NetTargets assigned successfully.");
        }
    }

    private void DebugLogStatus()
    {
        if (!verboseLogs) return;
        if (Time.time < nextLogTime) return;
        nextLogTime = Time.time + logEverySeconds;

        string owner = (realtimeView != null && realtimeView.isOwnedLocallySelf) ? "LOCAL" : "REMOTE";
        string mode = isMRScene ? "MR" : "VR";

        Debug.Log(
            $"[IKTargetFollowVRRig] Status mode={mode} owner={owner} " +
            $"calib={calibrationDone} assigned={targetsAssigned} " +
            $"rootPos={transform.position} " +
            $"headTarget={(head.vrTarget != null ? head.vrTarget.name : "null")} " +
            $"headIK={(head.ikTarget != null ? head.ikTarget.position.ToString("F3") : "null")}"
        );
    }
}






//using UnityEngine;
//using Unity.XR.CoreUtils;
//using Normal.Realtime;

//[System.Serializable]
//public class VRMap
//{
//    public Transform vrTarget; // Assigned at runtime
//    public Transform ikTarget; // Assigned in the prefab
//    public Vector3 trackingPositionOffset;
//    public Vector3 trackingRotationOffset;

//    public void Map()
//    {
//        if (vrTarget == null || ikTarget == null)
//            return;

//        ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
//        ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
//    }
//}

//public class IKTargetFollowVRRig : MonoBehaviour
//{
//    [Header("Mode")]
//    [Tooltip("Enable this ONLY in MR/Passthrough scenes.")]
//    public bool isMRScene = false;

//    [Range(0, 1)]
//    public float turnSmoothness = 0.1f;

//    public VRMap head;
//    public VRMap leftHand;
//    public VRMap rightHand;

//    public Vector3 headBodyPositionOffset; // Editable in Inspector
//    public float headBodyYawOffset;
//    public float avatarHeight = 1.7f; // Default height of the avatar

//    private bool vrTargetsAssigned = false;
//    private bool calibrationDone = false;

//    private XROrigin xrOrigin;
//    private float groundY; // y position of ground

//    private RealtimeView realtimeView; // For Normcore ownership checks

//    void Start()
//    {
//        realtimeView = GetComponent<RealtimeView>();
//        if (realtimeView == null)
//        {
//            Debug.LogError("[IKTargetFollowVRRig] RealtimeView component not found on avatar prefab root.");
//            return;
//        }

//        // Only local player runs mapping/calibration
//        if (!realtimeView.isOwnedLocallySelf)
//        {
//            Debug.Log("[IKTargetFollowVRRig] Not owned locally, skipping rig mapping/calibration.");
//            return;
//        }

//        xrOrigin = FindObjectOfType<XROrigin>();
//        if (xrOrigin == null)
//        {
//            Debug.LogError("[IKTargetFollowVRRig] XR Origin not found!");
//            return;
//        }

//        xrOrigin.CameraYOffset = 0f;

//        if (isMRScene)
//        {
//            groundY = 0f;
//            Debug.Log("[IKTargetFollowVRRig] MR mode enabled. Using groundY = 0 and skipping Floor tag lookup.");
//        }
//        else
//        {
//            GameObject floorObject = GameObject.FindGameObjectWithTag("Floor");
//            if (floorObject != null)
//            {
//                groundY = floorObject.transform.position.y;
//                Debug.Log($"[IKTargetFollowVRRig] VR mode. Found Floor tag groundY={groundY:F3}");
//            }
//            else
//            {
//                groundY = 0f;
//                Debug.LogWarning("[IKTargetFollowVRRig] VR mode but no Floor tag found. Defaulting groundY = 0.");
//            }
//        }

//        StartCalibration();
//    }

//    void Update()
//    {
//        if (!realtimeView.isOwnedLocallySelf)
//            return;

//        if (!vrTargetsAssigned && calibrationDone)
//        {
//            AssignVRTargets();
//        }
//    }

//    void LateUpdate()
//    {
//        if (!realtimeView.isOwnedLocallySelf)
//            return;

//        if (!vrTargetsAssigned || !calibrationDone)
//            return;

//        transform.position = head.ikTarget.position + headBodyPositionOffset;

//        float yaw = head.vrTarget.eulerAngles.y;
//        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(0, yaw, 0), turnSmoothness);

//        head.Map();
//        leftHand.Map();
//        rightHand.Map();
//    }

//    void StartCalibration()
//    {
//        if (!realtimeView.isOwnedLocallySelf)
//            return;

//        StartCoroutine(CalibrationRoutine());
//    }

//    System.Collections.IEnumerator CalibrationRoutine()
//    {
//        yield return new WaitForSeconds(1f);

//        float userHeight = Camera.main != null ? Camera.main.transform.position.y : 0f;
//        float requiredOffset = avatarHeight - (userHeight - groundY);

//        xrOrigin.CameraYOffset = requiredOffset;
//        calibrationDone = true;

//        Debug.Log($"[IKTargetFollowVRRig] Calibration done. userHeight={userHeight:F3}, groundY={groundY:F3}, requiredOffset={requiredOffset:F3}");
//    }

//    void AssignVRTargets()
//    {
//        if (!realtimeView.isOwnedLocallySelf)
//            return;

//        GameObject headTarget = GameObject.Find("Head Camera Target");
//        GameObject leftHandTarget = GameObject.Find("Left Cont Target");
//        GameObject rightHandTarget = GameObject.Find("Right Cont Target");

//        if (headTarget != null && leftHandTarget != null && rightHandTarget != null)
//        {
//            head.vrTarget = headTarget.transform;
//            leftHand.vrTarget = leftHandTarget.transform;
//            rightHand.vrTarget = rightHandTarget.transform;
//            vrTargetsAssigned = true;

//            Debug.Log("[IKTargetFollowVRRig] VR Targets assigned successfully.");
//        }
//        else
//        {
//            Debug.LogWarning("[IKTargetFollowVRRig] Could not find VR Targets. Check names: Head Camera Target, Left Cont Target, Right Cont Target.");
//        }
//    }
//}
