using UnityEngine;
using Normal.Realtime;
using System.IO;
using System.Text;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class DataLogger : MonoBehaviour {
    [Header("Assigned via Inspector")]
    public AvatarConfigData avatarConfigData;

    [Header("Assign virtual hand bones")]
    public Transform virtualLeftHand;
    public Transform virtualRightHand;

    private StreamWriter writer;
    private RealtimeView view;
    private Realtime realtime;
    private string role = "Unknown";
    private string dyadID = "UnknownDyad";
    private string condition = "UnknownCondition";
    private string complexity = "UnknownComplexity";
    private float deltaTime = 0.0f;

    void Awake() {
        view = GetComponent<RealtimeView>();
        realtime = FindObjectOfType<Realtime>();

        if (avatarConfigData != null) {
            dyadID = avatarConfigData.dyadID;
        }

        ParseSceneName();
    }

    private void ParseSceneName() {
        string scene = SceneManager.GetActiveScene().name.ToLower();

        if (scene.Contains("lowfid")) condition = "LowFid";
        else if (scene.Contains("highfidnas")) condition = "HighFidNAS";
        else if (scene.Contains("highfid")) condition = "HighFid";

        if (scene.Contains("simple")) complexity = "Simple";
        else if (scene.Contains("complex")) complexity = "Complex";
    }

    public void BeginTrial() {
        if (!view.isOwnedLocallySelf) return;

        // Decide role based on RoleManager, but rename to ParticipantA/B in the data
        if (RoleManager.Instance != null && realtime != null) {
            if (RoleManager.Instance.IsTeacher(realtime.clientID)) {
                role = "ParticipantA";
            } else if (RoleManager.Instance.IsStudent(realtime.clientID)) {
                role = "ParticipantB";
            } else {
                role = "Unknown";
            }
        } else {
            role = "Unknown";
        }

        string dir = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(dir);

        // Simpler filenames: Dyad_Condition_Role.csv
        string filename = $"{dyadID}_{condition}_{role}.csv";
        string path = Path.Combine(dir, filename);

        writer = new StreamWriter(path, false, Encoding.UTF8);
        writer.WriteLine(
            "DyadID,Condition,Role,Timestamp,FPS," +
            "HeadPosX,HeadPosY,HeadPosZ,HeadRotW,HeadRotX,HeadRotY,HeadRotZ," +
            "LCtrlPosX,LCtrlPosY,LCtrlPosZ,LCtrlRotW,LCtrlRotX,LCtrlRotY,LCtrlRotZ," +
            "RCtrlPosX,RCtrlPosY,RCtrlPosZ,RCtrlRotW,RCtrlRotX,RCtrlRotY,RCtrlRotZ," +
            "ArmScaleErrorL,ArmScaleErrorR," +
            "PinchStateL,GripValueL,PinchStateR,GripValueR," +
            "HeadVelX,HeadVelY,HeadVelZ," +
            "LCtrlVelX,LCtrlVelY,LCtrlVelZ," +
            "RCtrlVelX,RCtrlVelY,RCtrlVelZ"
        );
    }

    void LateUpdate() {
        if (!view.isOwnedLocallySelf || writer == null) return;

        // Frame rate calculation
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;

        float tstamp = Time.time;

        // Headset pose
        Vector3 hPos = Camera.main.transform.position;
        Quaternion hRot = Camera.main.transform.rotation;

        // Controller poses
        Vector3 lPos = InputTracking.GetLocalPosition(XRNode.LeftHand);
        Quaternion lRot = InputTracking.GetLocalRotation(XRNode.LeftHand);
        Vector3 rPos = InputTracking.GetLocalPosition(XRNode.RightHand);
        Quaternion rRot = InputTracking.GetLocalRotation(XRNode.RightHand);

        // Arm scale error
        float scaleErrL = (virtualLeftHand != null) ? Vector3.Distance(virtualLeftHand.position, lPos) : -1f;
        float scaleErrR = (virtualRightHand != null) ? Vector3.Distance(virtualRightHand.position, rPos) : -1f;

        // Grip + Pinch detection (left)
        float gripL = 0f;
        int pinchL = 0;
        var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftDevice.TryGetFeatureValue(CommonUsages.grip, out float gL)) {
            gripL = gL;
            pinchL = (gL > 0.5f) ? 1 : 0;
        }

        // Grip + Pinch detection (right)
        float gripR = 0f;
        int pinchR = 0;
        var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightDevice.TryGetFeatureValue(CommonUsages.grip, out float gR)) {
            gripR = gR;
            pinchR = (gR > 0.5f) ? 1 : 0;
        }

        // Velocity
        Vector3 headVel = Vector3.zero;
        Vector3 lVel = Vector3.zero;
        Vector3 rVel = Vector3.zero;

        var headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        headDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out headVel);
        leftDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out lVel);
        rightDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out rVel);

        // Write to CSV
        writer.WriteLine(
            $"{dyadID},{condition},{complexity},{role},{tstamp:F4},{fps:F1}," +
            $"{hPos.x:F3},{hPos.y:F3},{hPos.z:F3}," +
            $"{hRot.w:F3},{hRot.x:F3},{hRot.y:F3},{hRot.z:F3}," +
            $"{lPos.x:F3},{lPos.y:F3},{lPos.z:F3}," +
            $"{lRot.w:F3},{lRot.x:F3},{lRot.y:F3},{lRot.z:F3}," +
            $"{rPos.x:F3},{rPos.y:F3},{rPos.z:F3}," +
            $"{rRot.w:F3},{rRot.x:F3},{rRot.y:F3},{rRot.z:F3}," +
            $"{scaleErrL:F3},{scaleErrR:F3}," +
            $"{pinchL},{gripL:F2},{pinchR},{gripR:F2}," +
            $"{headVel.x:F3},{headVel.y:F3},{headVel.z:F3}," +
            $"{lVel.x:F3},{lVel.y:F3},{lVel.z:F3}," +
            $"{rVel.x:F3},{rVel.y:F3},{rVel.z:F3}"
        );
    }

    public void EndTrial() {
        if (writer != null) {
            writer.Flush();
            writer.Close();
            writer = null;
        }
    }
}




