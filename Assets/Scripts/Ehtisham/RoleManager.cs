using UnityEngine;
using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;

public class RoleManager : RealtimeComponent<RoleManagerModel> {
    public static RoleManager Instance;
    private bool roleAssigned = false; // Prevent duplicate assignment

    void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
        }
    }

    void Start() {
        if (this.realtime == null) {  
            Debug.LogError("Realtime component not found in the scene.");
            return;
        }
        StartCoroutine(WaitForPlayersAndAssignRoles());
    }

    private IEnumerator WaitForPlayersAndAssignRoles() {
        while (!roleAssigned) {
            yield return new WaitForSeconds(1.0f); // Check every second

            List<int> clientIDs = new List<int>();

            // Get all player avatars (assumed to have "PlayerAvatar" tag)
            foreach (GameObject avatar in GameObject.FindGameObjectsWithTag("PlayerAvatar")) {
                RealtimeView view = avatar.GetComponent<RealtimeView>();
                if (view != null && !clientIDs.Contains(view.ownerIDInHierarchy) && view.ownerIDInHierarchy >= 0) {
                    clientIDs.Add(view.ownerIDInHierarchy);
                }
            }

            if (clientIDs.Count < 2) continue; // Wait until at least 2 players have joined

            // Only the master client (lowest clientID) assigns roles
            int lowestClientID = Mathf.Min(clientIDs.ToArray());
            if (this.realtime.clientID != lowestClientID) {
                yield break; // Master will handle assignment
            }

            if (GetTeacherID() != 0 && GetStudentID() != 0) {
                roleAssigned = true;
                yield break;
            }

            // Teacher is always the first client. Student joins second!
            int teacherID = clientIDs[0];
            int studentID = clientIDs[1];

            model.teacherID = teacherID;
            model.studentID = studentID;

            // Generate and assign a common random seed
            int commonSeed = Random.Range(1, 1000000); // Use 1 to avoid 0
            model.commonSeed = commonSeed;
            Debug.Log($"Roles assigned: Teacher = {teacherID}, Student = {studentID} | Common Seed = {commonSeed}");

            roleAssigned = true;
            yield break;
        }
    }

    public bool IsTeacher(int clientID) {
        return model.teacherID == clientID;
    }

    public bool IsStudent(int clientID) {
        return model.studentID == clientID;
    }

    public int GetTeacherID() {
        return model != null ? model.teacherID : 0;
    }

    public int GetStudentID() {
        return model != null ? model.studentID : 0;
    }
    
    public int GetCommonSeed() {
        return model != null ? model.commonSeed : 0;
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