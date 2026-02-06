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
    public bool logWhenMissing = true;

    private Transform headXR;
    private Transform leftXR;
    private Transform rightXR;

    private RealtimeView view;
    private float nextFindTime = 0f;
    private bool warnedOnce = false;

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

        // Only publish for local-owned avatar
        if (!view.isOwnedLocallySelf)
        {
            enabled = false;
            return;
        }

        TryFindTargets(forceLog: false);
    }

    void Update()
    {
        if (!isMRScene) return;

        if (Time.time >= nextFindTime)
        {
            nextFindTime = Time.time + findRetryInterval;

            // Keep trying until we have the targets
            if (headXR == null || leftXR == null || rightXR == null)
                TryFindTargets(forceLog: false);
        }
    }

    [Range(0f, 1f)] public float smoothing = 0.25f; // add field

    void LateUpdate()
    {
        if (!isMRScene || view == null || !view.isOwnedLocallySelf) return;
        if (MRSharedAnchorManager.Instance == null || !MRSharedAnchorManager.Instance.AnchorReady) return;

        Transform anchor = MRSharedAnchorManager.Instance.AnchorTransform;
        if (anchor == null) return;

        if (headXR == null || leftXR == null || rightXR == null) return;

        Vector3 headP  = anchor.InverseTransformPoint(headXR.position);
        Vector3 leftP  = anchor.InverseTransformPoint(leftXR.position);
        Vector3 rightP = anchor.InverseTransformPoint(rightXR.position);

        Quaternion headR  = Quaternion.Inverse(anchor.rotation) * headXR.rotation;
        Quaternion leftR  = Quaternion.Inverse(anchor.rotation) * leftXR.rotation;
        Quaternion rightR = Quaternion.Inverse(anchor.rotation) * rightXR.rotation;

        float t = 1f - Mathf.Pow(1f - smoothing, Time.deltaTime * 60f);

        headNet.localPosition  = Vector3.Lerp(headNet.localPosition,  headP,  t);
        leftNet.localPosition  = Vector3.Lerp(leftNet.localPosition,  leftP,  t);
        rightNet.localPosition = Vector3.Lerp(rightNet.localPosition, rightP, t);

        headNet.localRotation  = Quaternion.Slerp(headNet.localRotation,  headR,  t);
        leftNet.localRotation  = Quaternion.Slerp(leftNet.localRotation,  leftR,  t);
        rightNet.localRotation = Quaternion.Slerp(rightNet.localRotation, rightR, t);
    }


    private void TryFindTargets(bool forceLog)
    {
        if (headXR == null)
            headXR = GameObject.Find(headXRName)?.transform;
        if (leftXR == null)
            leftXR = GameObject.Find(leftXRName)?.transform;
        if (rightXR == null)
            rightXR = GameObject.Find(rightXRName)?.transform;

        if ((forceLog || logWhenMissing) && (headXR == null || leftXR == null || rightXR == null))
        {
            // Only mild logging, no error spam
            // This is normal when controllers connect late on device
        }
    }
}
