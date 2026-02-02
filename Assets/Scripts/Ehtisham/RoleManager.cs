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
                Debug.Log("[RoleManager] Waiting for 2 players to join...");
                continue;
            }

            // Only lowest clientID assigns roles
            int lowestClientID = Mathf.Min(client_toggle(clientIDs));
            if (this.realtime.clientID != lowestClientID)
            {
                Debug.Log("[RoleManager] Not master client. Waiting for host to assign roles.");
                yield break;
            }

            // Stop if already assigned
            if (model.teacherID != 0 || model.studentID != 0)
            {
                roleAssigned = true;
                Debug.Log("[RoleManager] Roles already assigned. Exiting.");
                yield break;
            }

            // Assign roles
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




// using UnityEngine;
// using Normal.Realtime;
// using System.Collections;
// using System.Collections.Generic;

// public class RoleManager : RealtimeComponent<RoleManagerModel> {
//     public static RoleManager Instance;
//     private bool roleAssigned = false; // Prevents duplicate role assignment

//     void Awake() {
//         if (Instance == null) {
//             Instance = this;
//         } else {
//             Destroy(gameObject);
//         }
//     }

//     void Start() {
//         if (this.realtime == null) {  //  Use the inherited 'realtime' variable
//             Debug.LogError("Realtime component not found in the scene.");
//             return;
//         }

//         // Start checking for role assignment periodically
//         StartCoroutine(WaitForPlayersAndAssignRoles());
//     }

//     private IEnumerator WaitForPlayersAndAssignRoles() {
//         while (!roleAssigned) {
//             yield return new WaitForSeconds(1.0f); // Check every 1 second

//             List<int> clientIDs = new List<int>();

//             // Get all player avatars (assuming they have "PlayerAvatar" tag)
//             foreach (GameObject avatar in GameObject.FindGameObjectsWithTag("PlayerAvatar")) {
//                 RealtimeView view = avatar.GetComponent<RealtimeView>();
//                 if (view != null && !clientIDs.Contains(view.ownerIDInHierarchy) && view.ownerIDInHierarchy >= 0) {
//                     clientIDs.Add(view.ownerIDInHierarchy);
//                 }
//             }

//             if (clientIDs.Count < 2) {
//                 continue; // Keep checking until we have 2 players
//             }

//             // **Ensure only the lowest client ID assigns roles**
//             int lowestClientID = Mathf.Min(clientIDs.ToArray());
//             if (this.realtime.clientID != lowestClientID) {
//                 yield break; // Stop checking once roles are assigned
//             }

//             // **Only assign roles if not already assigned**
//             if (GetTeacherID() != 0 && GetStudentID() != 0) {
//                 roleAssigned = true;
//                 yield break;
//             }

//             // **Randomly assign Teacher and Student**
//             int teacherID = 1;//clientIDs[Random.Range(0, clientIDs.Count)];
//             int studentID = 0;//clientIDs.Find(id => id != teacherID);

//             model.teacherID = teacherID;
//             model.studentID = studentID;
//             roleAssigned = true; // Mark roles as assigned
//             yield break; // Stop the coroutine after assigning roles
//         }
//     }

//     public bool IsTeacher(int clientID) {
//         return model.teacherID == clientID;
//     }

//     public bool IsStudent(int clientID) {
//         return model.studentID == clientID;
//     }

//     public int GetTeacherID() {
//         return model != null ? model.teacherID : 0;
//     }

//     public int GetStudentID() {
//         return model != null ? model.studentID : 0;
//     }
// }