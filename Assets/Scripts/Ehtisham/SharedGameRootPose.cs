using UnityEngine;
using Normal.Realtime;

public class SharedGameRootPose : MonoBehaviour {
    public Transform gameRoot;
    public Transform hostPlacementSource;

    private Realtime realtime;
    private RoleManager roleManager;
    private bool subscribed = false;

    void Start() {
        realtime = FindObjectOfType<Realtime>();
        roleManager = FindObjectOfType<RoleManager>();

        InvokeRepeating(nameof(SubscribeToModelChanges), 0.2f, 0.2f);
    }

    void Update()
    {
        if (roleManager == null || realtime == null) return;

        bool isTeacher = roleManager.IsTeacher(realtime.clientID);

        if (isTeacher)
        {
            if (OVRInput.GetDown(OVRInput.Button.One) || Input.GetKeyDown(KeyCode.P))
            {
                HostSetTableHere();
            }
        }
        else
        {
            if (roleManager.GetGameRootPoseSet())
            {
                ApplyModelPose();
            }
        }
    }


    void SubscribeToModelChanges() {
        if (subscribed) return;
        if (roleManager == null) return;

        subscribed = true;
        CancelInvoke(nameof(SubscribeToModelChanges));

        // if already set
        if (roleManager.GetGameRootPoseSet()) 
            ApplyModelPose();
    }

    public void HostSetTableHere() {
        if (!roleManager.IsTeacher(realtime.clientID)) return;

        // Grab desired horizontal position from source
        Vector3 desiredPos = hostPlacementSource.position;

        // Put table *on floor*
        desiredPos.y = 0f;

        Quaternion desiredRot = hostPlacementSource.rotation;
        desiredRot = Quaternion.Euler(0f, desiredRot.eulerAngles.y, 0f);

        gameRoot.SetPositionAndRotation(desiredPos, desiredRot);

        roleManager.SetGameRootPose(desiredPos, desiredRot);
    }


    void ApplyModelPose() {
        if (roleManager == null) return;

        Vector3 pos = roleManager.GetGameRootPosition();
        Quaternion rot = roleManager.GetGameRootRotation();
        gameRoot.SetPositionAndRotation(pos, rot);
    }
}
