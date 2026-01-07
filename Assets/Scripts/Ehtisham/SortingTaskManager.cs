using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class SortingTaskManager : MonoBehaviour {
    [Header("Logging Settings")]
    [Tooltip("Check to disable all logging, including logger trial and metrics file.")]
    [SerializeField] private bool disableLogging = false;

    private Dictionary<string, Vector3> objectSpawnPoints = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> objectRotations = new Dictionary<string, Quaternion>();
    private Dictionary<string, bool> placedObjects = new Dictionary<string, bool>();
    private float startTime;
    private int errors = 0;
    private bool timeStarted = false;
    private Realtime realtime;
    private AudioSource audioSource;

    private DataLogger logger;
    private string dyadID = "UnknownDyad";
    private string condition = "UnknownCondition";
    private string complexity = "UnknownComplexity";
    private bool hasShuffled = false;
    private string metricsFilePath;

    public AudioClip correctSound;
    public AudioClip errorSound;

    void Start() {
        realtime = FindObjectOfType<Realtime>();

        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Shape")) {
            objectSpawnPoints[obj.name] = obj.transform.position;
            objectRotations[obj.name] = obj.transform.rotation;
            placedObjects[obj.name] = false;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        ParseSceneName();
    }

    void Update() {
        if (logger == null && !disableLogging) {
            DataLogger[] allLoggers = FindObjectsOfType<DataLogger>();
            foreach (DataLogger log in allLoggers) {
                if (log.GetComponent<RealtimeView>().isOwnedLocallySelf) {
                    logger = log;
                    dyadID = log.avatarConfigData != null ? log.avatarConfigData.dyadID : "UnknownDyad";
                    break;
                }
            }
        }

        if (!timeStarted &&
            (RoleManager.Instance.GetTeacherID() != 0 || RoleManager.Instance.GetStudentID() != 0)) {

            if (!hasShuffled && RoleManager.Instance.IsStudent(realtime.clientID)) {
                ShuffleShapePositions();
                hasShuffled = true;
            }

            if (RoleManager.Instance.IsStudent(realtime.clientID)) {
                startTime = Time.time;
                if (!disableLogging) InitMetricsFile();
            }

            if (!disableLogging && logger != null) {
                logger.BeginTrial();
            }

            timeStarted = true;
        }

        if (RoleManager.Instance.IsStudent(realtime.clientID)) {
            CheckForFallenShapes();
        }

        if (RoleManager.Instance.IsTeacher(realtime.clientID) && AreAllShapesMovedAway()) {
            if (!disableLogging && logger != null) {
                logger.EndTrial();
            }
            Invoke("ReturnToLobby", 2f);
        }
    }

    private void ParseSceneName() {
        string scene = SceneManager.GetActiveScene().name.ToLower();
        if (scene.Contains("lowfid")) condition = "LowFid";
        else if (scene.Contains("highfidnas")) condition = "HighFidNAS";
        else if (scene.Contains("highfid")) condition = "HighFid";

        if (scene.Contains("simple")) complexity = "Simple";
        else if (scene.Contains("complex")) complexity = "Complex";
    }

    public void OnShapeTriggerEnter(GameObject shapeObject, GameObject trigger) {
        if (!RoleManager.Instance.IsStudent(realtime.clientID)) return;

        if (shapeObject.CompareTag("Shape") && trigger.CompareTag("Trigger")) {
            HandlePlacement(shapeObject, trigger);
        }
    }

    private void HandlePlacement(GameObject obj, GameObject trigger) {
        float timestamp = Time.time - startTime;

        if (obj.name == trigger.name) {
            placedObjects[obj.name] = true;
            audioSource?.PlayOneShot(correctSound);

            obj.GetComponent<RealtimeView>()?.RequestOwnership();
            obj.GetComponent<XRGrabInteractable>()?.interactionManager.CancelInteractableSelection(obj.GetComponent<XRGrabInteractable>());
            obj.transform.position = new Vector3(9999f, 9999f, 9999f);

            if (!disableLogging) AppendPlacementLog(obj.name, "Correct", timestamp);
            CheckCompletion();
        } else {
            errors++;
            audioSource?.PlayOneShot(errorSound);
            RespawnObject(obj);
            if (!disableLogging) AppendPlacementLog(obj.name, "Incorrect", timestamp);
        }
    }

    private void CheckCompletion() {
        foreach (bool placed in placedObjects.Values) {
            if (!placed) return;
        }

        float totalTime = Time.time - startTime;

        if (!disableLogging && logger != null) {
            logger.EndTrial();
        }

        if (RoleManager.Instance.IsStudent(realtime.clientID)) {
            if (!disableLogging) AppendFinalSummary(totalTime, errors);
            Invoke("ReturnToLobby", 2f);
        }
    }

    private void InitMetricsFile() {
        string filename = $"{dyadID}_{condition}_PerformanceMetrics.csv";
        metricsFilePath = Path.Combine(Application.persistentDataPath, filename);
        if (!File.Exists(metricsFilePath)) {
            File.WriteAllText(metricsFilePath, "DyadID,Condition,Complexity,ShapeName,PlacementResult,Timestamp,TotalTime,TotalErrors\n");
        }
    }

    private void AppendPlacementLog(string shapeName, string result, float timestamp) {
        string row = $"{dyadID},{condition},{complexity},{shapeName},{result},{timestamp:F2},,";
        File.AppendAllText(metricsFilePath, row + "\n");
    }

    private void AppendFinalSummary(float totalTime, int totalErrors) {
        string row = $"{dyadID},{condition},{complexity},,,,,{totalTime:F2},{totalErrors}";
        File.AppendAllText(metricsFilePath, row + "\n");
    }

    private void RespawnObject(GameObject obj) {
        if (objectSpawnPoints.TryGetValue(obj.name, out Vector3 spawn)) {
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
            grab?.interactionManager.CancelInteractableSelection(grab);

            obj.transform.position = spawn;
            obj.transform.rotation = objectRotations[obj.name];
        }
    }

    private bool AreAllShapesMovedAway() {
        foreach (GameObject shape in GameObject.FindGameObjectsWithTag("Shape")) {
            if (shape.transform.position.x < 9000f) return false;
        }
        return true;
    }

    private void CheckForFallenShapes() {
        float fallY = -1f;
        float ignoreX = 9000f;

        foreach (GameObject shape in GameObject.FindGameObjectsWithTag("Shape")) {
            if (shape.transform.position.x >= ignoreX) continue;
            if (shape.transform.position.y < fallY) RespawnObject(shape);
        }
    }

    private void ShuffleShapePositions() {
        GameObject[] shapes = GameObject.FindGameObjectsWithTag("Shape");

        List<Vector3> positions = new List<Vector3>();
        List<Quaternion> rotations = new List<Quaternion>();

        foreach (GameObject shape in shapes) {
            if (objectSpawnPoints.TryGetValue(shape.name, out Vector3 pos)) {
                positions.Add(pos);
                rotations.Add(objectRotations[shape.name]);
            }
        }

        for (int i = 0; i < positions.Count; i++) {
            int randomIndex = Random.Range(i, positions.Count);
            (positions[i], positions[randomIndex]) = (positions[randomIndex], positions[i]);
            (rotations[i], rotations[randomIndex]) = (rotations[randomIndex], rotations[i]);
        }

        for (int i = 0; i < shapes.Length; i++) {
            GameObject shape = shapes[i];
            Vector3 newPos = positions[i];
            Quaternion newRot = rotations[i];

            shape.GetComponent<RealtimeView>()?.RequestOwnership();
            shape.GetComponent<RealtimeTransform>()?.RequestOwnership();

            Rigidbody rb = shape.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.MovePosition(newPos);
                rb.MoveRotation(newRot);
            } else {
                shape.transform.position = newPos;
                shape.transform.rotation = newRot;
            }

            objectSpawnPoints[shape.name] = newPos;
            objectRotations[shape.name] = newRot;
        }
    }

    private void ReturnToLobby() {
        SceneManager.LoadScene("LobbyAvtrs");
    }
}






