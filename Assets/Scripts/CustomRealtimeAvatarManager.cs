using UnityEngine;
using Normal.Realtime;

public class CustomAvatarManager : MonoBehaviour {
    public Realtime realtime;
    public GameObject avatarPrefab;
    private GameObject avatarGameObject;

    void Start() {
        // Ensure we have a reference to Realtime
        if (realtime == null)
            realtime = FindObjectOfType<Realtime>();

        if (realtime == null) {
            Debug.LogError("Realtime component not found in the scene.");
            return;
        }

        // Subscribe to the didConnectToRoom event
        realtime.didConnectToRoom += DidConnectToRoom;
    }

    void OnDestroy() {
        // Unsubscribe from the event when this object is destroyed
        if (realtime != null)
            realtime.didConnectToRoom -= DidConnectToRoom;
    }

    private void DidConnectToRoom(Realtime room) {
        // Calculate a unique spawn position
        Vector3 spawnOffset = GetUniqueSpawnOffset();

        // Instantiate the avatar prefab at the spawn position
        avatarGameObject = Realtime.Instantiate(avatarPrefab.name, spawnOffset, Quaternion.identity, new Realtime.InstantiateOptions {
            ownedByClient               = true,
            preventOwnershipTakeover    = true,
            destroyWhenOwnerLeaves      = true,
            destroyWhenLastClientLeaves = true,
            useInstance                 = realtime,
        });

        if (avatarGameObject == null) {
            Debug.LogError("Failed to instantiate avatar prefab.");
            return;
        }
        // Request ownership of the avatar's RealtimeView and all child RealtimeTransforms
        RealtimeView avatarRealtimeView = avatarGameObject.GetComponent<RealtimeView>();
        if (avatarRealtimeView != null) {
            RequestOwnershipOfAvatarAndChildren(avatarRealtimeView);
        } else {
            Debug.LogError("RealtimeView not found on the avatar prefab.");
        }
    }

    private void RequestOwnershipOfAvatarAndChildren(RealtimeView realtimeView) {
        // Request ownership of the RealtimeView
        realtimeView.RequestOwnership();

        // Request ownership of any RealtimeTransform on this GameObject
        RealtimeTransform realtimeTransform = realtimeView.GetComponent<RealtimeTransform>();
        if (realtimeTransform != null)
            realtimeTransform.RequestOwnership();

        // Recursively request ownership of child RealtimeViews and RealtimeTransforms
        foreach (Transform child in realtimeView.transform) {
            RealtimeView childRealtimeView = child.GetComponent<RealtimeView>();
            if (childRealtimeView != null) {
                RequestOwnershipOfAvatarAndChildren(childRealtimeView);
            } else {
                // If there's no RealtimeView, check for RealtimeTransform
                RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
                if (childRealtimeTransform != null)
                    childRealtimeTransform.RequestOwnership();

                // Continue recursion
                if (child.childCount > 0)
                    RequestOwnershipOfChildren(child);
            }
        }
    }

    private void RequestOwnershipOfChildren(Transform parent) {
        foreach (Transform child in parent) {
            RealtimeTransform childRealtimeTransform = child.GetComponent<RealtimeTransform>();
            if (childRealtimeTransform != null)
                childRealtimeTransform.RequestOwnership();

            // Recursively request ownership of deeper children
            if (child.childCount > 0)
                RequestOwnershipOfChildren(child);
        }
    }

    private Vector3 GetUniqueSpawnOffset() {
        int playerID = realtime.clientID;
        float spacing = 2.0f; // Adjust spacing between players as needed
        return new Vector3(playerID * spacing, 0, 0);
    }
}

