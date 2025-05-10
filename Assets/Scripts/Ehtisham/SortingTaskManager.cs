using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class SortingTaskManager : MonoBehaviour {
    private Dictionary<string, Vector3> objectSpawnPoints = new Dictionary<string, Vector3>();
    private Dictionary<string, Quaternion> objectRotations = new Dictionary<string, Quaternion>();
    private Dictionary<string, bool> placedObjects = new Dictionary<string, bool>();
    private float startTime;
    private int errors = 0;
    private string baseFileName;
    private Realtime realtime;
    private bool timeStarted = false; // ✅ Ensure we only start time once

    public AudioClip correctSound;
    public AudioClip errorSound;
    private AudioSource audioSource;

    void Start() {
        realtime = FindObjectOfType<Realtime>();
        if (realtime == null) {
            Debug.LogError("❌ Realtime component not found!");
            return;
        }

        baseFileName = SceneManager.GetActiveScene().name.Contains("HighFid")
            ? "highFid_exp_data"
            : "lowFid_exp_data";

        foreach (GameObject obj in GameObject.FindGameObjectsWithTag("Shape")) {
            objectSpawnPoints[obj.name] = obj.transform.position;
            objectRotations[obj.name] = obj.transform.rotation;
            placedObjects[obj.name] = false;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    void Update() {
        //  Ensure roles are assigned before setting the start time
        if (!timeStarted && (RoleManager.Instance.GetTeacherID() != 0 || RoleManager.Instance.GetStudentID() != 0)) {
            if (RoleManager.Instance.IsStudent(realtime.clientID)) {
                startTime = Time.time;
                timeStarted = true; //  Ensure it runs only once
            }
        }

        // Check if any shape has fallen too low and reset it
        if (RoleManager.Instance.IsStudent(realtime.clientID)) {
            CheckForFallenShapes();
        }


        // Teacher checks if all shapes have moved far away
        if (RoleManager.Instance.IsTeacher(realtime.clientID) && AreAllShapesMovedAway()) {
            Invoke("ReturnToLobby", 2f);
        }
    }

    public void OnShapeTriggerEnter(GameObject shapeObject, GameObject trigger) {
        if (!RoleManager.Instance.IsStudent(realtime.clientID)) return; // ✅ Only Student Handles Placement

        if (shapeObject.CompareTag("Shape") && trigger.CompareTag("Trigger")) {
            HandlePlacement(shapeObject, trigger);
        }
    }

    private void HandlePlacement(GameObject obj, GameObject trigger) {
        string objectName = obj.name;
        string triggerName = trigger.name;

        if (objectName == triggerName) {
            placedObjects[objectName] = true;

            //play feedback sound
            if (correctSound != null && audioSource != null) {
                audioSource.PlayOneShot(correctSound);
            }

            // ✅ Ensure ownership before modifying
            if (obj.GetComponent<RealtimeView>() != null) {
                obj.GetComponent<RealtimeView>().RequestOwnership();
            }

            // ✅ Force release to prevent it from being stuck in hand
            XRGrabInteractable grabInteractable = obj.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null) {
                grabInteractable.interactionManager.CancelInteractableSelection(grabInteractable);
            }

            // ✅ Instead of disabling, move it far away so it's invisible
            obj.transform.position = new Vector3(9999f, 9999f, 9999f);

            CheckCompletion();
        } else {
            errors++;
            if (errorSound != null && audioSource != null) {
                audioSource.PlayOneShot(errorSound);
            }
            RespawnObject(obj);
        }
    }

    private void RespawnObject(GameObject obj) {
        if (objectSpawnPoints.TryGetValue(obj.name, out Vector3 spawnPoint)) {
            obj.GetComponent<Rigidbody>().velocity = Vector3.zero; // ✅ Stop movement
            obj.GetComponent<Rigidbody>().angularVelocity = Vector3.zero; // ✅ Stop rotation

            XRGrabInteractable grabInteractable = obj.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null) {
                grabInteractable.interactionManager.CancelInteractableSelection(grabInteractable); // ✅ Force release from hand
            }

            obj.transform.position = spawnPoint;
            obj.transform.rotation = objectRotations[obj.name];
        } else {
            Debug.LogError($"❌ Spawn point not found for {obj.name}");
        }
    }

    private void CheckCompletion() {
        foreach (bool placed in placedObjects.Values) {
            if (!placed) return;
        }

        float totalTime = Time.time - startTime;
        Debug.Log($"🎉 Task Completed! Time: {totalTime} seconds, Errors: {errors}");

        // ✅ **Only Student Saves Data and Triggers Scene Change**
        if (RoleManager.Instance.IsStudent(realtime.clientID)) {
            SaveExperimentData(totalTime, errors);
            Invoke("ReturnToLobby", 2f);
        }
    }

    private void SaveExperimentData(float time, int errors) {
        string fileExtension = ".txt";
        string directoryPath = Application.persistentDataPath;
        int fileIndex = 0;
        string filePath = Path.Combine(directoryPath, baseFileName + fileExtension);

        while (File.Exists(filePath)) {
            fileIndex++;
            filePath = Path.Combine(directoryPath, $"{baseFileName}_{fileIndex}{fileExtension}");
        }

        string data = $"Time: {time}s, Errors: {errors}\n";

        try {
            File.WriteAllText(filePath, data);
            Debug.Log($"✅ Experiment data saved at: {filePath}");
        } catch (System.Exception e) {
            Debug.LogError($"❌ Failed to save experiment data: {e.Message}");
        }

        PlayerPrefs.SetFloat("LastTaskTime", time);
        PlayerPrefs.SetInt("LastTaskErrors", errors);
        PlayerPrefs.Save();
    }

    private bool AreAllShapesMovedAway() {
        GameObject[] shapes = GameObject.FindGameObjectsWithTag("Shape");
        
        foreach (GameObject shape in shapes) {
            if (shape.transform.position.x < 9000f) { // ✅ If any shape is NOT far away, return false
                return false;
            }
        }
        
        return true; // ✅ If all shapes are far away, return true
    }

    private void CheckForFallenShapes() {
        float fallThreshold = -0.8f; // ✅ Set floor height (adjust based on your scene)
        float farAwayThreshold = 9000f; // ✅ Ignore objects placed far away

        GameObject[] shapes = GameObject.FindGameObjectsWithTag("Shape");

        foreach (GameObject shape in shapes) {
            // ✅ Ignore objects that were intentionally moved far away
            if (shape.transform.position.x > farAwayThreshold) continue;

            // ✅ If object falls too low, respawn it
            if (shape.transform.position.y < fallThreshold) {
                RespawnObject(shape);
            }
        }
    }




    private void ReturnToLobby() {
        SceneManager.LoadScene("LobbyAvtrs");
    }


}