// using UnityEngine;
// using UnityEngine.XR.Interaction.Toolkit;
// using System.Collections.Generic;
// using System.Collections;
// using System.IO;
// using UnityEngine.SceneManagement;
// using Normal.Realtime;

// public class SortingTaskManager : MonoBehaviour {
//     private Dictionary<string, Vector3> objectSpawnPoints = new Dictionary<string, Vector3>();
//     private Dictionary<string, Quaternion> objectRotations = new Dictionary<string, Quaternion>();
//     private Dictionary<string, bool> placedObjects = new Dictionary<string, bool>();
//     private float startTime;
//     private int errors = 0;
//     private bool timeStarted = false;
//     private Realtime realtime;
//     private AudioSource audioSource;

//     private DataLogger logger;
//     private string dyadID = "UnknownDyad";
//     private string condition = "UnknownCondition";
//     private string complexity = "UnknownComplexity";

//     public AudioClip correctSound;
//     public AudioClip errorSound;

//     private bool hasShuffled = false;
//     private string metricsFilePath;

//     void Start() {
//         realtime = FindObjectOfType<Realtime>();

//         foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Shape")) {
//             objectSpawnPoints[obj.name] = obj.transform.position;
//             objectRotations[obj.name] = obj.transform.rotation;
//             placedObjects[obj.name] = false;
//         }

