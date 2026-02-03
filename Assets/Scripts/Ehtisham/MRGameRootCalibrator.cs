using UnityEngine;
using Normal.Realtime;

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
    public float tableDistanceMeters = 0.8f;

    [Header("Height")]
    public float tableHeightMeters = 0.3f;

    [Header("Yaw Offsets")]
    [Tooltip("Additional yaw offset applied to everyone.")]
    public float yawOffsetDegrees = 0f;

    [Header("Debug")]
    public bool verboseLogs = true;

    private bool calibrated = false;
    private Realtime realtime;

    void Start()
    {
        if (!isMRScene) return;

        realtime = FindObjectOfType<Realtime>();

        if (verboseLogs)
        {
            Debug.Log("[MRGameRootCalibrator] MR mode active.");
            Debug.Log($"[MRGameRootCalibrator] tableDistanceMeters={tableDistanceMeters:F3}, tableHeightMeters={tableHeightMeters:F3}");
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

        // Flatten camera forward
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("[MRGameRootCalibrator] Camera forward too small after flattening.");
            return;
        }
        fwd.Normalize();

        // Position table in front of camera
        Vector3 newPos = cam.transform.position + fwd * tableDistanceMeters;
        newPos.y = tableHeightMeters;

        // Base yaw from camera
        float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;

        // Role-based correction
        bool isStudent = false;
        if (RoleManager.Instance != null && realtime != null)
        {
            isStudent = RoleManager.Instance.IsStudent(realtime.clientID);
            if (isStudent)
            {
                yaw += 180f;
            }
        }

        yaw += yawOffsetDegrees;
        Quaternion newRot = Quaternion.Euler(0f, yaw, 0f);

        transform.SetPositionAndRotation(newPos, newRot);
        calibrated = true;

        Debug.Log(
            $"[MRGameRootCalibrator] Calibrated GameRoot\n" +
            $"  Role={(isStudent ? "Student" : "Teacher")}\n" +
            $"  Pos={newPos}\n" +
            $"  RotY={yaw:F1}\n" +
            $"  CamPos={cam.transform.position}\n" +
            $"  CamYaw={cam.transform.eulerAngles.y:F1}"
        );
    }

    public bool IsCalibrated() => calibrated;
}
