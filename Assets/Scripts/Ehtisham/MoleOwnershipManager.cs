using UnityEngine;
using Normal.Realtime;
using System.Collections;

public class MoleOwnershipManager : MonoBehaviour {
    public bool verboseLogs = true;

    private Realtime realtime;

    void Start() {
        realtime = FindObjectOfType<Realtime>();
        if (realtime == null) {
            Debug.LogError("[MoleOwnershipManager] Realtime not found.");
            return;
        }

        realtime.didConnectToRoom += DidConnect;
    }

    private void OnDestroy() {
        if (realtime != null)
            realtime.didConnectToRoom -= DidConnect;
    }

    private void DidConnect(Realtime room) {
        StartCoroutine(TakeOwnershipAfterDelay());
    }

    private IEnumerator TakeOwnershipAfterDelay() {
        // wait so scene views are registered
        for (int i = 0; i < 10; i++) yield return null;

        // Only teacher should request ownership (consistent with authority)
        if (RoleManager.Instance == null || !RoleManager.Instance.IsTeacher(realtime.clientID)) {
            if (verboseLogs) Debug.Log($"[MoleOwnershipManager] Not teacher (cid={realtime.clientID}). Skipping ownership requests.");
            yield break;
        }

        var moles = FindObjectsOfType<MoleController>(true);
        int count = 0;

        foreach (var mole in moles) {
            var view = mole.GetComponent<RealtimeView>();
            if (view != null) {
                view.RequestOwnership();
                count++;
            }
        }

        if (verboseLogs) Debug.Log($"[MoleOwnershipManager] Teacher requested ownership for {count} mole views.");
    }
}
