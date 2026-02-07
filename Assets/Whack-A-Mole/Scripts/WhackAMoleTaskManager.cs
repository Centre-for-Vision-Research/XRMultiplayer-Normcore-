using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class WhackAMoleTaskManager : MonoBehaviour
{
    public static WhackAMoleTaskManager Instance { get; private set; }

    [Header("Block Settings")]
    public float blockDurationSeconds = 120f;
    public bool autoReturnToLobby = true;
    public string lobbySceneName = "LobbyAvtrs";

    [Header("Logging")]
    public bool disableLogging = false;

    [Header("Debug")]
    public bool verboseLogs = true;

    private bool firstHitOccurred = false;
    private bool blockRunning = false;
    private float blockStartTime = 0f;

    // Authority for gameplay is ALWAYS cid==0
    private bool isAuthorityHost = false;

    private WhackAMoleSpawner spawner;
    private Realtime realtime;
    private DataLogger localLogger;

    private string dyadID = "UnknownDyad";
    private string condition = "UnknownCondition";

    private int totalMolesSpawned = 0;
    private int totalMisses = 0;
    private int[] hitsPerPlayer = new int[2];

    private class MoleEventInfo
    {
        public WhackHole hole;
        public float popTime;
    }

    private Dictionary<MoleController, MoleEventInfo> activeMoles =
        new Dictionary<MoleController, MoleEventInfo>();

    public bool IsHost() => isAuthorityHost;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        spawner = FindObjectOfType<WhackAMoleSpawner>();
        realtime = FindObjectOfType<Realtime>();
        ParseSceneName();
    }

    private void Start()
    {
        StartCoroutine(BootFlow());
    }

    private IEnumerator BootFlow()
    {
        while (realtime == null)
        {
            realtime = FindObjectOfType<Realtime>();
            yield return null;
        }

        while (realtime.clientID < 0)
            yield return null;

        // FIX:  Wait for MR anchor before touching gameplay
        if (MRSharedAnchorManager.Instance != null &&
            MRSharedAnchorManager.Instance.isMRScene)
        {
            yield return new WaitUntil(() => MRSharedAnchorManager.Instance.AnchorReady);
        }

        isAuthorityHost = (realtime.clientID == 0);
        Log($"Connected. cid={realtime.clientID} authorityHost={isAuthorityHost}");

        if (!disableLogging)
            StartCoroutine(InitLoggerAsync());

        if (!isAuthorityHost)
        {
            Log("Not authority host. Waiting for mole model updates only.");
            yield break;
        }

        yield return StartCoroutine(WaitForMoleModelsAndOwnership());
        yield return StartCoroutine(ForceAllDownThenRaiseOne());
    }


    private IEnumerator InitLoggerAsync()
    {
        int tries = 0;
        while (localLogger == null && tries < 240)
        {
            TryFindLocalLogger();
            tries++;
            yield return null;
        }

        Log(localLogger != null ? "Logger ready" : "Logger not found");
    }

    private void TryFindLocalLogger()
    {
        foreach (var log in FindObjectsOfType<DataLogger>())
        {
            var v = log.GetComponent<RealtimeView>();
            if (v != null && v.isOwnedLocallySelf)
            {
                localLogger = log;
                if (log.avatarConfigData != null)
                    dyadID = log.avatarConfigData.dyadID;
                break;
            }
        }
    }

    private IEnumerator WaitForMoleModelsAndOwnership()
    {
        float start = Time.time;

        while (true)
        {
            var moles = FindObjectsOfType<MoleController>(true);
            if (moles == null || moles.Length == 0)
            {
                if (Time.time - start > 10f)
                {
                    Debug.LogError("[WhackAMoleTaskManager] No moles found after 10s.");
                    yield break;
                }
                yield return null;
                continue;
            }

            bool ok = true;

            foreach (var m in moles)
            {
                if (m == null || !m.HasModel())
                {
                    ok = false; break;
                }

                var view = m.GetComponent<RealtimeView>();
                if (view == null || !view.isOwnedLocallySelf)
                {
                    ok = false; break;
                }
            }

            if (ok)
            {
                Log($"Mole models+ownership ready. moles={moles.Length}");
                yield break;
            }

            if (Time.time - start > 10f)
            {
                Debug.LogError("[WhackAMoleTaskManager] Timed out waiting for mole models+ownership.");
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator ForceAllDownThenRaiseOne()
    {
        Log("ForceAllDownThenRaiseOne begin");

        var moles = FindObjectsOfType<MoleController>(true);
        foreach (var m in moles)
            m.HideImmediate_Authority();

        // wait 2 frames to let model sync settle
        yield return null;
        yield return null;

        var holes = new List<WhackHole>(FindObjectsOfType<WhackHole>(true));
        holes.RemoveAll(h => h == null || h.mole == null);

        if (holes.Count == 0)
        {
            Debug.LogError("[WhackAMoleTaskManager] No valid holes with moles.");
            yield break;
        }

        var chosen = holes[Random.Range(0, holes.Count)];
        chosen.mole.ShowImmediate_Authority();
        RegisterSpawn(chosen);

        Log($"Initial mole UP -> hole={chosen.name} mole={chosen.mole.name}");
    }

    // -------------------------------------------------------
    public void RegisterSpawn(WhackHole hole)
    {
        if (!isAuthorityHost) return;

        totalMolesSpawned++;
        activeMoles[hole.mole] = new MoleEventInfo { hole = hole, popTime = Time.time };
    }

    public void OnMoleHit(MoleController mole, int playerId)
    {
        if (!isAuthorityHost || mole == null) return;
        if (!mole.Hit_Authority()) return;

        if (!firstHitOccurred)
        {
            firstHitOccurred = true;
            blockRunning = true;
            blockStartTime = Time.time;

            // Start trial only if dyad exists (optional, you can remove this gate)
            if (!disableLogging && localLogger != null && RoleManager.Instance != null &&
                RoleManager.Instance.GetTeacherID() != RoleManager.Instance.GetStudentID() &&
                RoleManager.Instance.GetCommonSeed() != 0)
            {
                localLogger.BeginTrial();
            }

            spawner.StartGenerating();
            Log("FIRST HIT -> spawner started");
        }

        if (playerId >= 0 && playerId < hitsPerPlayer.Length)
            hitsPerPlayer[playerId]++;

        activeMoles.Remove(mole);
    }

    public void OnMoleMissed(MoleController mole)
    {
        if (!isAuthorityHost || mole == null) return;
        if (!activeMoles.ContainsKey(mole)) return;

        activeMoles.Remove(mole);
        totalMisses++;
    }

    private void Update()
    {
        if (!isAuthorityHost) return;

        if (blockRunning && Time.time - blockStartTime >= blockDurationSeconds)
            EndBlock();
    }

    private void EndBlock()
    {
        blockRunning = false;
        spawner.StopGenerating();

        if (!disableLogging && localLogger != null)
            localLogger.EndTrial();

        Log($"BLOCK END. spawned={totalMolesSpawned} misses={totalMisses}");

        if (autoReturnToLobby)
            SceneManager.LoadScene(lobbySceneName);
    }

    private void ParseSceneName()
    {
        string s = SceneManager.GetActiveScene().name.ToLower();
        if (s.Contains("lowfid")) condition = "LowFid";
        else if (s.Contains("highfidnas")) condition = "HighFidNAS";
        else if (s.Contains("highfid")) condition = "HighFid";
        else condition = "UnknownCondition";
    }

    private void Log(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[WhackAMoleTaskManager] {msg}");
    }
}
