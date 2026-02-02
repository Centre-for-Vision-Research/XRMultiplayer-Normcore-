using UnityEngine;
using Unity.XR.CoreUtils;
using Normal.Realtime;

[System.Serializable]
public class VRMap
{
    public Transform vrTarget; // Assigned at runtime
    public Transform ikTarget; // Assigned in the prefab
    public Vector3 trackingPositionOffset;
    public Vector3 trackingRotationOffset;

    public void Map()
    {
        if (vrTarget == null || ikTarget == null)
            return;

        ikTarget.position = vrTarget.TransformPoint(trackingPositionOffset);
        ikTarget.rotation = vrTarget.rotation * Quaternion.Euler(trackingRotationOffset);
    }
}

public class IKTargetFollowVRRig : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Enable this ONLY in MR/Passthrough scenes.")]
    public bool isMRScene = false;

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
    private float groundY; // y position of ground

    private RealtimeView realtimeView; // For Normcore ownership checks

    void Start()
    {
        realtimeView = GetComponent<RealtimeView>();
        if (realtimeView == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] RealtimeView component not found on avatar prefab root.");
            return;
        }

        // Only local player runs mapping/calibration
        if (!realtimeView.isOwnedLocallySelf)
        {
            Debug.Log("[IKTargetFollowVRRig] Not owned locally, skipping rig mapping/calibration.");
            return;
        }

        xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] XR Origin not found!");
            return;
        }

        xrOrigin.CameraYOffset = 0f;

        if (isMRScene)
        {
            groundY = 0f;
            Debug.Log("[IKTargetFollowVRRig] MR mode enabled. Using groundY = 0 and skipping Floor tag lookup.");
        }
        else
        {
            GameObject floorObject = GameObject.FindGameObjectWithTag("Floor");
            if (floorObject != null)
            {
                groundY = floorObject.transform.position.y;
                Debug.Log($"[IKTargetFollowVRRig] VR mode. Found Floor tag groundY={groundY:F3}");
            }
            else
            {
                groundY = 0f;
                Debug.LogWarning("[IKTargetFollowVRRig] VR mode but no Floor tag found. Defaulting groundY = 0.");
            }
        }

        StartCalibration();
    }

    void Update()
    {
        if (!realtimeView.isOwnedLocallySelf)
            return;

        if (!vrTargetsAssigned && calibrationDone)
        {
            AssignVRTargets();
        }
    }

    void LateUpdate()
    {
        if (!realtimeView.isOwnedLocallySelf)
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

    void StartCalibration()
    {
        if (!realtimeView.isOwnedLocallySelf)
            return;

        StartCoroutine(CalibrationRoutine());
    }

    System.Collections.IEnumerator CalibrationRoutine()
    {
        yield return new WaitForSeconds(1f);

        float userHeight = Camera.main != null ? Camera.main.transform.position.y : 0f;
        float requiredOffset = avatarHeight - (userHeight - groundY);

        xrOrigin.CameraYOffset = requiredOffset;
        calibrationDone = true;

        Debug.Log($"[IKTargetFollowVRRig] Calibration done. userHeight={userHeight:F3}, groundY={groundY:F3}, requiredOffset={requiredOffset:F3}");
    }

    void AssignVRTargets()
    {
        if (!realtimeView.isOwnedLocallySelf)
            return;

        GameObject headTarget = GameObject.Find("Head Camera Target");
        GameObject leftHandTarget = GameObject.Find("Left Cont Target");
        GameObject rightHandTarget = GameObject.Find("Right Cont Target");

        if (headTarget != null && leftHandTarget != null && rightHandTarget != null)
        {
            head.vrTarget = headTarget.transform;
            leftHand.vrTarget = leftHandTarget.transform;
            rightHand.vrTarget = rightHandTarget.transform;
            vrTargetsAssigned = true;

            Debug.Log("[IKTargetFollowVRRig] VR Targets assigned successfully.");
        }
        else
        {
            Debug.LogWarning("[IKTargetFollowVRRig] Could not find VR Targets. Check names: Head Camera Target, Left Cont Target, Right Cont Target.");
        }
    }
}
