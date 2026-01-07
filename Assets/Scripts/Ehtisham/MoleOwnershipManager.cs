using UnityEngine;
using Normal.Realtime;
using System.Collections.Generic;

public class MoleOwnershipManager : MonoBehaviour {

    private Realtime realtime;

    void Start() {
        realtime = FindObjectOfType<Realtime>();
        if (realtime == null) {
            Debug.LogError("Realtime not found.");
            return;
        }

        realtime.didConnectToRoom += DidConnect;
    }

    private void OnDestroy() {
        if (realtime != null)
            realtime.didConnectToRoom -= DidConnect;
    }

    private void DidConnect(Realtime room) {
        // Only host controls moles
        if (RoleManager.Instance == null) return;
        if (!RoleManager.Instance.IsTeacher(realtime.clientID)) return;

        // Find all mole views
        foreach (MoleController mole in FindObjectsOfType<MoleController>()) {

            RealtimeView view = mole.GetComponent<RealtimeView>();
            if (view != null) {
                view.RequestOwnership();
            }

            RealtimeTransform rt = mole.GetComponent<RealtimeTransform>();
            if (rt != null) {
                rt.RequestOwnership();
            }
        }

        Debug.Log("Host successfully took ownership of all moles.");
    }
}
