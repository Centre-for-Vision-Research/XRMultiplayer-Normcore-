using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

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
                if (view != null && view.ownerIDInHierarchy >= 0 && !clientIDs.Contains(view.ownerIDInHierarchy)) {
                    clientIDs.Add(view.ownerIDInHierarchy);
                }
            }

            if (clientIDs.Count < 2) continue; // Wait until at least 2 players have joined

            // Only the master client (lowest clientID) assigns roles
            int lowestClientID = Mathf.Min(clientIDs.ToArray());
            if (this.realtime.clientID != lowestClientID) {
                yield break; // Master will handle assignment
            }

            // If already assigned, stop
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

            // Initialize hole index to "none"
            model.currentHoleIndex = -1;
            
            roleAssigned = true;
            yield break;
        }
    }

    // ----------------------------------------------------------------
    // Public helpers — use these from other scripts (NO direct model!)
    // ----------------------------------------------------------------
    public bool IsTeacher(int clientID) {
        return model != null && model.teacherID == clientID;
    }

    public bool IsStudent(int clientID) {
        return model != null && model.studentID == clientID;
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

    public int GetCurrentHoleIndex() {
        return model != null ? model.currentHoleIndex : -1;
    }

    // --------------------------------------------------------------
    // Expose GameRoot Pose from model
    // --------------------------------------------------------------
    public bool GetGameRootPoseSet() {
        return model != null && model.gameRootPoseSet;
    }

    public Vector3 GetGameRootPosition() {
        if (model == null) return Vector3.zero;
        return new Vector3(model.grPosX, model.grPosY, model.grPosZ);
    }

    public Quaternion GetGameRootRotation() {
        if (model == null) return Quaternion.identity;
        return new Quaternion(model.grRotX, model.grRotY, model.grRotZ, model.grRotW);
    }

    // Only host should call this
    public void SetGameRootPose(Vector3 pos, Quaternion rot) {
        if (model == null) return;
        model.grPosX = pos.x;
        model.grPosY = pos.y;
        model.grPosZ = pos.z;
        model.grRotX = rot.x;
        model.grRotY = rot.y;
        model.grRotZ = rot.z;
        model.grRotW = rot.w;
        model.gameRootPoseSet = true;
    }


    // Only whoever owns this RoleManagerView should call this
    public void SetCurrentHoleIndex(int index) {
        if (model == null) return;

        // Optional: only allow lowest clientID / "master" to drive it
        if (realtime != null) {
            int lowestClientID = realtime.clientID; // Minimal assumption
            // You can add extra guard here if needed
        }

        model.currentHoleIndex = index;
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