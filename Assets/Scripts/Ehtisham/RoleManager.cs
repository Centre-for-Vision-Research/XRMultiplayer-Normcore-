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
        // Wait until the model exists
        yield return new WaitUntil(() => model != null);
        yield return new WaitUntil(() => realtime != null && realtime.connected && realtime.clientID >= 0);

        while (true)
        {
            yield return new WaitForSeconds(pollIntervalSeconds);

            List<int> clientIDs = CollectClientIDs();

            if (Time.time >= _nextLogTime)
            {
                _nextLogTime = Time.time + logEverySeconds;
                Log($"Polling owners=[{string.Join(",", clientIDs)}] teacher={SafeTeacher()} student={SafeStudent()} seed={SafeSeed()}");
            }

            if (clientIDs.Count == 0)
                continue;

            clientIDs.Sort();
            int hostID = clientIDs[0];

            // Only host writes to model
            if (realtime.clientID != hostID)
                continue;

            // SOLO: teacher is host, student is -1
            if (clientIDs.Count == 1)
            {
                int teacherID = clientIDs[0];

                bool changed =
                    model.teacherID != teacherID ||
                    model.studentID != -1;

                if (changed)
                {
                    model.teacherID = teacherID;
                    model.studentID = -1;          // IMPORTANT
                    model.currentHoleIndex = -1;
                }

                if (model.commonSeed == 0)
                    model.commonSeed = Random.Range(1, 1000000);

                continue;
            }

            // DYAD: first is teacher, second is student
            int t = clientIDs[0];
            int s = clientIDs[1];

            bool rolesOk = (model.teacherID == t && model.studentID == s);
            if (!rolesOk)
            {
                model.teacherID = t;
                model.studentID = s;
                model.currentHoleIndex = -1;
            }

            if (model.commonSeed == 0)
                model.commonSeed = Random.Range(1, 1000000);
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
        => model != null && model.teacherID >= 0 && model.teacherID == clientID;

    public bool IsStudent(int clientID)
        => model != null && model.studentID >= 0 && model.studentID == clientID;

    public bool IsSolo()
        => model != null && model.studentID < 0 && model.teacherID >= 0;

    public bool IsDyadReady()
        => model != null &&
           model.teacherID >= 0 &&
           model.studentID >= 0 &&
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

    private int SafeTeacher() => model != null ? model.teacherID : -999;
    private int SafeStudent() => model != null ? model.studentID : -999;
    private int SafeSeed() => model != null ? model.commonSeed : -999;

    private void Log(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[RoleManager] cid={realtime.clientID} {msg}");
    }
}
