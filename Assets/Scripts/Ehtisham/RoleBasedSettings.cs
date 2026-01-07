using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;
using UnityEngine.SceneManagement;

public class RoleBasedSettings : MonoBehaviour {
    private Realtime realtime;
    private RealtimeView realtimeView;
    private XRDirectInteractor leftDirectInteractor;
    private XRDirectInteractor rightDirectInteractor;
    string sceneName;

    private bool applied = false;

    void Start() {
        realtime = FindObjectOfType<Realtime>();
        realtimeView = GetComponent<RealtimeView>();
        sceneName = SceneManager.GetActiveScene().name.ToLower();
    }

    public void ApplyRoleSettings() {
        if (applied || realtime == null || realtimeView == null) return;

        // Make sure only the local avatar applies settings
        if (!realtimeView.isOwnedLocallySelf) return;

        bool isTeacher = RoleManager.Instance.IsTeacher(realtime.clientID);

        // Disable grabbing if this avatar is the teacher
        leftDirectInteractor = FindInteractorGameObjectByName("Left Direct Interactor")?.GetComponent<XRDirectInteractor>();
        rightDirectInteractor = FindInteractorGameObjectByName("Right Direct Interactor")?.GetComponent<XRDirectInteractor>();

        if (isTeacher && !sceneName.Contains("tutorial")) {
            if (leftDirectInteractor != null) leftDirectInteractor.enabled = false;
            if (rightDirectInteractor != null) rightDirectInteractor.enabled = false;
        }

        // Apply the correct cube view
        SortingCube.Instance.SetTeacherView(isTeacher);

        Debug.Log(" Role settings applied for: " + (isTeacher ? "Teacher" : "Student"));

        applied = true;
    }

    private GameObject FindInteractorGameObjectByName(string name) {
        return GameObject.Find(name);
    }
}