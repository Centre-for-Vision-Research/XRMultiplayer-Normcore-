using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class WhackAMoleTaskManager : MonoBehaviour {
    public static WhackAMoleTaskManager Instance { get; private set; }

    [Header("Block Settings")]
    public float blockDurationSeconds = 120f;
    public bool autoReturnToLobby = true;
    public string lobbySceneName = "LobbyAvtrs";

    [Header("Logging")]
    [SerializeField] private bool disableLogging = false;

    private bool firstHitOccurred = false;
    private bool blockRunning = false;
    private float blockStartTime = 0f;

    private bool isHost = true;
    private bool isLoggingOwner = false;

    private WhackAMoleSpawner spawner;
    private Realtime realtime;
    private DataLogger localLogger;

    private string dyadID = "UnknownDyad";
    private string condition = "UnknownCondition";

    private int totalMolesSpawned = 0;
    private int totalMisses = 0;
    private int[] hitsPerPlayer = new int[2];

    private string behaviorFilePath;
    private string performanceFilePath;

    private class MoleEventInfo {
        public WhackHole hole;
        public float popTime;
        public bool wasHit;
    }

    private Dictionary<MoleController, MoleEventInfo> activeMoles =
        new Dictionary<MoleController, MoleEventInfo>();

    public bool IsHost() => isHost;

    // --------------------------------------------------------------
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        spawner = FindObjectOfType<WhackAMoleSpawner>();
        realtime = FindObjectOfType<Realtime>();

        if (RoleManager.Instance != null && realtime != null) {
            isHost = RoleManager.Instance.IsTeacher(realtime.clientID);
            isLoggingOwner = isHost;
        } else {
            isHost = true;
            isLoggingOwner = true;
        }

        ParseSceneName();
    }

    private void Start() {
        // 🔥 Do NOT spawn immediately → wait for logger creation
        StartCoroutine(WaitForLoggerBeforeStart());
    }

    private void Update() {
        if (!disableLogging && localLogger == null)
            TryFindLocalLogger();

        if (!isHost) return;

        if (blockRunning && Time.time - blockStartTime >= blockDurationSeconds)
            EndBlock();
    }

    // --------------------------------------------------------------
    // LOGGER WAIT + SPAWN
    // --------------------------------------------------------------
    private IEnumerator WaitForLoggerBeforeStart() {
        int attempts = 0;

        while (!disableLogging && isLoggingOwner && localLogger == null && attempts < 120) {
            TryFindLocalLogger();
            attempts++;
            yield return null;
        }

        if (!disableLogging && isLoggingOwner && localLogger != null)
            InitLogFiles();

        if (isHost)
            StartCoroutine(SpawnInitialMoleDelayed());
    }

    // --------------------------------------------------------------
    private void TryInitLoggerAndFiles() {
        if (localLogger == null)
            TryFindLocalLogger();
        else
            InitLogFiles();
    }

    private void TryFindLocalLogger() {
        foreach (var log in FindObjectsOfType<DataLogger>()) {
            RealtimeView view = log.GetComponent<RealtimeView>();
            if (view != null && view.isOwnedLocallySelf) {
                localLogger = log;

                if (log.avatarConfigData != null)
                    dyadID = log.avatarConfigData.dyadID;

                break;
            }
        }
    }

    private void InitLogFiles() {
        if (disableLogging || !isLoggingOwner || localLogger == null) return;

        string dir = Path.Combine(Application.persistentDataPath, "WhackAMole");
        Directory.CreateDirectory(dir);

        string roleTag = GetLocalRoleTag();

        behaviorFilePath = Path.Combine(dir,
            $"{dyadID}_{condition}_{roleTag}_WhackAMole_Behavior.csv");

        performanceFilePath = Path.Combine(dir,
            $"{dyadID}_{condition}_{roleTag}_WhackAMole_PerformanceMetrics.csv");

        if (!File.Exists(behaviorFilePath)) {
            File.WriteAllText(behaviorFilePath,
                "DyadID,Condition,Role,Timestamp,EventType,PlayerID,MoleID," +
                "HoleName,PopTime,HitTime,ReactionTime,WasHit\n");
        }

        if (!File.Exists(performanceFilePath)) {
            File.WriteAllText(performanceFilePath,
                "DyadID,Condition,Role,BlockDuration,TotalMolesSpawned," +
                "HitsA,HitsB,TotalHits,TotalMisses\n");
        }
    }

    private string GetLocalRoleTag() {
        if (RoleManager.Instance == null || realtime == null) return "Unknown";

        if (RoleManager.Instance.IsTeacher(realtime.clientID)) return "ParticipantA";
        if (RoleManager.Instance.IsStudent(realtime.clientID)) return "ParticipantB";

        return "Unknown";
    }

    private void ParseSceneName() {
        string scene = SceneManager.GetActiveScene().name.ToLower();

        if (scene.Contains("lowfid")) condition = "LowFid";
        else if (scene.Contains("highfidnas")) condition = "HighFidNAS";
        else if (scene.Contains("highfid")) condition = "HighFid";
        else condition = "UnknownCondition";
    }

    // --------------------------------------------------------------
    private IEnumerator SpawnInitialMoleDelayed() {
        yield return null;

        WhackHole[] holes = FindObjectsOfType<WhackHole>();
        if (holes.Length == 0) {
            Debug.LogError("No WhackHole in scene!");
            yield break;
        }

        foreach (var h in holes)
            if (h.mole != null)
                h.mole.HideImmediate();

        WhackHole chosen = holes[Random.Range(0, holes.Length)];

        chosen.mole.ShowImmediate();
        RegisterSpawn(chosen);
    }

    // --------------------------------------------------------------
    public void RegisterSpawn(WhackHole hole) {
        if (!isHost) return;

        totalMolesSpawned++;

        var info = new MoleEventInfo {
            hole = hole,
            popTime = Time.time,
            wasHit = false
        };

        activeMoles[hole.mole] = info;

        LogSpawn(hole, hole.mole, info.popTime);
    }

    public void OnMoleHit(MoleController mole, int playerId) {
        if (!isHost || mole == null) return;

        bool success = mole.Hit();
        if (!success) return;

        if (!firstHitOccurred) {
            firstHitOccurred = true;
            blockRunning = true;
            blockStartTime = Time.time;

            if (!disableLogging && localLogger != null)
                localLogger.BeginTrial();

            spawner.StartGenerating();
        }

        if (playerId >= 0 && playerId < hitsPerPlayer.Length)
            hitsPerPlayer[playerId]++;

        MoleEventInfo info;
        if (!activeMoles.TryGetValue(mole, out info)) {
            info = new MoleEventInfo { hole = null, popTime = Time.time };
        } else {
            activeMoles.Remove(mole);
        }

        float hitTime = Time.time;
        float reaction = hitTime - info.popTime;

        LogHit(info.hole, mole, playerId, info.popTime, hitTime, reaction);
    }

    public void OnMoleMissed(MoleController mole) {
        if (!isHost || mole == null) return;

        MoleEventInfo info;
        if (!activeMoles.TryGetValue(mole, out info))
            return;

        activeMoles.Remove(mole);
        totalMisses++;

        LogMiss(info.hole, mole, info.popTime);
    }

    // --------------------------------------------------------------
    private void EndBlock() {
        if (!isHost) return;

        blockRunning = false;
        spawner.StopGenerating();

        if (!disableLogging && localLogger != null)
            localLogger.EndTrial();

        int totalHits = hitsPerPlayer[0] + hitsPerPlayer[1];

        if (!disableLogging && isLoggingOwner && !string.IsNullOrEmpty(performanceFilePath)) {
            string row =
                $"{dyadID},{condition},{GetLocalRoleTag()},{blockDurationSeconds}," +
                $"{totalMolesSpawned},{hitsPerPlayer[0]},{hitsPerPlayer[1]}," +
                $"{totalHits},{totalMisses}\n";

            File.AppendAllText(performanceFilePath, row);
        }

        if (autoReturnToLobby)
            SceneManager.LoadScene(lobbySceneName);
    }

    // --------------------------------------------------------------
    // SAFE LOGGING HELPERS
    // --------------------------------------------------------------
    private void LogSpawn(WhackHole hole, MoleController m, float pop) {
        if (disableLogging || !isLoggingOwner || string.IsNullOrEmpty(behaviorFilePath))
            return;

        string row =
            $"{dyadID},{condition},{GetLocalRoleTag()},{Time.time:F3}," +
            $"Spawn,-1,{m.GetInstanceID()},{hole.name},{pop},,,-1\n";

        File.AppendAllText(behaviorFilePath, row);
    }

    private void LogHit(WhackHole hole, MoleController m, int playerId,
                        float pop, float hit, float reaction) {

        if (disableLogging || !isLoggingOwner || string.IsNullOrEmpty(behaviorFilePath))
            return;

        string hName = hole != null ? hole.name : "None";

        string row =
            $"{dyadID},{condition},{GetLocalRoleTag()},{Time.time:F3}," +
            $"Hit,{playerId},{m.GetInstanceID()},{hName}," +
            $"{pop},{hit},{reaction},1\n";

        File.AppendAllText(behaviorFilePath, row);
    }

    private void LogMiss(WhackHole hole, MoleController m, float pop) {
        if (disableLogging || !isLoggingOwner || string.IsNullOrEmpty(behaviorFilePath))
            return;

        string hName = hole != null ? hole.name : "None";

        string row =
            $"{dyadID},{condition},{GetLocalRoleTag()},{Time.time:F3}," +
            $"Miss,-1,{m.GetInstanceID()},{hName},{pop},,,-1\n";

        File.AppendAllText(behaviorFilePath, row);
    }

}



