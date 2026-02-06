using UnityEngine;
using Normal.Realtime;
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

    [Tooltip("Name of the parent object that should contain avatars in MR (child under NetworkSpaceRoot).")]
    public string networkAvatarsObjectName = "NetworkAvatars";

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

        if (isMRScene)
        {
            // Robust: let SSA manager handle parenting when anchor becomes ready.
            if (MRSharedAnchorManager.Instance != null)
            {
                MRSharedAnchorManager.Instance.RegisterAvatarForReparent(avatarGameObject.transform);
            }
            else
            {
                // If SSA not initialized yet, do nothing here.
                // The avatar will be reparented once SSA exists and scans pending avatars,
                // OR you can keep it under scene root temporarily.
                avatarGameObject.transform.SetParent(null, true);
            }
        }

        RealtimeView avatarRealtimeView = avatarGameObject.GetComponent<RealtimeView>();
        if (avatarRealtimeView != null)
            RequestOwnershipOfAvatarAndChildren(avatarRealtimeView);
        else
            Debug.LogError("[CustomAvatarManager] RealtimeView not found on the avatar prefab root.");
    }


    private GameObject GetPrefabForClientID(int clientID)
    {
        if (avatarPrefabs == null || avatarPrefabs.Count == 0)
            return null;

        if (SceneManager.GetActiveScene().name.Contains("HighFid") && avatarConfig != null)
        {
            int index = avatarConfig.bodyType != null && avatarConfig.bodyType.ToLower() == "female" ? 0 : 1;
            if (index >= 0 && index < avatarPrefabs.Count)
                return avatarPrefabs[index];
        }

        int fallbackIndex = clientID % avatarPrefabs.Count;
        return avatarPrefabs[fallbackIndex];
    }

    private void RequestOwnershipOfAvatarAndChildren(RealtimeView realtimeView)
    {
        realtimeView.RequestOwnership();

        RealtimeTransform rt = realtimeView.GetComponent<RealtimeTransform>();
        if (rt != null) rt.RequestOwnership();

        foreach (Transform child in realtimeView.transform)
        {
            RealtimeView childView = child.GetComponent<RealtimeView>();
            if (childView != null)
                RequestOwnershipOfAvatarAndChildren(childView);
            else
            {
                RealtimeTransform childRt = child.GetComponent<RealtimeTransform>();
                if (childRt != null) childRt.RequestOwnership();

                if (child.childCount > 0)
                    RequestOwnershipOfChildren(child);
            }
        }
    }

    private void RequestOwnershipOfChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            RealtimeTransform rt = child.GetComponent<RealtimeTransform>();
            if (rt != null) rt.RequestOwnership();

            if (child.childCount > 0)
                RequestOwnershipOfChildren(child);
        }
    }
}



// using UnityEngine;
// using Normal.Realtime;
// using System.Collections.Generic;
// using UnityEngine.SceneManagement;

// public class CustomAvatarManager : MonoBehaviour
// {
//     [Header("Normcore")]
//     public Realtime realtime;

//     [Header("Avatar Prefabs")]
//     public List<GameObject> avatarPrefabs;
//     private GameObject avatarGameObject;
//     public AvatarConfigData avatarConfig;

//     [Header("MR Settings")]
//     [Tooltip("Enable ONLY in MR scenes.")]
//     public bool isMRScene = false;

//     [Tooltip("Name of the parent object under GameRoot that should contain avatars.")]
//     public string networkAvatarsObjectName = "NetworkAvatars";

//     void Start()
//     {
//         if (realtime == null)
//             realtime = FindObjectOfType<Realtime>();

//         if (realtime == null)
//         {
//             Debug.LogError("[CustomAvatarManager] Realtime component not found in the scene.");
//             return;
//         }

//         realtime.didConnectToRoom += DidConnectToRoom;
//         Debug.Log("[CustomAvatarManager] Subscribed to didConnectToRoom.");
//     }

//     void OnDestroy()
//     {
//         if (realtime != null)
//             realtime.didConnectToRoom -= DidConnectToRoom;
//     }

//     private void DidConnectToRoom(Realtime room)
//     {
//         Debug.Log($"[CustomAvatarManager] Connected to room. clientID={realtime.clientID} isMRScene={isMRScene}");

//         Vector3 spawnOffset = Vector3.zero;

//         GameObject selectedPrefab = GetPrefabForClientID(realtime.clientID);
//         if (selectedPrefab == null)
//         {
//             Debug.LogError("[CustomAvatarManager] No available avatar prefabs to assign.");
//             return;
//         }

//         avatarGameObject = Realtime.Instantiate(
//             selectedPrefab.name,
//             spawnOffset,
//             Quaternion.identity,
//             new Realtime.InstantiateOptions
//             {
//                 ownedByClient = true,
//                 preventOwnershipTakeover = true,
//                 destroyWhenOwnerLeaves = true,
//                 destroyWhenLastClientLeaves = true,
//                 useInstance = realtime,
//             }
//         );

//         if (avatarGameObject == null)
//         {
//             Debug.LogError("[CustomAvatarManager] Failed to instantiate avatar prefab.");
//             return;
//         }

//         // Request ownership of the avatar's RealtimeView and all child RealtimeTransforms
//         RealtimeView avatarRealtimeView = avatarGameObject.GetComponent<RealtimeView>();
//         if (avatarRealtimeView != null)
//         {
//             RequestOwnershipOfAvatarAndChildren(avatarRealtimeView);
//         }
//         else
//         {
//             Debug.LogError("[CustomAvatarManager] RealtimeView not found on the avatar prefab root.");
//         }
//     }

//     private GameObject GetPrefabForClientID(int clientID)
//     {
//         if (avatarPrefabs == null || avatarPrefabs.Count == 0)
//             return null;

//         if (SceneManager.GetActiveScene().name.Contains("HighFid") && avatarConfig != null)
//         {
//             int index = avatarConfig.bodyType != null && avatarConfig.bodyType.ToLower() == "female" ? 0 : 1;

//             if (index >= 0 && index < avatarPrefabs.Count)
//             {
//                 return avatarPrefabs[index];
//             }
//             else
//             {
//                 Debug.LogWarning("[CustomAvatarManager] Config-selected index out of range. Falling back to clientID-based assignment.");
//             }
//         }

//         int fallbackIndex = clientID % avatarPrefabs.Count;
//         return avatarPrefabs[fallbackIndex];
//     }

//     private void RequestOwnershipOfAvatarAndChildren(RealtimeView realtimeView)
//     {
//         realtimeView.RequestOwnership();

//         RealtimeTransform realtimeTransform = realtimeView.GetComponent<RealtimeTransform>();
//         if (realtimeTransform != null)
//             realtimeTransform.RequestOwnership();

//         foreach (Transform child in realtimeView.transform)
//         {
//             RealtimeView childRealtimeView = child.GetComponent<RealtimeView>();
//             if (childRealtimeView != null)
//             {
//                 RequestOwnershipOfAvatarAndChildren(childRealtimeView);
//             }
//             else
//             {
//                 RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
//                 if (childRealtimeTransform != null)
//                     childRealtimeTransform.RequestOwnership();

//                 if (child.childCount > 0)
//                     RequestOwnershipOfChildren(child);
//             }
//         }
//     }

//     private void RequestOwnershipOfChildren(Transform parent)
//     {
//         foreach (Transform child in parent)
//         {
//             RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
//             if (childRealtimeTransform != null)
//                 childRealtimeTransform.RequestOwnership();

//             if (child.childCount > 0)
//                 RequestOwnershipOfChildren(child);
//         }
//     }
// }


