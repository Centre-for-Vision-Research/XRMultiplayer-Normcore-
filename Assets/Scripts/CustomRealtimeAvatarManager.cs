using UnityEngine;
using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class CustomAvatarManager : MonoBehaviour
{
    [Header("Normcore")]
    public Realtime realtime;

    [Header("Avatar Prefabs")]
    public List<GameObject> avatarPrefabs;
    private GameObject avatarGameObject;
    public AvatarConfigData avatarConfig;

    [Header("Scene Mode")]
    [Tooltip("Enable ONLY in MR scenes.")]
    public bool isMRScene = false;

    [Header("VR Spawn (only affects initial placement, local IK will override)")]
    public float secondPlayerZOffset = 2.2f;

    [Header("MR Pivot Names (must match prefab children exactly)")]
    public string leftPivotName = "LeftGripPivot";
    public string rightPivotName = "RightGripPivot";
    public bool requestPivotOwnership = true;

    [Tooltip("Max time to wait for MRSharedAnchorManager.Instance to exist.")]
    public float waitForSSAManagerSeconds = 10f;

    [Header("Debug")]
    public bool verboseLogs = true;

    void Start()
    {
        if (realtime == null)
            realtime = FindObjectOfType<Realtime>();

        if (realtime == null)
        {
            Debug.LogError("[CustomAvatarManager] Realtime not found in scene.");
            return;
        }

        realtime.didConnectToRoom += DidConnectToRoom;
    }

    void OnDestroy()
    {
        if (realtime != null)
            realtime.didConnectToRoom -= DidConnectToRoom;
    }

    private void DidConnectToRoom(Realtime room)
    {
        if (verboseLogs)
            Debug.Log($"[CustomAvatarManager] Connected. cid={realtime.clientID} isMRScene={isMRScene}");

        GameObject selectedPrefab = GetPrefabForClientID(realtime.clientID);
        if (selectedPrefab == null)
        {
            Debug.LogError("[CustomAvatarManager] No avatar prefab found.");
            return;
        }

        // Initial spawn pose
        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        // VR: place second player 2.2m forward and rotate 180 degrees
        if (!isMRScene && (realtime.clientID % 2 == 1))
        {
            spawnPos = new Vector3(0f, 0f, secondPlayerZOffset);
            spawnRot = Quaternion.Euler(0f, 180f, 0f);
        }

        avatarGameObject = Realtime.Instantiate(
            selectedPrefab.name,
            spawnPos,
            spawnRot,
            new Realtime.InstantiateOptions
            {
                ownedByClient = true,
                preventOwnershipTakeover = true,
                destroyWhenOwnerLeaves = true,
                destroyWhenLastClientLeaves = true,
                useInstance = realtime,
            }
        );

        if (avatarGameObject == null)
        {
            Debug.LogError("[CustomAvatarManager] Realtime.Instantiate failed.");
            return;
        }

        // IMPORTANT: restore deep ownership so local scripts can drive IK targets / pivots / any network transforms
        RequestOwnershipDeep(avatarGameObject.transform);

        // MR: register for anchor reparenting
        if (isMRScene)
        {
            StartCoroutine(RegisterWithSSAWhenAvailable(avatarGameObject.transform));

            if (requestPivotOwnership)
            {
                RequestOwnershipForPivotIfPresent(avatarGameObject.transform, leftPivotName, "LeftGripPivot");
                RequestOwnershipForPivotIfPresent(avatarGameObject.transform, rightPivotName, "RightGripPivot");
            }
        }
    }

    private void RequestOwnershipDeep(Transform root)
    {
        if (root == null) return;

        // Own all views (safe since preventOwnershipTakeover=true)
        var views = root.GetComponentsInChildren<RealtimeView>(true);
        foreach (var v in views) v.RequestOwnership();

        // Own all transforms so your local IK scripts can publish motion
        var rts = root.GetComponentsInChildren<RealtimeTransform>(true);
        foreach (var rt in rts) rt.RequestOwnership();

        if (verboseLogs)
            Debug.Log($"[CustomAvatarManager] Requested deep ownership. views={views.Length} rts={rts.Length}");
    }

    private IEnumerator RegisterWithSSAWhenAvailable(Transform avatarRoot)
    {
        float start = Time.time;

        while (MRSharedAnchorManager.Instance == null)
        {
            if (Time.time - start > waitForSSAManagerSeconds)
            {
                Debug.LogWarning("[CustomAvatarManager] Timed out waiting for MRSharedAnchorManager. Avatar will rely on tag scan reparent.");
                yield break;
            }
            yield return null;
        }

        MRSharedAnchorManager.Instance.RegisterAvatarForReparent(avatarRoot);
    }

    private void RequestOwnershipForPivotIfPresent(Transform avatarRoot, string pivotChildName, string labelForLogs)
    {
        Transform pivot = avatarRoot.Find(pivotChildName);
        if (pivot == null)
        {
            Debug.LogWarning($"[CustomAvatarManager] {labelForLogs} not found. Expected child '{pivotChildName}'.");
            return;
        }

        var pv = pivot.GetComponent<RealtimeView>();
        if (pv != null) pv.RequestOwnership();

        var prt = pivot.GetComponent<RealtimeTransform>();
        if (prt != null) prt.RequestOwnership();
    }

    private GameObject GetPrefabForClientID(int clientID)
    {
        if (avatarPrefabs == null || avatarPrefabs.Count == 0)
            return null;

        // Use saved selection in high-fid scenes
        if (SceneManager.GetActiveScene().name.Contains("HighFid") && avatarConfig != null)
        {
            int index = (avatarConfig.bodyType != null && avatarConfig.bodyType.ToLower() == "female") ? 0 : 1;
            if (index >= 0 && index < avatarPrefabs.Count)
                return avatarPrefabs[index];
        }

        int fallbackIndex = clientID % avatarPrefabs.Count;
        return avatarPrefabs[fallbackIndex];
    }
}
