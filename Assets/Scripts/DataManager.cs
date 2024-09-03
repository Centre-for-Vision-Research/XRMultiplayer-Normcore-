using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using static EnvObject;

/// <summary>
/// DataManager, used to monitor and store data from: <br/>
/// Player Avatars, GameObjects in the Scene and Experiment data entries
/// </summary>
public class DataManager : MonoBehaviour
{
    /// <summary>
    /// Gameobjects in the scene to track or not
    /// </summary>
    [SerializeField]
    HashSet<GameObject> _toTrack = new HashSet<GameObject>();
    /// <summary>
    /// Whether or not we can track the player hands, head, etc. in the scene
    /// </summary>
    [SerializeField]
    private bool _canTrackPlayer;
    /// <summary>
    /// Whether or not we can track "EnvObjects" in the scene
    /// </summary>
    [SerializeField]
    private bool _canTrackObjects;
    /// <summary>
    /// How often should data be read to the output files in seconds when specified
    /// </summary>
    [SerializeField]
    private float _timeInterval = 0.1f;

    //heading for .csv files
    private readonly string OBJECTS_HEADING = "Object,Owner,Time,XPos,Ypos,ZPos,XRot,YRot,ZRot,XScale,YScale,ZScale,Status,Trial\n";
    private readonly string PLAYER_HEADING = "Bone,Time,XPos,YPos,ZPos,XRot,YRot,ZRot\n";
    private readonly string EXP_HEADING = "Trial,Shape,Size,Response,ResponseTime,Time";

    private float timeCounter = 0;

    //all file paths
    private string EXP_FILE_PATH = "";
    private Dictionary<int, string> PLAYER_FILE_PATH;
    private string OBJECT_FILE_PATH = "";
    //temp files, StringBuilders
    private static StringBuilder EXP_FILE_TEMP;
    private static Dictionary<int, StringBuilder> PLAYER_FILE_TEMP;
    private static StringBuilder OBJECT_FILE_TEMP;
    //flags
    private bool EXP_FILE_READY = false;
    //private static Dictionary<int, bool> PLAYER_FILE_READY;
    private bool OBJECTS_FILE_READY = false;
    private bool _updatingTrackedObjects = false;
    private static readonly string SEPARATOR = ",";

    private ExpController expController;

    // Start is called before the first frame update
    void Start()
    {
        expController = FindObjectOfType<ExpController>();
        //create a file for the trial;
        CreateObjectsFile();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if(timeCounter >= _timeInterval) {
            UpdateObjectFile();

            timeCounter = 0;
        }

        timeCounter += Time.deltaTime;
    }

    /// <summary>
    /// Add GameObject to list of objects tracked by DataManager
    /// </summary>
    /// <param name="g"></param>
    public void AddObjectTrack(GameObject g)
    {
        _updatingTrackedObjects = true;
        _toTrack.Add(g);
        _updatingTrackedObjects = false;
    }

    /// <summary>
    /// Remove GameObject from list of objects tracked by DataManager
    /// </summary>
    /// <param name="g"></param>
    public void RemoveObjectTrack(GameObject g)
    {
        _updatingTrackedObjects = true;
        _toTrack.Remove(g);
        _updatingTrackedObjects = false;
    }

    /// <summary>
    /// Create .csv file to store data for tracked GameObjects
    /// </summary>
    private void CreateObjectsFile()
    {
        if (!_canTrackObjects) return;

        if (OBJECTS_FILE_READY) return;

        OBJECT_FILE_PATH = $"{Application.persistentDataPath}/objectsData_{System.DateTime.Now.ToString("yyyy-MM-dd-HH_mm_ss")}.csv";//something to identify the participant

        OBJECT_FILE_TEMP = new StringBuilder();
        OBJECT_FILE_TEMP.AppendLine(string.Join(SEPARATOR, OBJECTS_HEADING));
        //File.WriteAllText(OBJECT_FILE_PATH, _objectHeader);

        //Debug.Log($"Object File Path is: {OBJECT_FILE_PATH}");

        OBJECTS_FILE_READY = true;
        SaveObjectsFile();
    }