// using UnityEngine;
// using System.Collections.Generic;
// using System.IO;
// using System.Text;
// using UnityEngine.SceneManagement;
// using Normal.Realtime;

// public class WhackAMoleTaskManager : MonoBehaviour {
//     public static WhackAMoleTaskManager Instance { get; private set; }

//     [Header("Block Settings")]
//     public float blockDurationSeconds = 120f; // after first hit
//     public bool autoReturnToLobby = true;
//     public string lobbySceneName = "LobbyAvtrs";

//     [Header("Logging")]
//     [SerializeField] private bool disableLogging = false;

//     private bool firstHitOccurred = false;
//     private float blockStartTime = 0f;
//     private float firstHitTime = 0f;
//     private bool blockRunning = false;

//     private WhackAMoleSpawner spawner;
//     private Realtime realtime;
//     private DataLogger localLogger;

//     private string dyadID = "UnknownDyad";
//     private string condition = "UnknownCondition";

//     // Performance counters
//     private int totalMolesSpawned = 0;
//     private int[] hitsPerPlayer = new int[2];
//     private int totalMisses = 0;

//     // Logging paths
//     private string behaviorFilePath;
//     private string performanceFilePath;

//     private class MoleEventInfo {
//         public WhackHole hole;
//         public float popTime;
//         public bool wasHit;
//     }

