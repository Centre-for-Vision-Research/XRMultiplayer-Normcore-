using UnityEngine;
using Normal.Realtime;

public class MRNetworkPosePublisher : MonoBehaviour
{
    public bool isMRScene = true;

    [Header("NetTargets (children under avatar)")]
    public Transform headNet;
    public Transform leftNet;
    public Transform rightNet;

    [Header("XR Target Names in Scene (optional)")]
    public string headXRName = "Head Camera Target";
    public string leftXRName = "Left Cont Target";
    public string rightXRName = "Right Cont Target";

    [Header("Robustness")]
    public float findRetryInterval = 0.5f;
    public bool logWhenMissing = false;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float smoothing = 0.25f;

    [Header("Anchor Stabilization")]
    [Tooltip("Extra delay after AnchorReady before publishing poses. Helps late joiners.")]
    public float publishDelayAfterAnchorReadySeconds = 1.0f;

    private Transform headXR;
    private Transform leftXR;
    private Transform rightXR;

    private RealtimeView view;
    private float nextFindTime = 0f;

    private bool didResetOnce = false;
    private float anchorReadySeenAt = -1f;

    void Start()
    {
        if (!isMRScene) return;

        view = GetComponent<RealtimeView>();
        if (view == null)
        {
            Debug.LogError("[MRNetworkPosePublisher] Missing RealtimeView.");
            enabled = false;
            return;
        }

        if (!view.isOwnedLocallySelf)
        {
            enabled = false;
            return;
        }

        TryFindTargets();
    }

    void Update()
    {
        if (!isMRScene) return;

        if (Time.time >= nextFindTime)
        {
            nextFindTime = Time.time + findRetryInterval;

            if (headXR == null || leftXR == null || rightXR == null)
                TryFindTargets();
        }
    }

    void LateUpdate()
    {
        if (!isMRScene || view == null || !view.isOwnedLocallySelf) return;

        // XR targets must exist
        if (headXR == null || leftXR == null || rightXR == null) return;

        // NetTargets must exist
        if (headNet == null || leftNet == null || rightNet == null) return;

        // Must have anchor + correct parenting
        if (!IsAnchoredAndParentedCorrectly()) return;

        // Optional stabilization delay for late joiners
        if (anchorReadySeenAt < 0f) anchorReadySeenAt = Time.time;
        if (Time.time - anchorReadySeenAt < publishDelayAfterAnchorReadySeconds) return;

        // Reset once AFTER anchored parenting is confirmed
        if (!didResetOnce)
        {
            ResetNetTargetsWorldToXR();
            didResetOnce = true;

            if (logWhenMissing)
                Debug.Log("[MRNetworkPosePublisher] Reset NetTargets world pose after anchor ready.");

            return; // skip first frame after reset
        }

        // Smooth world pose toward XR world pose
        float t = 1f - Mathf.Pow(1f - smoothing, Time.deltaTime * 60f);

        headNet.position  = Vector3.Lerp(headNet.position,  headXR.position,  t);
        leftNet.position  = Vector3.Lerp(leftNet.position,  leftXR.position,  t);
        rightNet.position = Vector3.Lerp(rightNet.position, rightXR.position, t);

        headNet.rotation  = Quaternion.Slerp(headNet.rotation,  headXR.rotation,  t);
        leftNet.rotation  = Quaternion.Slerp(leftNet.rotation,  leftXR.rotation,  t);
        rightNet.rotation = Quaternion.Slerp(rightNet.rotation, rightXR.rotation, t);
    }

    private bool IsAnchoredAndParentedCorrectly()
    {
        var mgr = MRSharedAnchorManager.Instance;
        if (mgr == null) return false;
        if (!mgr.AnchorReady) return false;

        Transform expectedParent = mgr.NetworkAvatarsParent;
        if (expectedParent == null) return false;

        // This avatar prefab root must live under the anchored NetworkAvatars parent
        return transform.IsChildOf(expectedParent);
    }

    private void ResetNetTargetsWorldToXR()
    {
        headNet.position  = headXR.position;
        leftNet.position  = leftXR.position;
        rightNet.position = rightXR.position;

        headNet.rotation  = headXR.rotation;
        leftNet.rotation  = leftXR.rotation;
        rightNet.rotation = rightXR.rotation;
    }

    private void TryFindTargets()
    {
        if (headXR == null)  headXR  = GameObject.Find(headXRName)?.transform;
        if (leftXR == null)  leftXR  = GameObject.Find(leftXRName)?.transform;
        if (rightXR == null) rightXR = GameObject.Find(rightXRName)?.transform;

        if (logWhenMissing && (headXR == null || leftXR == null || rightXR == null))
        {
            Debug.LogWarning($"[MRNetworkPosePublisher] Missing XR targets: head={headXR!=null} left={leftXR!=null} right={rightXR!=null}");
        }
    }
}