    /// <summary>
    /// Update data about tracked GameObjects
    /// </summary>
    public void UpdateObjectFile()
    {
        if (!_canTrackObjects) return;

        if (!OBJECTS_FILE_READY) CreateObjectsFile();

        if (_toTrack.Count <= 0) return;

        if (_updatingTrackedObjects) return;
        int ownerID = -1;
        StatusWRTBox status = StatusWRTBox.OutsideBox;
        int trial = -1;

        foreach (GameObject track in _toTrack)
        {
            if(track.TryGetComponent<EnvObject>(out EnvObject eO))
            {
                ownerID = eO.GetOwnerID();
                status = eO.GetStatus();
            }
            if (expController)
            {
                trial = expController.GetCurrTrialNumber();
            }
            string update = $"{track.name},{ownerID},{DateTime.Now.TimeOfDay}," +
        $"{track.transform.position.x},{track.transform.position.y},{track.transform.position.z}," +
        $"{track.transform.eulerAngles.x},{track.transform.eulerAngles.y},{track.transform.eulerAngles.z}," +
        $"{track.transform.lossyScale.x},{track.transform.lossyScale.y},{track.transform.lossyScale.z}," +
        $"{status},{trial}";
            OBJECT_FILE_TEMP.AppendLine(string.Join(SEPARATOR, update));
            //Debug.Log($"Appending {update}");
        }
    }

    public bool ObjectFileExists()
    {
        return OBJECT_FILE_PATH != "";
    }

    public string GetObjectFilePath()
    {
        return OBJECT_FILE_PATH;
    }

    /// <summary>
    /// Call to save the file containing information about objects in the room <br/>
    /// NOTE: ALL FILES MONITORED BY THE DATAMANAGER ARE ALREADY SET TO SAVE WHEN THE APPLICATION QUITS OR PAUSES
    /// </summary>
    private void SaveObjectsFile()
    {
        if (!_canTrackObjects) return;

        if (!OBJECTS_FILE_READY) return;

        if (_toTrack.Count <= 0) return;

        if(OBJECT_FILE_TEMP == null) return;

        try
        {
            File.AppendAllText(OBJECT_FILE_PATH, OBJECT_FILE_TEMP.ToString());
            OBJECT_FILE_TEMP.Clear();
            //Debug.Log($"Objects file saved to {OBJECT_FILE_PATH}");
        }
        catch (Exception e)
        {
            //Debug.Log($"Objects data could not be written to csv file due to exception: {e}");
            return;
        }
    }

    /// <summary>
    /// Create a file to store skeleton data for player ID "pID" <br/>
    /// Does nothing if file already exists
    /// </summary>
    public void CreatePlayerFile(int pID)
    {
        if (!_canTrackPlayer) return;
        if (PlayerFileExists(pID) || pID < 0) return;
        if (PLAYER_FILE_PATH == null) PLAYER_FILE_PATH = new Dictionary<int, string>();
        if (PLAYER_FILE_TEMP == null) PLAYER_FILE_TEMP = new Dictionary<int, StringBuilder>();

        string playerFilePath = $"{Application.persistentDataPath}/p{pID}_skeletonData_{System.DateTime.Now.ToString("yyyy-MM-dd-HH_mm_ss")}.csv";

        PLAYER_FILE_TEMP.Add(pID, new StringBuilder());
        PLAYER_FILE_TEMP[pID].AppendLine(string.Join(SEPARATOR, PLAYER_HEADING));

        PLAYER_FILE_PATH.Add(pID, playerFilePath);
        Debug.Log($"Player {pID} file created");
        SavePlayerFile(pID);
    }

