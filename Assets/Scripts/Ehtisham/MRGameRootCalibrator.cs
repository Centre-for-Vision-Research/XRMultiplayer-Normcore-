using UnityEngine;

public class MRGameRootCalibrator : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("Enable ONLY in MR scenes.")]
    public bool isMRScene = true;

    [Header("Calibration Controls")]
    [Tooltip("Quest A button is JoystickButton0 by default.")]
    public KeyCode calibrateKey = KeyCode.JoystickButton0;

    [Header("Placement Settings")]
    [Tooltip("Distance from camera forward to place the table center.")]
    public float tableDistanceMeters = 0.9f;

    [Tooltip("If true, keep GameRoot current Y. If false, force Y to 0.")]
    public bool keepCurrentY = true;

    [Tooltip("Optional yaw offset for the whole setup (degrees).")]
    public float yawOffsetDegrees = 0f;

    [Header("Debug")]
    public bool verboseLogs = true;

    private bool calibrated = false;

    void Start()
    {
        if (!isMRScene) return;

        if (verboseLogs)
        {
            Debug.Log("[MRGameRootCalibrator] MR mode active. Waiting for user calibration input.");
            Debug.Log($"[MRGameRootCalibrator] tableDistanceMeters={tableDistanceMeters:F3}, keepCurrentY={keepCurrentY}, yawOffsetDegrees={yawOffsetDegrees:F1}");
        }
    }

    void Update()
    {
        if (!isMRScene) return;

        if (Input.GetKeyDown(calibrateKey))
        {
            DoCalibration();
        }
    }

    private void DoCalibration()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[MRGameRootCalibrator] Camera.main is null. Cannot calibrate.");
            return;
        }

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("[MRGameRootCalibrator] Camera forward too small after flattening. Cannot calibrate.");
            return;
        }
        fwd.Normalize();

        float y = keepCurrentY ? transform.position.y : 0f;

        Vector3 newPos = cam.transform.position + fwd * tableDistanceMeters;
        newPos.y = y;

        float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y + yawOffsetDegrees;
        Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

        transform.SetPositionAndRotation(newPos, newRot);
        calibrated = true;

        Debug.Log($"[MRGameRootCalibrator] Calibrated GameRoot. Pos={newPos} RotY={yaw:F1} CameraPos={cam.transform.position}");
    }

    public bool IsCalibrated() => calibrated;
}
