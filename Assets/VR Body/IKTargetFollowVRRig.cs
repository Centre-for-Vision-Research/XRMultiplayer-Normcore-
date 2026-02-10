using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using Normal.Realtime;

[System.Serializable]
public class VRMap
{
    public Transform vrTarget;            // Assigned at runtime
    public Transform ikTarget;            // Assigned in prefab
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
    [Range(0, 1)]
    public float turnSmoothness = 0.1f;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    [Header("Body placement")]
    public Vector3 headBodyPositionOffset;
    public float headBodyYawOffset;

    [Header("Force standing height")]
    [Tooltip("Set to 1.595 to force standing avatar height even if user starts seated.")]
    public float avatarHeight = 1.595f;

    [Header("XR target names")]
    public string headTargetName = "Head Camera Target";
    public string leftHandTargetName = "Left Cont Target";
    public string rightHandTargetName = "Right Cont Target";

    [Header("Debug")]
    public bool verboseLogs = false;

    private bool vrTargetsAssigned = false;
    private bool calibrationDone = false;

    private XROrigin xrOrigin;
    private float groundY = 0f;

    private RealtimeView realtimeView;

    void Start()
    {
        realtimeView = GetComponent<RealtimeView>();
        if (realtimeView == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] Missing RealtimeView on the same GameObject.");
            enabled = false;
            return;
        }

        if (!realtimeView.isOwnedLocallySelf)
        {
            // Remote avatars should NOT run local XR mapping.
            enabled = false;
            return;
        }

        xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] XROrigin not found.");
            enabled = false;
            return;
        }

        // Reset offset before calibrating
        xrOrigin.CameraYOffset = 0f;

        // Find ground reference (optional)
        GameObject floorObject = GameObject.FindGameObjectWithTag("Floor");
        if (floorObject != null) groundY = floorObject.transform.position.y;
        else groundY = 0f;

        StartCoroutine(CalibrationRoutine());
    }

    void Update()
    {
        if (!calibrationDone) return;

        if (!vrTargetsAssigned)
            AssignVRTargets();
    }

    void LateUpdate()
    {
        if (!calibrationDone || !vrTargetsAssigned) return;

        if (head.vrTarget == null)
            return;

        float yaw = head.vrTarget.eulerAngles.y + headBodyYawOffset;
        Quaternion targetRot = Quaternion.Euler(0f, yaw, 0f);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, Mathf.Clamp01(turnSmoothness));

        // 2) Now map IK targets so the constraint rig can follow
        head.Map();
        leftHand.Map();
        rightHand.Map();
        // after you call head.Map(), leftHand.Map(), rightHand.Map()
        transform.position = head.ikTarget.position + headBodyPositionOffset;

    }

    System.Collections.IEnumerator CalibrationRoutine()
    {
        yield return new WaitForSeconds(1f);

        Transform headSource = Camera.main != null ? Camera.main.transform : null;
        float userHeadY = headSource != null ? headSource.position.y : (groundY + 1.6f);

        float currentHeight = userHeadY - groundY;
        float requiredOffset = avatarHeight - currentHeight;

        xrOrigin.CameraYOffset = requiredOffset;
        calibrationDone = true;

        if (verboseLogs)
        {
            Debug.Log($"[IKTargetFollowVRRig] Calibration done. groundY={groundY:F3}, userHeadY={userHeadY:F3}, currentHeight={currentHeight:F3}, avatarHeight={avatarHeight:F3}, CameraYOffset={requiredOffset:F3}");
        }
    }

    void AssignVRTargets()
    {
        // Head target
        Transform headT = GameObject.Find(headTargetName)?.transform;
        if (headT == null && Camera.main != null) headT = Camera.main.transform;

        Transform leftT = GameObject.Find(leftHandTargetName)?.transform;
        Transform rightT = GameObject.Find(rightHandTargetName)?.transform;

        if (headT == null || leftT == null || rightT == null)
        {
            if (verboseLogs)
                Debug.Log($"[IKTargetFollowVRRig] Waiting XR targets. head={headT != null} left={leftT != null} right={rightT != null}");
            return;
        }

        head.vrTarget = headT;
        leftHand.vrTarget = leftT;
        rightHand.vrTarget = rightT;

        vrTargetsAssigned = true;

        if (verboseLogs)
            Debug.Log("[IKTargetFollowVRRig] XR targets assigned.");
    }
}