    /// <summary>
    /// Updates the player file based on the specified player ID "pID"
    /// </summary>
    /// <param name="pID"></param>
    /// <param name="transform"></param>
    public void UpdatePlayerFile(int pID, Transform transform)
    {
        if (!CanUpdatePlayerFile(pID)) return;

        string update = $"{transform.name},{DateTime.Now.TimeOfDay}," +
            $"{transform.position.x},{transform.position.y},{transform.position.z}," +
            $"{transform.eulerAngles.x},{transform.eulerAngles.y},{transform.eulerAngles.z}";
        //Debug.Log($"updating player file....{update}");
        PLAYER_FILE_TEMP[pID].AppendLine(string.Join(SEPARATOR, update));
    }

    /// <summary>
    /// Updates the player file based on the specified player ID "pID"
    /// </summary>
    /// <param name="pID"></param>
    /// <param name="name"></param>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    public void UpdatePlayerFile(int pID, string name, Vector3 pos, Vector3 rot)
    {
        if (!CanUpdatePlayerFile(pID)) return;

        string update = $"{name},{DateTime.Now.TimeOfDay}," +
            $"{pos.x},{pos.y},{pos.z}," +
            $"{rot.x},{rot.y},{rot.z}";
        Debug.Log($"updating player file....{update}");
        PLAYER_FILE_TEMP[pID].AppendLine(string.Join(SEPARATOR, update));
    }

    /// <summary>
    /// Updates player file based on the specified player ID "pID"
    /// </summary>
    /// <param name="pID"></param>
    /// <param name="pose"></param>
    /// <param name="name"></param>
    public void UpdatePlayerFile(int pID, Pose pose, string name)
    {
        if (!CanUpdatePlayerFile(pID)) return;

        string update = $"{name},{DateTime.Now.TimeOfDay}," +
            $"{pose.position.x},{pose.position.y},{pose.position.z}," +
            $"{pose.rotation.eulerAngles.x},{pose.rotation.eulerAngles.y},{pose.rotation.eulerAngles.z}";
        Debug.Log($"updating player file....{update}");
        PLAYER_FILE_TEMP[pID].AppendLine(string.Join(SEPARATOR, update));
    }

    /// <summary>
    /// Call to save the file related to a specific player "pID" <br/>
    /// NOTE: ALL FILES MONITORED BY THE DATAMANAGER ARE ALREADY SET TO SAVE WHEN THE APPLICATION QUITS OR PAUSES
    /// </summary>
    /// <param name="pID"></param>
    public void SavePlayerFile(int pID)
    {
        if (!CanUpdatePlayerFile(pID)) return;

        try
        {
            File.AppendAllText(PLAYER_FILE_PATH[pID], PLAYER_FILE_TEMP[pID].ToString());
            PLAYER_FILE_TEMP[pID].Clear();
            Debug.Log($"Player {pID} file saved to {PLAYER_FILE_PATH[pID]}");
        }
        catch (Exception e)
        {
            Debug.Log($"Player {pID} data could not be written to file due to exception: {e}");
            return;
        }
    }

    /// <summary>
    /// Save the file for all the player's in the scene
    /// </summary>
    private void SaveAllPlayerFiles()
    {
        Debug.Log($"Saving all player files....");
        if (PLAYER_FILE_PATH == null) return;
        foreach (int pID in PLAYER_FILE_PATH.Keys)
            SavePlayerFile(pID);
    }

    private bool CanUpdatePlayerFile(int pID = -1)
    {
        if (!_canTrackPlayer) return false;

        //only ID 0 and up is valid
        if (pID < 0) return false;

        if (PLAYER_FILE_PATH == null || !PLAYER_FILE_PATH.ContainsKey(pID)) return false;

        if (PLAYER_FILE_TEMP == null || !PLAYER_FILE_TEMP.ContainsKey(pID)) return false;

        return true;
    }

    public bool PlayerFileExists(int pID)
    {
        return PLAYER_FILE_PATH != null && PLAYER_FILE_PATH.ContainsKey(pID);
    }

