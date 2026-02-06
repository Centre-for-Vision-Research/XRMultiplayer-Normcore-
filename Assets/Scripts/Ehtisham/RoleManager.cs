using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

public class RoleManager : RealtimeComponent<RoleManagerModel>
{
    public static RoleManager Instance;

    [Header("Debug")]
    public bool verboseLogs = true;
    public float pollIntervalSeconds = 0.1f;
    public float logEverySeconds = 1.0f;

    private bool rolesFinalized = false;
    private float _nextLogTime = 0f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Log("Instance created.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (realtime == null)
        {
            Debug.LogError("[RoleManager] Realtime not found.");
            return;
        }

        StartCoroutine(RoleAssignmentLoop());
    }

    private IEnumerator RoleAssignmentLoop()
    {
        while (!rolesFinalized)
        {
            yield return new WaitForSeconds(pollIntervalSeconds);

            List<int> clientIDs = CollectClientIDs();

            if (Time.time >= _nextLogTime)
            {
                _nextLogTime = Time.time + logEverySeconds;
                Log($"Polling owners=[{string.Join(",", clientIDs)}] " +
                    $"teacher={SafeTeacher()} student={SafeStudent()} seed={SafeSeed()}");
            }

            if (clientIDs.Count == 0)
                continue;

            clientIDs.Sort();
            int hostID = clientIDs[0];

            // Only host writes to model
            if (realtime.clientID != hostID)
            {
                // Non-host just waits for model to update
                if (IsDyadReady())
                {
                    rolesFinalized = true;
                    Log("Detected finalized dyad from model.");
                }
                continue;
            }

            // ---------- SOLO MODE ----------
            if (clientIDs.Count == 1)
            {
                int soloID = clientIDs[0];

                if (model.teacherID != soloID || model.studentID != soloID)
                {
                    model.teacherID = soloID;
                    model.studentID = soloID;   // placeholder
                    model.commonSeed = 0;       // NOT finalized
                    model.currentHoleIndex = -1;

                    Log($"SOLO MODE assigned. Teacher={soloID}. Waiting for peer.");
                }

                continue;
            }

            // ---------- DYAD MODE ----------
            int teacherID = clientIDs[0];
            int studentID = clientIDs[1];

            bool alreadyFinal =
                model.teacherID == teacherID &&
                model.studentID == studentID &&
                model.commonSeed != 0;

            if (alreadyFinal)
            {
                rolesFinalized = true;
                Log("Dyad already finalized.");
                yield break;
            }

            model.teacherID = teacherID;
            model.studentID = studentID;
            model.commonSeed = Random.Range(1, 1000000);
            model.currentHoleIndex = -1;

            rolesFinalized = true;

            Log($"DYAD FINALIZED. Teacher={teacherID} Student={studentID} Seed={model.commonSeed}");
            yield break;
        }
    }

    private List<int> CollectClientIDs()
    {
        List<int> ids = new List<int>();
        var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");

        foreach (var avatar in avatars)
        {
            var view = avatar.GetComponent<RealtimeView>();
            if (view == null) continue;

            int owner = view.ownerIDInHierarchy;
            if (owner >= 0 && !ids.Contains(owner))
                ids.Add(owner);
        }

        return ids;
    }

    // ---------------- Public API ----------------

    public bool IsTeacher(int clientID)
        => model != null && model.teacherID == clientID;

    public bool IsStudent(int clientID)
        => model != null && model.studentID == clientID;

    public bool IsSolo()
        => model != null && model.teacherID == model.studentID;

    public bool IsDyadReady()
        => model != null &&
           model.teacherID != model.studentID &&
           model.commonSeed != 0;

    public int GetTeacherID()
        => model != null ? model.teacherID : -1;

    public int GetStudentID()
        => model != null ? model.studentID : -1;

    public int GetCommonSeed()
        => model != null ? model.commonSeed : 0;

    public int GetCurrentHoleIndex()
        => model != null ? model.currentHoleIndex : -1;

    public void SetCurrentHoleIndex(int index)
    {
        if (model != null)
            model.currentHoleIndex = index;
    }

    // ---------------- Logging helpers ----------------

