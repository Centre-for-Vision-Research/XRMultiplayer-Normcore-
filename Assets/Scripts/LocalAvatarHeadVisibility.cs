using UnityEngine;
using Normal.Realtime;

public class LocalAvatarHeadVisibility : MonoBehaviour
{
    private RealtimeView realtimeView;

    [Tooltip("List of renderers to hide for the local player.")]
    [SerializeField] private SkinnedMeshRenderer[] renderersToCull; // Renderers assigned via the Inspector

    [Tooltip("Layer to assign to local avatar renderers for culling.")]
    [SerializeField] private string cullingLayerName = "LocalAvatarCulling"; // Layer name

    private int cullingLayer;

    void Start()
    {
        // Get the RealtimeView component to check ownership
        realtimeView = GetComponent<RealtimeView>();

        if (realtimeView == null)
        {
            Debug.LogError("RealtimeView component not found on the avatar.");
            return;
        }

        // Fetch the layer ID
        cullingLayer = LayerMask.NameToLayer(cullingLayerName);

        if (cullingLayer == -1)
        {
            Debug.LogError($"Layer '{cullingLayerName}' not found. Please add it to the Layers in Project Settings.");
            return;
        }

        // Check if this avatar is owned by the local player
        if (realtimeView.isOwnedLocally)
        {
            AssignCullingLayer();
        }
    }

    private void AssignCullingLayer()
    {
        if (renderersToCull == null || renderersToCull.Length == 0) return;

        foreach (var renderer in renderersToCull)
        {
            if (renderer != null)
            {
                // Assign the culling layer to the renderer's GameObject
                renderer.gameObject.layer = cullingLayer;
                Debug.Log($"Assigned {renderer.gameObject.name} to layer {cullingLayerName}.");
            }
        }
    }
}
