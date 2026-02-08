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

    [Header("MR Settings")]
    [Tooltip("Enable ONLY in MR scenes.")]
    public bool isMRScene = false;

    [Header("MR Pivot Names (must match prefab children exactly)")]
    public string leftPivotName = "LeftGripPivot";
    public string rightPivotName = "RightGripPivot";

    [Tooltip("If true, explicitly request ownership on the pivots' RealtimeView/RealtimeTransform.")]
    public bool requestPivotOwnership = true;

    [Tooltip("Max time to wait for MRSharedAnchorManager.Instance to exist.")]
    public float waitForSSAManagerSeconds = 10f;

    void Start()
    {
        if (realtime == null)
            realtime = FindObjectOfType<Realtime>();

        if (realtime == null)
        {
            Debug.LogError("[CustomAvatarManager] Realtime component not found in the scene.");
            return;
        }

        realtime.didConnectToRoom += DidConnectToRoom;
        Debug.Log("[CustomAvatarManager] Subscribed to didConnectToRoom.");
    }

    void OnDestroy()
    {
        if (realtime != null)
            realtime.didConnectToRoom -= DidConnectToRoom;
    }

    private void DidConnectToRoom(Realtime room)
    {
        Debug.Log($"[CustomAvatarManager] Connected to room. clientID={realtime.clientID} isMRScene={isMRScene}");

        Vector3 spawnPos = Vector3.zero;
        Quaternion spawnRot = Quaternion.identity;

        GameObject selectedPrefab = GetPrefabForClientID(realtime.clientID);
        if (selectedPrefab == null)
        {
            Debug.LogError("[CustomAvatarManager] No available avatar prefabs to assign.");
            return;
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
            Debug.LogError("[CustomAvatarManager] Failed to instantiate avatar prefab.");
            return;
        }

        // MR: let SSA manager do reparenting when it becomes available and anchor becomes ready.
        if (isMRScene)
        {
            StartCoroutine(RegisterWithSSAWhenAvailable(avatarGameObject.transform));

            if (requestPivotOwnership)
                RequestOwnershipForPivotIfPresent(avatarGameObject.transform, leftPivotName, "LeftGripPivot");

            if (requestPivotOwnership)
                RequestOwnershipForPivotIfPresent(avatarGameObject.transform, rightPivotName, "RightGripPivot");
        }

        // In most cases this is not necessary because ownedByClient=true already owns the root view.
        // But it is harmless if the root has a RealtimeView and you want to be explicit.
        var rootView = avatarGameObject.GetComponent<RealtimeView>();
        if (rootView != null) rootView.RequestOwnership();
    }

    private IEnumerator RegisterWithSSAWhenAvailable(Transform avatarRoot)
    {
        float start = Time.time;

        while (MRSharedAnchorManager.Instance == null)
        {
            if (Time.time - start > waitForSSAManagerSeconds)
            {
                Debug.LogWarning("[CustomAvatarManager] Timed out waiting for MRSharedAnchorManager.Instance. Avatar stays under scene root until manager scans by tag.");
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
            Debug.LogWarning($"[CustomAvatarManager] {labelForLogs} not found under avatar root. Expected child named '{pivotChildName}'.");
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