    private int SafeTeacher() => model != null ? model.teacherID : -999;
    private int SafeStudent() => model != null ? model.studentID : -999;
    private int SafeSeed() => model != null ? model.commonSeed : -999;

    private void Log(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[RoleManager] cid={realtime.clientID} {msg}");
    }
}





//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;
//using Normal.Realtime;

//public class RoleManager : RealtimeComponent<RoleManagerModel>
//{
//    public static RoleManager Instance;

//    private bool roleAssigned = false; // Prevent duplicate assignment

//    void Awake()
//    {
//        if (Instance == null)
//        {
//            Instance = this;
//            Debug.Log("[RoleManager] Instance created.");
//        }
//        else
//        {
//            Destroy(gameObject);
//        }
//    }

//    void Start()
//    {
//        if (this.realtime == null)
//        {
//            Debug.LogError("[RoleManager] Realtime component not found in the scene.");
//            return;
//        }

//        Debug.Log("[RoleManager] Starting role assignment coroutine.");
//        StartCoroutine(WaitForPlayersAndAssignRoles());
//    }

//    private IEnumerator WaitForPlayersAndAssignRoles()
//    {
//        while (!roleAssigned)
//        {
//            yield return new WaitForSeconds(1.0f);

//            List<int> clientIDs = new List<int>();

//            // Find all avatars tagged PlayerAvatar
//            foreach (GameObject avatar in GameObject.FindGameObjectsWithTag("PlayerAvatar"))
//            {
//                RealtimeView view = avatar.GetComponent<RealtimeView>();
//                if (view != null && view.ownerIDInHierarchy >= 0)
//                {
//                    if (!clientIDs.Contains(view.ownerIDInHierarchy))
//                    {
//                        clientIDs.Add(view.ownerIDInHierarchy);
//                    }
//                }
//            }

//            if (clientIDs.Count < 2)
//            {
//                Debug.Log("[RoleManager] Waiting for 2 players to join...");
//                continue;
//            }

//            // Only lowest clientID assigns roles
//            int lowestClientID = Mathf.Min(client_toggle(clientIDs));
//            if (this.realtime.clientID != lowestClientID)
//            {
//                Debug.Log("[RoleManager] Not master client. Waiting for host to assign roles.");
//                yield break;
//            }

//            // Stop if already assigned
//            if (model.teacherID != 0 || model.studentID != 0)
//            {
//                roleAssigned = true;
//                Debug.Log("[RoleManager] Roles already assigned. Exiting.");
//                yield break;
//            }

//            // Assign roles
//            int teacherID = clientIDs[0];
//            int studentID = clientIDs[1];

//            model.teacherID = teacherID;
//            model.studentID = studentID;

//            int commonSeed = Random.Range(1, 1000000);
//            model.commonSeed = commonSeed;
//            model.currentHoleIndex = -1;

//            roleAssigned = true;

//            Debug.Log(
//                $"[RoleManager] Roles assigned. " +
//                $"TeacherID={teacherID}, StudentID={studentID}, CommonSeed={commonSeed}"
//            );

//            yield break;
//        }
//    }

//    // ----------------------------------------------------------------
//    // Public helpers (safe accessors)
//    // ----------------------------------------------------------------
//    public bool IsTeacher(int clientID)
//    {
//        return model != null && model.teacherID == clientID;
//    }

//    public bool IsStudent(int clientID)
//    {
//        return model != null && model.studentID == clientID;
//    }

//    public int GetTeacherID()
//    {
//        return model != null ? model.teacherID : 0;
//    }

//    public int GetStudentID()
//    {
//        return model != null ? model.studentID : 0;
//    }

//    public int GetCommonSeed()
//    {
//        return model != null ? model.commonSeed : 0;
//    }

//    public int GetCurrentHoleIndex()
//    {
//        return model != null ? model.currentHoleIndex : -1;
//    }

//    // Only whoever owns RoleManagerView should call this
//    public void SetCurrentHoleIndex(int index)
//    {
//        if (model == null) return;
//        model.currentHoleIndex = index;
//    }

//    // Utility
//    private int[] client_toggle(List<int> list)
//    {
//        int[] arr = new int[list.Count];
//        for (int i = 0; i < list.Count; i++)
//            arr[i] = list[i];
//        return arr;
//    }
//}