//     private Dictionary<MoleController, MoleEventInfo> activeMoles =
//         new Dictionary<MoleController, MoleEventInfo>();

//     void Awake() {
//         if (Instance != null && Instance != this) {
//             Destroy(gameObject);
//             return;
//         }
//         Instance = this;

//         spawner = FindObjectOfType<WhackAMoleSpawner>();
//         realtime = FindObjectOfType<Realtime>();

//         ParseSceneName();
//     }

//     void Start() {
//         // We don't start the block yet. First hit will trigger.
//         // But we do want one mole up at the beginning.
//         SpawnInitialMole();
//     }

//     void Update() {
//         if (disableLogging && !blockRunning && firstHitOccurred) {
//             // still manage block timing even without logging
//         }

//         // Lazy-find local logger
//         if (localLogger == null && !disableLogging) {
//             DataLogger[] all = FindObjectsOfType<DataLogger>();
//             foreach (var log in all) {
//                 var view = log.GetComponent<RealtimeView>();
//                 if (view != null && view.isOwnedLocallySelf) {
//                     localLogger = log;
//                     if (log.avatarConfigData != null) {
//                         dyadID = log.avatarConfigData.dyadID;
//                     }
//                     InitLogFiles();
//                     break;
//                 }
//             }
//         }

//         if (blockRunning) {
//             float t = Time.time - blockStartTime;
//             if (t >= blockDurationSeconds) {
//                 EndBlock();
//             }
//         }
//     }

//     private void ParseSceneName() {
//         string scene = SceneManager.GetActiveScene().name.ToLower();
//         if (scene.Contains("lowfid")) condition = "LowFid";
//         else if (scene.Contains("highfidnas")) condition = "HighFidNAS";
//         else if (scene.Contains("highfid")) condition = "HighFid";
//         else condition = "UnknownCondition";
//     }

//     private void InitLogFiles() {
//         string dir = Path.Combine(Application.persistentDataPath, "WhackAMole");
//         Directory.CreateDirectory(dir);

//         // Behavioral file (per condition)
//         string behaviorFilename = $"{dyadID}_{condition}_WhackAMole_Behavior.csv";
//         behaviorFilePath = Path.Combine(dir, behaviorFilename);
//         if (!File.Exists(behaviorFilePath)) {
//             File.WriteAllText(behaviorFilePath,
//                 "DyadID,Condition,Timestamp,EventType,PlayerID," +
//                 "MoleID,RowIndex,ColIndex,RegionLateral,RegionDepth," +
//                 "PopTime,HitTime,ReactionTime,WasHit\n");
//         }

//         // Performance file (one per dyad, append per condition)
//         string perfFilename = $"{dyadID}_WhackAMole_PerformanceMetrics.csv";
//         performanceFilePath = Path.Combine(dir, perfFilename);
//         if (!File.Exists(performanceFilePath)) {
//             File.WriteAllText(performanceFilePath,
//                 "DyadID,Condition,BlockDuration,TotalMolesSpawned,HitsP0,HitsP1,TotalMisses\n");
//         }
//     }

//     private void SpawnInitialMole() {
//         if (spawner == null) return;

//         // Simple: ask spawner to spawn once by cheating:
//         // Temporarily run one iteration or just manually pick a random hole
//         var holes = FindObjectsOfType<WhackHole>();
//         if (holes.Length == 0) return;

