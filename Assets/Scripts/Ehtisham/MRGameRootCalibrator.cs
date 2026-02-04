using UnityEngine;
using Normal.Realtime;
using UnityEngine.XR;

public class MRGameRootCalibrator : MonoBehaviour
{
    [Header("Mode")]
    public bool isMRScene = true;

    [Header("Calibration Trigger")]
    [Tooltip("If true, uses XR InputDevice (recommended for Quest builds). Falls back to KeyCode if XR not available.")]
    public bool useXRButtonA = true;

    [Tooltip("Fallback key. JoystickButton0 is commonly 'A' in editor, but may fail on device.")]
    public KeyCode fallbackCalibrateKey = KeyCode.JoystickButton0;

    [Header("Lock")]
    [Tooltip("If true, calibration works only once. Default false (you can recalibrate anytime).")]
    public bool lockAfterCalibrate = false;

    [Header("Placement Settings")]
    public float tableDistanceMeters = 0.8f;
    public float tableHeightMeters = 0.3f;

    [Header("Optional Yaw Offset")]
    public float yawOffsetDegrees = 0f;

    [Header("Local Flip Target")]
    [Tooltip("Assign the Table transform that parents Holes&Moles (and colliders). This is flipped locally for Student.")]
    public Transform tableRootToFlip;

    [Header("Debug")]
    public bool verboseLogs = true;

    private bool calibrated = false;
    private Realtime realtime;

    // XR input state (edge detect)
    private InputDevice rightHand;
    private bool lastAState = false;

    void Start()
    {
        if (!isMRScene) return;

        realtime = FindObjectOfType<Realtime>();

        if (useXRButtonA)
            TryInitRightHand();

        if (verboseLogs)
            Debug.Log("[MRGameRootCalibrator] MR active. Press A to calibrate.");
    }

    void Update()
    {
        if (!isMRScene) return;

        if (lockAfterCalibrate && calibrated)
            return;

        bool pressed = WasCalibratePressedThisFrame();
        if (pressed)
            DoCalibration();
    }

    // -------------------------
    // Input
    // -------------------------
    private bool WasCalibratePressedThisFrame()
    {
        // Preferred: XR device A button (primaryButton on right controller)
        if (useXRButtonA)
        {
            if (!rightHand.isValid)
                TryInitRightHand();

            if (rightHand.isValid)
            {
                bool aNow = false;
                if (rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out aNow))
                {
                    bool risingEdge = aNow && !lastAState;
                    lastAState = aNow;
                    if (risingEdge) return true;
                }
            }
        }

        // Fallback: KeyCode
        return Input.GetKeyDown(fallbackCalibrateKey);
    }

    private void TryInitRightHand()
    {
        rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (verboseLogs && rightHand.isValid)
            Debug.Log("[MRGameRootCalibrator] RightHand XR device found for A button.");
    }

    // -------------------------
    // Calibration
    // -------------------------
    private void DoCalibration()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[MRGameRootCalibrator] Camera.main null. Cannot calibrate.");
            return;
        }

        // Flatten camera forward on ground plane
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("[MRGameRootCalibrator] Camera forward too small after flattening.");
            return;
        }
        fwd.Normalize();

        // Place GameRoot in front of camera
        Vector3 newPos = cam.transform.position + fwd * tableDistanceMeters;
        newPos.y = tableHeightMeters;

        float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y + yawOffsetDegrees;
        Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

        transform.SetPositionAndRotation(newPos, newRot);
        calibrated = true;

        // Flip table locally for student only IF role info exists
        bool roleKnown = (RoleManager.Instance != null && realtime != null);
        bool isStudent = false;

        if (roleKnown)
        {
            isStudent = RoleManager.Instance.IsStudent(realtime.clientID);
        }

        if (tableRootToFlip != null)
        {
            if (roleKnown)
            {
                tableRootToFlip.localRotation = isStudent ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            }
            else
            {
                // Do not guess flip if roles not ready
                tableRootToFlip.localRotation = Quaternion.identity;
            }
        }

        if (verboseLogs)
        {
            Debug.Log(
                "[MRGameRootCalibrator] Calibrated\n" +
                $"  clientID={(realtime != null ? realtime.clientID.ToString() : "n/a")}\n" +
                $"  roleKnown={roleKnown}\n" +
                $"  role={(roleKnown ? (isStudent ? "Student" : "Teacher") : "Unknown")}\n" +
                $"  gameRootPos={newPos}\n" +
                $"  gameRootYaw={yaw:F1}\n" +
                $"  tableFlipApplied={(tableRootToFlip != null && roleKnown ? (isStudent ? "YES" : "NO") : "NO (role unknown or null)")}\n" +
                $"  lockAfterCalibrate={lockAfterCalibrate}"
            );
        }
    }

    public bool IsCalibrated() => calibrated;
}
