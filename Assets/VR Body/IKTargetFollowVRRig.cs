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

    [Header("Smoothing")]
    [Range(0f, 1f)]
    public float turnSmoothness = 0.1f;

    public VRMap head;
    public VRMap leftHand;
    public VRMap rightHand;

    [Header("Body")]
    public Vector3 headBodyPositionOffset;
    public float headBodyYawOffset;
    public float avatarHeight = 1.7f;

    [Header("MR Rig Root (DO NOT NETWORK)")]
    [Tooltip("Assign RigRoot (child that holds bones + constraints + hammers). In MR we move this instead of the avatar root.")]
    public Transform rigRootToMove;

    [Header("Debug")]
    public bool verboseLogs = false;

    private bool targetsAssigned = false;

    // VR-only calibration
    private bool calibrationDone = false;
    private XROrigin xrOrigin;
    private float groundY = 0f;

    private RealtimeView realtimeView;

    void Start()
    {
        realtimeView = GetComponent<RealtimeView>();
        if (realtimeView == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] Missing RealtimeView on avatar root.");
            return;
        }

        if (isMRScene)
        {
            AssignMRNetTargets();
            calibrationDone = true; // MR does not use CameraYOffset

            if (rigRootToMove == null)
            {
                Debug.LogError("[IKTargetFollowVRRig] MR requires rigRootToMove assigned (RigRoot child).");
            }

            return;
        }

        // VR: only local calibrates
        if (!realtimeView.isOwnedLocallySelf)
            return;

        xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[IKTargetFollowVRRig] XR Origin not found!");
            return;
        }

        xrOrigin.CameraYOffset = 0f;

        GameObject floorObject = GameObject.FindGameObjectWithTag("Floor");
        groundY = (floorObject != null) ? floorObject.transform.position.y : 0f;

        StartCoroutine(CalibrationRoutine_VR());
    }

    void Update()
    {
        if (isMRScene)
        {
            if (!targetsAssigned) AssignMRNetTargets();
            return;
        }

        // VR: only local tries to assign XR targets after calibration
        if (!realtimeView.isOwnedLocallySelf) return;
        if (!calibrationDone) return;
        if (!targetsAssigned) AssignVRTargets();
    }

    void LateUpdate()
    {
        // VR: only local drives
        if (!isMRScene && !realtimeView.isOwnedLocallySelf) return;
        if (!targetsAssigned || !calibrationDone) return;

        // In MR, wait until anchor is ready to avoid any early root motion baking
        if (isMRScene)
        {
            if (MRSharedAnchorManager.Instance == null) return;
            if (!MRSharedAnchorManager.Instance.AnchorReady) return;
            if (rigRootToMove == null) return;
        }

        head.Map();
        leftHand.Map();
        rightHand.Map();

        // IMPORTANT:
        // VR: move avatar root (transform)
        // MR: move non-networked rig root, never move the networked avatar root
        Transform mover = isMRScene ? rigRootToMove : transform;

        mover.position = head.ikTarget.position + headBodyPositionOffset;

        float yaw = head.vrTarget.eulerAngles.y + headBodyYawOffset;
        mover.rotation = Quaternion.Lerp(mover.rotation, Quaternion.Euler(0f, yaw, 0f), turnSmoothness);
    }

    private System.Collections.IEnumerator CalibrationRoutine_VR()
    {
        yield return new WaitForSeconds(1f);

        float userHeight = Camera.main != null ? Camera.main.transform.position.y : 0f;
        float requiredOffset = avatarHeight - (userHeight - groundY);

        xrOrigin.CameraYOffset = requiredOffset;
        calibrationDone = true;
    }

    private void AssignVRTargets()
    {
        Transform h = GameObject.Find(headXRName)?.transform;
        Transform l = GameObject.Find(leftXRName)?.transform;
        Transform r = GameObject.Find(rightXRName)?.transform;

        if (h == null || l == null || r == null) return;

        head.vrTarget = h;
        leftHand.vrTarget = l;
        rightHand.vrTarget = r;

        targetsAssigned = true;
    }

    private void AssignMRNetTargets()
    {
        Transform netRoot = transform.Find(netTargetsRootName);
        if (netRoot == null) return;

        Transform h = netRoot.Find(headNetName);
        Transform l = netRoot.Find(leftNetName);
        Transform r = netRoot.Find(rightNetName);

        if (h == null || l == null || r == null) return;

        head.vrTarget = h;
        leftHand.vrTarget = l;
        rightHand.vrTarget = r;

        targetsAssigned = true;
    }
}
