using System.Text;
using UnityEngine;
using Normal.Realtime;

public class RemoteHammerHierarchyInspector : MonoBehaviour {
    [Header("Names")]
    public string networkAvatarsName = "NetworkAvatars";
    public string handTargetName = "HandTarget";
    public string hammerName = "Hammer";

    [Header("Hotkey")]
    public KeyCode dumpKey = KeyCode.H; // Editor
    public bool alsoDumpEvery5Seconds = false;

    float _nextDump;

    void Update() {
        if (Input.GetKeyDown(dumpKey)) DumpRemoteHammer();

        if (alsoDumpEvery5Seconds && Time.time >= _nextDump) {
            _nextDump = Time.time + 5f;
            DumpRemoteHammer();
        }
    }

    public void DumpRemoteHammer() {
        // Find a remote avatar (not owned locally).
        var views = FindObjectsOfType<RealtimeView>(true);

        RealtimeView remoteView = null;
        foreach (var v in views) {
            // Only consider player avatars you expect. If your avatar root always has a RealtimeView, this is enough.
            if (!v.isOwnedLocallySelf && v.ownerIDInHierarchy >= 0) {
                remoteView = v;
                break;
            }
        }

        if (remoteView == null) {
            Debug.LogWarning("[Inspector] No remote RealtimeView found yet.");
            return;
        }

        Transform remoteAvatarRoot = remoteView.transform;

        // Try to locate HandTarget and Hammer under the remote avatar.
        Transform handTarget = FindDeepChild(remoteAvatarRoot, handTargetName);
        Transform hammer = handTarget != null ? FindDeepChild(handTarget, hammerName) : null;

        var sb = new StringBuilder();
        sb.AppendLine("========== Remote Hammer Hierarchy Dump ==========");
        sb.AppendLine($"RemoteAvatarRoot = {remoteAvatarRoot.name} (owner={remoteView.ownerIDInHierarchy})");
        sb.AppendLine($"RemoteAvatarRoot path: {GetPath(remoteAvatarRoot)}");

        if (handTarget == null) {
            sb.AppendLine($"HandTarget '{handTargetName}' NOT found under remote avatar.");
            Debug.Log(sb.ToString());
            return;
        }

        if (hammer == null) {
            sb.AppendLine($"Hammer '{hammerName}' NOT found under HandTarget.");
            sb.AppendLine($"HandTarget path: {GetPath(handTarget)}");
            Debug.Log(sb.ToString());
            return;
        }

        // Print chain from GameRoot down to Hammer
        sb.AppendLine($"HandTarget path: {GetPath(handTarget)}");
        sb.AppendLine($"Hammer path:     {GetPath(hammer)}");
        sb.AppendLine("");
        sb.AppendLine("LocalPosition audit (each node's localPosition):");

        // Walk upward from hammer to scene root, printing locals
        Transform t = hammer;
        int depth = 0;
        while (t != null && depth < 30) {
            sb.AppendLine($"- {t.name}: localPos={t.localPosition:F4} localRotEuler={t.localEulerAngles:F2}");
            t = t.parent;
            depth++;
        }

        sb.AppendLine("==================================================");
        Debug.Log(sb.ToString());
    }

    static Transform FindDeepChild(Transform parent, string childName) {
        if (parent == null) return null;
        foreach (Transform child in parent) {
            if (child.name == childName) return child;
            var result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }

    static string GetPath(Transform t) {
        if (t == null) return "<null>";
        var sb = new StringBuilder(t.name);
        while (t.parent != null) {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}
