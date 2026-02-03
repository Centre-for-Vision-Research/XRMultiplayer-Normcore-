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

    [Tooltip("Name of the parent object under GameRoot that should contain avatars.")]
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

        Vector3 spawnOffset = Vector3.zero;

        GameObject selectedPrefab = GetPrefabForClientID(realtime.clientID);
        if (selectedPrefab == null)
        {
            Debug.LogError("[CustomAvatarManager] No available avatar prefabs to assign.");
            return;
        }

        avatarGameObject = Realtime.Instantiate(
            selectedPrefab.name,
            spawnOffset,
            Quaternion.identity,
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

        // Parent under NetworkAvatars in MR scene
        if (isMRScene)
        {
            GameObject parent = GameObject.Find(networkAvatarsObjectName);
            if (parent != null)
            {
                avatarGameObject.transform.SetParent(parent.transform, true);
                Debug.Log($"[CustomAvatarManager] Parented avatar under {networkAvatarsObjectName}.");
            }
            else
            {
                Debug.LogWarning($"[CustomAvatarManager] Could not find '{networkAvatarsObjectName}'. Avatar will remain unparented.");
            }
        }

        // Request ownership of the avatar's RealtimeView and all child RealtimeTransforms
        RealtimeView avatarRealtimeView = avatarGameObject.GetComponent<RealtimeView>();
        if (avatarRealtimeView != null)
        {
            RequestOwnershipOfAvatarAndChildren(avatarRealtimeView);
        }
        else
        {
            Debug.LogError("[CustomAvatarManager] RealtimeView not found on the avatar prefab root.");
        }
    }

    private GameObject GetPrefabForClientID(int clientID)
    {
        if (avatarPrefabs == null || avatarPrefabs.Count == 0)
            return null;

        if (SceneManager.GetActiveScene().name.Contains("HighFid") && avatarConfig != null)
        {
            int index = avatarConfig.bodyType != null && avatarConfig.bodyType.ToLower() == "female" ? 0 : 1;

            if (index >= 0 && index < avatarPrefabs.Count)
            {
                return avatarPrefabs[index];
            }
            else
            {
                Debug.LogWarning("[CustomAvatarManager] Config-selected index out of range. Falling back to clientID-based assignment.");
            }
        }

        int fallbackIndex = clientID % avatarPrefabs.Count;
        return avatarPrefabs[fallbackIndex];
    }

    private void RequestOwnershipOfAvatarAndChildren(RealtimeView realtimeView)
    {
        realtimeView.RequestOwnership();

        RealtimeTransform realtimeTransform = realtimeView.GetComponent<RealtimeTransform>();
        if (realtimeTransform != null)
            realtimeTransform.RequestOwnership();

        foreach (Transform child in realtimeView.transform)
        {
            RealtimeView childRealtimeView = child.GetComponent<RealtimeView>();
            if (childRealtimeView != null)
            {
                RequestOwnershipOfAvatarAndChildren(childRealtimeView);
            }
            else
            {
                RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
                if (childRealtimeTransform != null)
                    childRealtimeTransform.RequestOwnership();

                if (child.childCount > 0)
                    RequestOwnershipOfChildren(child);
            }
        }
    }

    private void RequestOwnershipOfChildren(Transform parent)
    {
        foreach (Transform child in parent)
        {
            RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
            if (childRealtimeTransform != null)
                childRealtimeTransform.RequestOwnership();

            if (child.childCount > 0)
                RequestOwnershipOfChildren(child);
        }
    }
}








//using UnityEngine;
//using Normal.Realtime;
//using System.Collections.Generic;
//using UnityEngine.SceneManagement;

//public class CustomAvatarManager : MonoBehaviour {
//    public Realtime realtime;
//    public List<GameObject> avatarPrefabs; // Multiple prefabs
//    private GameObject avatarGameObject;
//    public AvatarConfigData avatarConfig;


//    void Start() {
//        // Ensure we have a reference to Realtime
//        if (realtime == null)
//            realtime = FindObjectOfType<Realtime>();

//        if (realtime == null) {
//            Debug.LogError("Realtime component not found in the scene.");
//            return;
//        }

