using System.Collections;
using UnityEngine;
using Normal.Realtime;

public class VRSpawnOffsetAfterLocalAvatar : MonoBehaviour
{
    [Header("Assign in Inspector")]
    public Transform playerRoot; // Player object that parents XROrigin

    [Header("Student Offset")]
    public float studentZ = 2.2f;
    public float studentYaw = 180f;

    [Header("Debug")]
    public bool verboseLogs = true;

    private bool _applied = false;

    private IEnumerator Start()
    {
        if (playerRoot == null)
        {
            Debug.LogError("[VRSpawnOffset] PlayerRoot not assigned.");
            yield break;
        }

        // Wait RoleManager
        yield return new WaitUntil(() => RoleManager.Instance != null);

        // Wait Realtime connection
        var realtime = FindObjectOfType<Realtime>();
        yield return new WaitUntil(() => realtime != null && realtime.clientID >= 0);

        // Wait until local avatar exists
        yield return new WaitUntil(LocalOwnedAvatarExists);

        // Critical: let XR + IK finish one frame
        yield return null;

        int cid = realtime.clientID;

        // STRICT: student only
        if (!RoleManager.Instance.IsStudent(cid))
        {
            if (verboseLogs)
                Debug.Log($"[VRSpawnOffset] Not student. No offset applied. cid={cid}");
            yield break;
        }

        // Apply student pose
        Vector3 p = playerRoot.position;
        playerRoot.position = new Vector3(0f, p.y, studentZ);
        playerRoot.rotation = Quaternion.Euler(0f, studentYaw, 0f);

        _applied = true;

        if (verboseLogs)
            Debug.Log($"[VRSpawnOffset] STUDENT offset applied pos=(0,{p.y:F3},{studentZ}) rot=(0,{studentYaw},0)");
    }

    private bool LocalOwnedAvatarExists()
    {
        if (_applied) return true;

        var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        foreach (var av in avatars)
        {
            var rv = av.GetComponent<RealtimeView>();
            if (rv != null && rv.isOwnedLocallySelf)
                return true;
        }
        return false;
    }
}
