using UnityEngine;
using Normal.Realtime;

public class MRHammerFollower : MonoBehaviour
{
    public bool isMRScene = true;

    [Header("Networked pivots (these have RealtimeView + RealtimeTransform)")]
    public Transform leftGripPivot;
    public Transform rightGripPivot;

    [Header("XR controller target names in scene")]
    public string leftXRName = "Left Cont Target";
    public string rightXRName = "Right Cont Target";

    [Header("Robustness")]
    public float findRetryInterval = 0.5f;
    public float startDelayAfterAnchorReady = 0.15f;
    public bool verboseLogs = false;

    private Transform leftXR;
    private Transform rightXR;

    private RealtimeView rootView;
    private float nextFindTime;
    private float anchorReadySeenAt = -1f;
    private bool ownershipRequested = false;

    void Start()
    {
        if (!isMRScene) return;

        rootView = GetComponent<RealtimeView>();
        if (rootView == null)
        {
            Debug.LogError("[MRHammerFollower] Missing RealtimeView on avatar root.");
            enabled = false;
            return;
        }

        // Only the locally owned avatar should drive pivots
        if (!rootView.isOwnedLocallySelf)
        {
            enabled = false;
            return;
        }

        TryFindXRTargets();
    }

    void Update()
    {
        if (!isMRScene) return;

        if (Time.time >= nextFindTime)
        {
            nextFindTime = Time.time + findRetryInterval;

            if (leftXR == null || rightXR == null)
                TryFindXRTargets();
        }
    }

    void LateUpdate()
    {
        if (!isMRScene) return;

        var mgr = MRSharedAnchorManager.Instance;
        if (mgr == null || !mgr.AnchorReady) return;

        // Ensure this avatar is under the anchored network parent on THIS client
        Transform expectedParent = mgr.NetworkAvatarsParent;
        if (expectedParent == null) return;
        if (!transform.IsChildOf(expectedParent)) return;

        if (anchorReadySeenAt < 0f)
        {
            anchorReadySeenAt = Time.time;
            if (verboseLogs) Debug.Log("[MRHammerFollower] Anchor ready and parenting ok. Starting delay gate.");
        }

        if (Time.time - anchorReadySeenAt < startDelayAfterAnchorReady) return;

        if (leftXR == null || rightXR == null) return;
        if (leftGripPivot == null || rightGripPivot == null) return;

        // Make sure we own pivot views too (safe even if already owned)
        if (!ownershipRequested)
        {
            RequestPivotOwnershipIfPresent(leftGripPivot);
            RequestPivotOwnershipIfPresent(rightGripPivot);
            ownershipRequested = true;

            if (verboseLogs) Debug.Log("[MRHammerFollower] Requested pivot ownership.");
        }

        WritePivotLocal(leftGripPivot, leftXR);
        WritePivotLocal(rightGripPivot, rightXR);
    }

    private void WritePivotLocal(Transform pivot, Transform xr)
    {
        Transform p = pivot.parent;
        if (p == null)
        {
            pivot.SetPositionAndRotation(xr.position, xr.rotation);
            return;
        }

        // Convert XR world pose into pivot LOCAL pose under its current parent.
        pivot.localPosition = p.InverseTransformPoint(xr.position);
        pivot.localRotation = Quaternion.Inverse(p.rotation) * xr.rotation;
    }

    private void RequestPivotOwnershipIfPresent(Transform pivot)
    {
        if (pivot == null) return;

        var rv = pivot.GetComponent<RealtimeView>();
        if (rv != null) rv.RequestOwnership();

        var rt = pivot.GetComponent<RealtimeTransform>();
        if (rt != null) rt.RequestOwnership();
    }

    private void TryFindXRTargets()
    {
        if (leftXR == null)
            leftXR = GameObject.Find(leftXRName)?.transform;

        if (rightXR == null)
            rightXR = GameObject.Find(rightXRName)?.transform;

        if (verboseLogs && (leftXR == null || rightXR == null))
        {
            Debug.Log($"[MRHammerFollower] XR targets found: left={leftXR != null}, right={rightXR != null}");
        }
    }
}