//        // Subscribe to the didConnectToRoom event
//        realtime.didConnectToRoom += DidConnectToRoom;
//    }

//    void OnDestroy() {
//        // Unsubscribe from the event when this object is destroyed
//        if (realtime != null)
//            realtime.didConnectToRoom -= DidConnectToRoom;
//    }

//    private void DidConnectToRoom(Realtime room) {
//        // Calculate a unique spawn position
//        Vector3 spawnOffset = GetUniqueSpawnOffset();

//        // Get the avatar prefab based on Normcore's clientID
//        GameObject selectedPrefab = GetPrefabForClientID(realtime.clientID);

//        if (selectedPrefab == null) {
//            Debug.LogError("No available avatar prefabs to assign.");
//            return;
//        }

//        // Instantiate the selected avatar prefab at the spawn position
//        avatarGameObject = Realtime.Instantiate(selectedPrefab.name, spawnOffset, Quaternion.identity, new Realtime.InstantiateOptions {
//            ownedByClient               = true,
//            preventOwnershipTakeover    = true,
//            destroyWhenOwnerLeaves      = true,
//            destroyWhenLastClientLeaves = true,
//            useInstance                 = realtime,
//        });

//        if (avatarGameObject == null) {
//            Debug.LogError("Failed to instantiate avatar prefab.");
//            return;
//        }

//        // Request ownership of the avatar's RealtimeView and all child RealtimeTransforms
//        RealtimeView avatarRealtimeView = avatarGameObject.GetComponent<RealtimeView>();
//        if (avatarRealtimeView != null) {
//            RequestOwnershipOfAvatarAndChildren(avatarRealtimeView);
//        } else {
//            Debug.LogError("RealtimeView not found on the avatar prefab.");
//        }
//    }

//    private GameObject GetPrefabForClientID(int clientID) {
//        if (avatarPrefabs.Count == 0)
//            return null;

//        // Use saved selection in high-fid scenes
//        if (SceneManager.GetActiveScene().name.Contains("HighFid") && avatarConfig != null) {
//            int index = avatarConfig.bodyType.ToLower() == "female" ? 0 : 1;

//            if (index >= 0 && index < avatarPrefabs.Count) {
//                return avatarPrefabs[index];
//            } else {
//                Debug.LogWarning("Config-selected index out of range, falling back to clientID-based assignment.");
//            }
//        }

//        // Low-fid fallback or invalid config
//        int fallbackIndex = clientID % avatarPrefabs.Count;
//        return avatarPrefabs[fallbackIndex];
//    }



//    private void RequestOwnershipOfAvatarAndChildren(RealtimeView realtimeView) {
//        // Request ownership of the RealtimeView
//        realtimeView.RequestOwnership();

//        // Request ownership of any RealtimeTransform on this GameObject
//        RealtimeTransform realtimeTransform = realtimeView.GetComponent<RealtimeTransform>();
//        if (realtimeTransform != null)
//            realtimeTransform.RequestOwnership();

//        // Recursively request ownership of child RealtimeViews and RealtimeTransforms
//        foreach (Transform child in realtimeView.transform) {
//            RealtimeView childRealtimeView = child.GetComponent<RealtimeView>();
//            if (childRealtimeView != null) {
//                RequestOwnershipOfAvatarAndChildren(childRealtimeView);
//            } else {
//                // If there's no RealtimeView, check for RealtimeTransform
//                RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
//                if (childRealtimeTransform != null)
//                    childRealtimeTransform.RequestOwnership();

//                // Continue recursion
//                if (child.childCount > 0)
//                    RequestOwnershipOfChildren(child);
//            }
//        }
//    }

//    private void RequestOwnershipOfChildren(Transform parent) {
//        foreach (Transform child in parent) {
//            RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
//            if (childRealtimeTransform != null)
//                childRealtimeTransform.RequestOwnership();

//            // Recursively request ownership of deeper children
//            if (child.childCount > 0)
//                RequestOwnershipOfChildren(child);
//        }
//    }

//    private Vector3 GetUniqueSpawnOffset() {
//        int playerID = realtime.clientID;
//        float spacing = 2.0f; // Adjust spacing between players as needed
//        return new Vector3(playerID * spacing, 0, 0);
//    }
//}