//         audioSource = gameObject.AddComponent<AudioSource>();
//         audioSource.playOnAwake = false;

//         ParseSceneName();
//     }

//     void Update() {
//         if (logger == null) {
//             DataLogger[] allLoggers = FindObjectsOfType<DataLogger>();
//             foreach (DataLogger log in allLoggers) {
//                 if (log.GetComponent<RealtimeView>().isOwnedLocallySelf) {
//                     logger = log;
//                     dyadID = log.avatarConfigData != null ? log.avatarConfigData.dyadID : "UnknownDyad";
//                     break;
//                 }
//             }
//         }

//         if (!timeStarted && logger != null &&
//             (RoleManager.Instance.GetTeacherID() != 0 || RoleManager.Instance.GetStudentID() != 0)) {

//             if (!hasShuffled && RoleManager.Instance.IsStudent(realtime.clientID)) {
//                 ShuffleShapePositions();
//                 hasShuffled = true;
//             }

//             if (RoleManager.Instance.IsStudent(realtime.clientID)) {
//                 startTime = Time.time;
//                 InitMetricsFile();
//             }

//             logger.BeginTrial();
//             timeStarted = true;
//         }

//         if (RoleManager.Instance.IsStudent(realtime.clientID)) {
//             CheckForFallenShapes();
//         }

//         if (RoleManager.Instance.IsTeacher(realtime.clientID) && AreAllShapesMovedAway()) {
//             logger.EndTrial();
//             Invoke("ReturnToLobby", 2f);
//         }
//     }

//     private void ParseSceneName() {
//         string scene = SceneManager.GetActiveScene().name.ToLower();
//         if (scene.Contains("lowfid")) condition = "LowFid";
//         else if (scene.Contains("highfidnas")) condition = "HighFidNAS";
//         else if (scene.Contains("highfid")) condition = "HighFid";

//         if (scene.Contains("simple")) complexity = "Simple";
//         else if (scene.Contains("complex")) complexity = "Complex";
//     }

//     public void OnShapeTriggerEnter(GameObject shapeObject, GameObject trigger) {
//         if (!RoleManager.Instance.IsStudent(realtime.clientID)) return;

//         if (shapeObject.CompareTag("Shape") && trigger.CompareTag("Trigger")) {
//             HandlePlacement(shapeObject, trigger);
//         }
//     }

//     private void HandlePlacement(GameObject obj, GameObject trigger) {
//         float timestamp = Time.time - startTime;
//         if (obj.name == trigger.name) {
//             placedObjects[obj.name] = true;
//             audioSource?.PlayOneShot(correctSound);

//             if (obj.GetComponent<RealtimeView>() != null)
//                 obj.GetComponent<RealtimeView>().RequestOwnership();

//             XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
//             if (grab != null)
//                 grab.interactionManager.CancelInteractableSelection(grab);

//             obj.transform.position = new Vector3(9999f, 9999f, 9999f);

//             AppendPlacementLog(obj.name, "Correct", timestamp);
//             CheckCompletion();
//         } else {
//             errors++;
//             audioSource?.PlayOneShot(errorSound);
//             RespawnObject(obj);
//             AppendPlacementLog(obj.name, "Incorrect", timestamp);
//         }
//     }

//     private void CheckCompletion() {
//         foreach (bool placed in placedObjects.Values) {
//             if (!placed) return;
//         }

//         float totalTime = Time.time - startTime;
//         logger.EndTrial();