    public string GetPlayerFilePath(int pID)
    {
        return PLAYER_FILE_PATH[pID];
    }

    /// <summary>
    /// Create file to store experiment data
    /// </summary>
    public void CreateExpFile()
    {
        if(EXP_FILE_READY)
        {
            //Debug.LogError("Exp File already created");
            return;
        }

        EXP_FILE_PATH = $"{Application.persistentDataPath}/ExpEntry_{System.DateTime.Now:yyyy-MM-dd-HH_mm_ss}.csv";
        //Debug.Log($"EXP FILE PATH IS: {EXP_FILE_PATH}");

        EXP_FILE_TEMP = new StringBuilder();
        EXP_FILE_TEMP.AppendLine(string.Join(SEPARATOR, EXP_HEADING));
        SaveExpFile();
        EXP_FILE_READY = true;
    }

    /// <summary>
    /// Create file to store experiment data, adding extraInfo to the name of the file
    /// </summary>
    /// <param name="extraInfo"></param>
    public void CreateExpFile(string extraInfo)
    {
        if (EXP_FILE_READY)
        {
            //Debug.LogError("Exp File already created");
            return;
        }

        EXP_FILE_PATH = $"{Application.persistentDataPath}/ExpEntry_{extraInfo}_{System.DateTime.Now:yyyy-MM-dd-HH_mm_ss}.csv";
        //Debug.Log($"EXP FILE PATH IS: {EXP_FILE_PATH}");

        EXP_FILE_TEMP = new StringBuilder();
        EXP_FILE_TEMP.AppendLine(string.Join(SEPARATOR, EXP_HEADING));
        SaveExpFile();
        EXP_FILE_READY = true;
    }

    /// <summary>
    /// Update the player file with the given inputs
    /// </summary>
    /// <param name="trial"></param>
    /// <param name="shape"></param>
    /// <param name="size"></param>
    /// <param name="response"></param>
    /// <param name="responseTime"></param>
    /// <param name="time"></param>
    public void UpdateExpFile(int trial, string shape, float size, string response, string responseTime, string time)
    {
        if (!EXP_FILE_READY) return;

        string entry = $"{trial},{shape}, {size}, {response}, {responseTime}, {time}";
        //Debug.Log($"Updating Entry: {entry}");
        EXP_FILE_TEMP.AppendLine(string.Join(SEPARATOR, entry));
    }

    public string GetExpFilePath()
    {
        return EXP_FILE_PATH;
    }

    /// <summary>
    /// Call to save the experiment file <br/>
    /// NOTE: ALL FILES MONITORED BY THE DATAMANAGER ARE ALREADY SET TO SAVE WHEN THE APPLICATION QUITS OR PAUSES
    /// </summary>
    public void SaveExpFile()
    {
        if (!EXP_FILE_READY) return;
        try
        {
            File.AppendAllText(EXP_FILE_PATH, EXP_FILE_TEMP.ToString());
            EXP_FILE_TEMP.Clear();
            //Debug.Log($"Exp file saved to {EXP_FILE_PATH}");
        }
        catch (Exception e)
        {
            //Debug.Log($"Exp data could not be written to file due to exception: {e}");
            return;
        }
    }

    /// <summary>
    /// Call to save all the files monitored by the DataManager <br/>
    /// NOTE: ALL FILES MONITORED BY THE DATAMANAGER ARE ALREADY SET TO SAVE WHEN THE APPLICATION QUITS OR PAUSES
    /// </summary>
    public void SaveAllFiles()
    {
        SaveExpFile();
        SaveObjectsFile();
        SaveAllPlayerFiles();
    }
    
    private void OnApplicationPause(bool pause)
    {
        SaveAllFiles();
    }

    private void OnApplicationQuit()
    {
        SaveAllFiles();
    }

    private void OnDisable()
    {
        SaveAllFiles();
    }

    private void OnDestroy()
    {
        SaveAllFiles();
    }
}