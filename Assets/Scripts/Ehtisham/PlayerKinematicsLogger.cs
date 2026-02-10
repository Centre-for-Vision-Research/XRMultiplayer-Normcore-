using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using Normal.Realtime;

public class PlayerKinematicsLogger : MonoBehaviour
{
    [Header("Assigned via Inspector")]
    public AvatarConfigData avatarConfigData;

    [Header("Optional virtual hand bones for arm scale error")]
    public Transform virtualLeftHand;
    public Transform virtualRightHand;

    [Header("Debug")]
    public bool verboseLogs = false;

    private RealtimeView _view;
    private Realtime _realtime;

    private StreamWriter _writer;
    private float _fpsSmoother = 0f;

    private bool _logging = false;
    private int _lastGameState = -1;

    private string _dyadId = "UnknownDyad";
    private string _condition = "Unknown";
    private string _role = "Unknown";

    private void Awake()
    {
        _view = GetComponent<RealtimeView>();
        _realtime = FindObjectOfType<Realtime>();

        if (avatarConfigData != null) _dyadId = avatarConfigData.dyadID;
        _condition = WhackConditionUtil.GetConditionFromScene();
    }

    private void Update()
    {
        if (_view == null || !_view.isOwnedLocallySelf) return;

        var gs = WhackGameStateSync.Instance;
        if (gs == null) return;

        int state = gs.GameState;
        if (state != _lastGameState)
        {
            _lastGameState = state;

            if (state == 2 && !_logging)
            {
                BeginTrial();
            }
            else if (state == 3 && _logging)
            {
                EndTrial();
            }
        }
    }

    private void BeginTrial()
    {
        _role = ResolveRole();

        string dir = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(dir);

        // OVERWRITE always for kinematics logs
        string filename = $"{_dyadId}_{_condition}_{_role}.csv";
        string path = Path.Combine(dir, filename);

        _writer = new StreamWriter(path, false, Encoding.UTF8);
        _writer.WriteLine(
            "DyadID,Condition,Role,Timestamp,FPS," +
            "HeadPosX,HeadPosY,HeadPosZ,HeadRotW,HeadRotX,HeadRotY,HeadRotZ," +
            "LCtrlPosX,LCtrlPosY,LCtrlPosZ,LCtrlRotW,LCtrlRotX,LCtrlRotY,LCtrlRotZ," +
            "RCtrlPosX,RCtrlPosY,RCtrlPosZ,RCtrlRotW,RCtrlRotX,RCtrlRotY,RCtrlRotZ," +
            "ArmScaleErrorL,ArmScaleErrorR," +
            "GripL,GripR," +
            "HeadVelX,HeadVelY,HeadVelZ," +
            "LCtrlVelX,LCtrlVelY,LCtrlVelZ," +
            "RCtrlVelX,RCtrlVelY,RCtrlVelZ"
        );

        _logging = true;
        if (verboseLogs) Debug.Log($"[PlayerKinematicsLogger] BeginTrial -> {path}");
    }

    private string ResolveRole()
    {
        if (RoleManager.Instance != null && _realtime != null)
        {
            int cid = _realtime.clientID;
            if (RoleManager.Instance.IsTeacher(cid)) return "A";
            if (RoleManager.Instance.IsStudent(cid)) return "B";
        }
        return "Unknown";
    }

    private void LateUpdate()
    {
        if (!_logging || _writer == null) return;
        if (_view == null || !_view.isOwnedLocallySelf) return;

        _fpsSmoother += (Time.unscaledDeltaTime - _fpsSmoother) * 0.1f;
        float fps = _fpsSmoother > 0.00001f ? (1f / _fpsSmoother) : 0f;

        float t = Time.time;

        // Head pose
        Vector3 hPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        Quaternion hRot = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;

        // Controller local poses
        Vector3 lPos = InputTracking.GetLocalPosition(XRNode.LeftHand);
        Quaternion lRot = InputTracking.GetLocalRotation(XRNode.LeftHand);

        Vector3 rPos = InputTracking.GetLocalPosition(XRNode.RightHand);
        Quaternion rRot = InputTracking.GetLocalRotation(XRNode.RightHand);

        // Arm scale error (optional)
        float scaleErrL = (virtualLeftHand != null) ? Vector3.Distance(virtualLeftHand.position, lPos) : -1f;
        float scaleErrR = (virtualRightHand != null) ? Vector3.Distance(virtualRightHand.position, rPos) : -1f;

        // Grip
        float gripL = 0f;
        float gripR = 0f;

        var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (left.TryGetFeatureValue(CommonUsages.grip, out float gL)) gripL = gL;

        var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (right.TryGetFeatureValue(CommonUsages.grip, out float gR)) gripR = gR;

        // Velocities
        Vector3 headVel = Vector3.zero;
        Vector3 lVel = Vector3.zero;
        Vector3 rVel = Vector3.zero;

        var headDev = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        headDev.TryGetFeatureValue(CommonUsages.deviceVelocity, out headVel);
        left.TryGetFeatureValue(CommonUsages.deviceVelocity, out lVel);
        right.TryGetFeatureValue(CommonUsages.deviceVelocity, out rVel);

        _writer.WriteLine(
            $"{_dyadId},{_condition},{_role},{t:F4},{fps:F1}," +
            $"{hPos.x:F3},{hPos.y:F3},{hPos.z:F3}," +
            $"{hRot.w:F3},{hRot.x:F3},{hRot.y:F3},{hRot.z:F3}," +
            $"{lPos.x:F3},{lPos.y:F3},{lPos.z:F3}," +
            $"{lRot.w:F3},{lRot.x:F3},{lRot.y:F3},{lRot.z:F3}," +
            $"{rPos.x:F3},{rPos.y:F3},{rPos.z:F3}," +
            $"{rRot.w:F3},{rRot.x:F3},{rRot.y:F3},{rRot.z:F3}," +
            $"{scaleErrL:F3},{scaleErrR:F3}," +
            $"{gripL:F2},{gripR:F2}," +
            $"{headVel.x:F3},{headVel.y:F3},{headVel.z:F3}," +
            $"{lVel.x:F3},{lVel.y:F3},{lVel.z:F3}," +
            $"{rVel.x:F3},{rVel.y:F3},{rVel.z:F3}"
        );
    }

    private void EndTrial()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Close();
            _writer = null;
        }
        _logging = false;

        if (verboseLogs) Debug.Log("[PlayerKinematicsLogger] EndTrial");
    }

    private void OnDestroy()
    {
        EndTrial();
    }
}