//         if (RoleManager.Instance.IsStudent(realtime.clientID)) {
//             AppendFinalSummary(totalTime, errors);
//             Invoke("ReturnToLobby", 2f);
//         }
//     }

//     private void InitMetricsFile() {
//         string filename = $"{dyadID}_{condition}_PerformanceMetrics.csv";
//         metricsFilePath = Path.Combine(Application.persistentDataPath, filename);
//         if (!File.Exists(metricsFilePath)) {
//             File.WriteAllText(metricsFilePath, "DyadID,Condition,Complexity,ShapeName,PlacementResult,Timestamp,TotalTime,TotalErrors\n");
//         }
//     }

//     private void AppendPlacementLog(string shapeName, string result, float timestamp) {
//         string row = $"{dyadID},{condition},{complexity},{shapeName},{result},{timestamp:F2},,";
//         File.AppendAllText(metricsFilePath, row + "\n");
//     }

//     private void AppendFinalSummary(float totalTime, int totalErrors) {
//         string row = $"{dyadID},{condition},{complexity},,,,,{totalTime:F2},{totalErrors}";
//         File.AppendAllText(metricsFilePath, row + "\n");
//     }

//     private void RespawnObject(GameObject obj) {
//         if (objectSpawnPoints.TryGetValue(obj.name, out Vector3 spawn)) {
//             Rigidbody rb = obj.GetComponent<Rigidbody>();
//             if (rb != null) {
//                 rb.velocity = Vector3.zero;
//                 rb.angularVelocity = Vector3.zero;
//             }

//             XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
//             if (grab != null)
//                 grab.interactionManager.CancelInteractableSelection(grab);

//             obj.transform.position = spawn;
//             obj.transform.rotation = objectRotations[obj.name];
//         }
//     }

//     private bool AreAllShapesMovedAway() {
//         foreach (GameObject shape in GameObject.FindGameObjectsWithTag("Shape")) {
//             if (shape.transform.position.x < 9000f) return false;
//         }
//         return true;
//     }

//     private void CheckForFallenShapes() {
//         float fallY = -1f;
//         float ignoreX = 9000f;

//         foreach (GameObject shape in GameObject.FindGameObjectsWithTag("Shape")) {
//             if (shape.transform.position.x >= ignoreX) continue;
//             if (shape.transform.position.y < fallY) RespawnObject(shape);
//         }
//     }

//     private void ShuffleShapePositions() {
//         GameObject[] shapes = GameObject.FindGameObjectsWithTag("Shape");

//         List<Vector3> positions = new List<Vector3>();
//         List<Quaternion> rotations = new List<Quaternion>();

//         foreach (GameObject shape in shapes) {
//             if (objectSpawnPoints.TryGetValue(shape.name, out Vector3 pos)) {
//                 positions.Add(pos);
//                 rotations.Add(objectRotations[shape.name]);
//             }
//         }

//         for (int i = 0; i < positions.Count; i++) {
//             int randomIndex = Random.Range(i, positions.Count);
//             (positions[i], positions[randomIndex]) = (positions[randomIndex], positions[i]);
//             (rotations[i], rotations[randomIndex]) = (rotations[randomIndex], rotations[i]);
//         }

//         for (int i = 0; i < shapes.Length; i++) {
//             GameObject shape = shapes[i];
//             Vector3 newPos = positions[i];
//             Quaternion newRot = rotations[i];

//             RealtimeView view = shape.GetComponent<RealtimeView>();
//             RealtimeTransform rtransform = shape.GetComponent<RealtimeTransform>();
//             if (view != null && rtransform != null) {
//                 view.RequestOwnership();
//                 rtransform.RequestOwnership();
//             }

//             Rigidbody rb = shape.GetComponent<Rigidbody>();
//             if (rb != null) {
//                 rb.velocity = Vector3.zero;
//                 rb.angularVelocity = Vector3.zero;
//                 rb.MovePosition(newPos);
//                 rb.MoveRotation(newRot);
//             } else {
//                 shape.transform.position = newPos;
//                 shape.transform.rotation = newRot;
//             }

//             objectSpawnPoints[shape.name] = newPos;
//             objectRotations[shape.name] = newRot;
//         }
//     }

//     private void ReturnToLobby() {
//         SceneManager.LoadScene("LobbyAvtrs");
//     }
// }


