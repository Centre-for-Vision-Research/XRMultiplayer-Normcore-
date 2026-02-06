using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

public class RoleManager : RealtimeComponent<RoleManagerModel>
{
    public static RoleManager Instance;

    private bool roleAssigned = false; // Prevent duplicate assignment

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[RoleManager] Instance created.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (this.realtime == null)
        {
            Debug.LogError("[RoleManager] Realtime component not found in the scene.");
            return;
        }

        Debug.Log("[RoleManager] Starting role assignment coroutine.");
        StartCoroutine(WaitForPlayersAndAssignRoles());
    }

    private IEnumerator WaitForPlayersAndAssignRoles()
    {
        while (!roleAssigned)
        {
            yield return new WaitForSeconds(1.0f);

            List<int> clientIDs = new List<int>();

            // Find all avatars tagged PlayerAvatar
            foreach (GameObject avatar in GameObject.FindGameObjectsWithTag("PlayerAvatar"))
            {
                RealtimeView view = avatar.GetComponent<RealtimeView>();
                if (view != null && view.ownerIDInHierarchy >= 0)
                {
                    if (!clientIDs.Contains(view.ownerIDInHierarchy))
                    {
                        clientIDs.Add(view.ownerIDInHierarchy);
                    }
                }
            }

            if (clientIDs.Count < 2)
            {
                continue;
            }

            // FIX 1: deterministic order
            clientIDs.Sort();

            // Only lowest clientID assigns roles
            int lowestClientID = Mathf.Min(client_toggle(clientIDs));
            if (this.realtime.clientID != lowestClientID)
            {
                Debug.Log("[RoleManager] Not master client. Waiting for host to assign roles.");
                yield break;
            }

            // FIX 2: robust "already assigned" check (teacherID can be 0)
            bool rolesAlreadyAssigned =
                (model.teacherID != model.studentID) &&
                clientIDs.Contains(model.teacherID) &&
                clientIDs.Contains(model.studentID);

            if (rolesAlreadyAssigned)
            {
                roleAssigned = true;
                Debug.Log($"[RoleManager] Roles already assigned. TeacherID={model.teacherID}, StudentID={model.studentID}. Exiting.");
                yield break;
            }

            // Assign roles (now deterministic)
            int teacherID = clientIDs[0];
            int studentID = clientIDs[1];

            model.teacherID = teacherID;
            model.studentID = studentID;

            int commonSeed = Random.Range(1, 1000000);
            model.commonSeed = commonSeed;
            model.currentHoleIndex = -1;

            roleAssigned = true;

            Debug.Log(
                $"[RoleManager] Roles assigned. " +
                $"TeacherID={teacherID}, StudentID={studentID}, CommonSeed={commonSeed}"
            );

            yield break;
        }
    }

    // ----------------------------------------------------------------
    // Public helpers (safe accessors)
    // ----------------------------------------------------------------
    public bool IsTeacher(int clientID)
    {
        return model != null && model.teacherID == clientID;
    }

    public bool IsStudent(int clientID)
    {
        return model != null && model.studentID == clientID;
    }

    public int GetTeacherID()
    {
        return model != null ? model.teacherID : 0;
    }

    public int GetStudentID()
    {
        return model != null ? model.studentID : 0;
    }

    public int GetCommonSeed()
    {
        return model != null ? model.commonSeed : 0;
    }

    public int GetCurrentHoleIndex()
    {
        return model != null ? model.currentHoleIndex : -1;
    }

    // Only whoever owns RoleManagerView should call this
    public void SetCurrentHoleIndex(int index)
    {
        if (model == null) return;
        model.currentHoleIndex = index;
    }

    // Utility
    private int[] client_toggle(List<int> list)
    {
        int[] arr = new int[list.Count];
        for (int i = 0; i < list.Count; i++)
            arr[i] = list[i];
        return arr;
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
