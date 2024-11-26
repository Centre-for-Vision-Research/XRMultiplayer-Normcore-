using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

public class MeshSyncComponent : RealtimeComponent<MeshSyncModel> {
    public Mesh[] availableMeshes; // Array of meshes to assign
    private MeshFilter meshFilter; // Reference to the MeshFilter component

    private void Start() {
        // Get the MeshFilter component on this GameObject
        meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter == null) {
            Debug.LogError("MeshSyncComponent requires a MeshFilter in its children.");
        }
    }

    protected override void OnRealtimeModelReplaced(MeshSyncModel previousModel, MeshSyncModel currentModel) {
        if (previousModel != null) {
            // Unsubscribe from events on the previous model
            previousModel.meshIndexDidChange -= OnMeshIndexChanged;
        }

        if (currentModel != null) {
            // If the model is new, assign the current mesh index
            if (currentModel.isFreshModel) {
                currentModel.meshIndex = Random.Range(0, availableMeshes.Length);
            }

            // Subscribe to events on the new model
            currentModel.meshIndexDidChange += OnMeshIndexChanged;

            // Update the mesh to match the current model
            UpdateMesh();
        }
    }

    private void OnMeshIndexChanged(MeshSyncModel model, int value) {
        UpdateMesh();
    }

    public void SetMeshIndex(int index) {
        if (model == null) return;

        // Set the mesh index in the model
        model.meshIndex = index;
    }

    private void UpdateMesh() {
        if (model == null || meshFilter == null || availableMeshes == null) return;

        int index = model.meshIndex;
        if (index >= 0 && index < availableMeshes.Length) {
            meshFilter.mesh = availableMeshes[index];
        } else {
            Debug.LogWarning("Invalid mesh index: " + index);
        }
    }
}