//         var h = holes[Random.Range(0, holes.Length)];
//         if (h.mole != null && !h.mole.IsUp) {
//             h.mole.Up();
//             OnMoleSpawned(h);
//         }
//     }

//     // Called by spawner for normal spawns
//     public void OnMoleSpawned(WhackHole hole) {
//         totalMolesSpawned++;

//         var mole = hole.mole;
//         if (mole == null) return;

//         var info = new MoleEventInfo {
//             hole = hole,
//             popTime = Time.time,
//             wasHit = false
//         };
//         activeMoles[mole] = info;

//         if (!disableLogging && !string.IsNullOrEmpty(behaviorFilePath)) {
//             string row = $"{dyadID},{condition},{Time.time:F3},Spawn,-1," +
//                          $"{mole.GetInstanceID()},{hole.rowIndex},{hole.colIndex}," +
//                          $"{hole.regionLateral},{hole.regionDepth}," +
//                          $"{info.popTime:F3},,,-1\n";
//             File.AppendAllText(behaviorFilePath, row);
//         }
//     }

//     // Called by HammerHit when a collider enters a mole
//     public void OnMoleHit(MoleController mole, int playerId) {
//         // First hit in entire session?
//         if (!firstHitOccurred) {
//             firstHitOccurred = true;
//             firstHitTime = Time.time;
//             blockStartTime = Time.time;
//             blockRunning = true;

//             if (!disableLogging && localLogger != null) {
//                 localLogger.BeginTrial();
//             }

//             if (spawner != null) {
//                 spawner.StartGenerating(blockStartTime);
//             }
//         }

//         // Try to hit the mole (this will move it underground)
//         bool wasHitNow = mole.Hit();
//         if (!wasHitNow) return; // already underground or something

//         hitsPerPlayer[playerId]++;

//         MoleEventInfo info;
//         if (!activeMoles.TryGetValue(mole, out info)) {
//             // No record (e.g., initial mole). Just log something basic.
//             info = new MoleEventInfo {
//                 hole = null,
//                 popTime = Time.time,
//                 wasHit = true
//             };
//         } else {
//             info.wasHit = true;
//             activeMoles.Remove(mole);
//         }

//         float hitTime = Time.time;
//         float reaction = hitTime - info.popTime;

//         if (!disableLogging && !string.IsNullOrEmpty(behaviorFilePath)) {
//             string row = $"{dyadID},{condition},{Time.time:F3},Hit,{playerId}," +
//                          $"{mole.GetInstanceID()}," +
//                          $"{(info.hole != null ? info.hole.rowIndex.ToString() : "-1")}," +
//                          $"{(info.hole != null ? info.hole.colIndex.ToString() : "-1")}," +
//                          $"{(info.hole != null ? info.hole.regionLateral : "Unknown")}," +
//                          $"{(info.hole != null ? info.hole.regionDepth : "Unknown")}," +
//                          $"{info.popTime:F3},{hitTime:F3},{reaction:F3},1\n";
//             File.AppendAllText(behaviorFilePath, row);
//         }
//     }

//     // Optional: call this from MoleController when it goes DOWN without hit
//     public void OnMoleMissed(MoleController mole) {
//         totalMisses++;

//         MoleEventInfo info;
//         if (!activeMoles.TryGetValue(mole, out info)) {
//             return;
//         }
//         activeMoles.Remove(mole);

//         if (!disableLogging && !string.IsNullOrEmpty(behaviorFilePath)) {
//             string row = $"{dyadID},{condition},{Time.time:F3},Miss,-1," +
//                          $"{mole.GetInstanceID()},{info.hole.rowIndex},{info.hole.colIndex}," +
//                          $"{info.hole.regionLateral},{info.hole.regionDepth}," +
//                          $"{info.popTime:F3},,,-1\n";
//             File.AppendAllText(behaviorFilePath, row);
//         }
//     }

//     private void EndBlock() {
//         blockRunning = false;

//         if (spawner != null) {
//             spawner.StopGenerating();
//         }

//         if (!disableLogging && localLogger != null) {
//             localLogger.EndTrial();
//         }

//         if (!disableLogging && !string.IsNullOrEmpty(performanceFilePath)) {
//             string row = $"{dyadID},{condition},{blockDurationSeconds:F1}," +
//                          $"{totalMolesSpawned},{hitsPerPlayer[0]},{hitsPerPlayer[1]},{totalMisses}\n";
//             File.AppendAllText(performanceFilePath, row);
//         }

//         if (autoReturnToLobby) {
//             Invoke(nameof(ReturnToLobby), 2f);
//         }
//     }

//     private void ReturnToLobby() {
//         SceneManager.LoadScene(lobbySceneName);
//     }
// }
