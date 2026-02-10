using System.Collections.Generic;
using UnityEngine;

public class AvatarRigRefs : MonoBehaviour
{
    [Header("Auto-resolve XR targets by name (recommended)")]
    public string headTargetName = "Head Camera Target";
    public string leftTargetName = "Left Cont Target";
    public string rightTargetName = "Right Cont Target";

    [Tooltip("How often (seconds) to re-scan if refs are missing or inactive.")]
    public float rescanInterval = 0.5f;

    [Header("Resolved (read-only at runtime)")]
    public Transform head;
    public Transform leftController;
    public Transform rightController;

    [Header("Auto-found by tag Hammer (colliders under this avatar)")]
    public List<Collider> hammerColliders = new List<Collider>(2);

    private float _nextScanTime;

    private void Awake()
    {
        CacheHammerColliders();
        TryResolveTargets(force: true);
    }

    private void OnEnable()
    {
        // In case avatars enable after spawning or after MR reparent
        _nextScanTime = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextScanTime) return;

        // Re-scan if missing or became inactive
        if (!IsUsable(head) || !IsUsable(leftController) || !IsUsable(rightController))
        {
            TryResolveTargets(force: false);
        }

        _nextScanTime = Time.unscaledTime + rescanInterval;
    }

    private bool IsUsable(Transform t)
    {
        // Usable means non-null and active in hierarchy
        return t != null && t.gameObject.activeInHierarchy;
    }

    private void TryResolveTargets(bool force)
    {
        // If already usable and not forcing, skip
        if (!force &&
            IsUsable(head) &&
            IsUsable(leftController) &&
            IsUsable(rightController))
            return;

        // Search within this avatar first (fast, correct for remote avatars too)
        if (!IsUsable(head)) head = FindDeepChildByName(transform, headTargetName);
        if (!IsUsable(leftController)) leftController = FindDeepChildByName(transform, leftTargetName);
        if (!IsUsable(rightController)) rightController = FindDeepChildByName(transform, rightTargetName);

        // If still missing, do nothing. Next rescan will try again.
    }

    private Transform FindDeepChildByName(Transform root, string exactName)
    {
        if (root == null || string.IsNullOrEmpty(exactName)) return null;

        // include inactive
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == exactName)
                return all[i];
        }
        return null;
    }

    private void CacheHammerColliders()
    {
        if (hammerColliders == null) hammerColliders = new List<Collider>(2);
        hammerColliders.Clear();

        // include inactive
        var cols = GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null && c.CompareTag("Hammer"))
                hammerColliders.Add(c);
        }
    }

    public Vector3 GetRolePositionFallback()
    {
        // Use head target if we have it, otherwise avatar root
        if (IsUsable(head)) return head.position;
        return transform.position;
    }
}
