using UnityEngine;
using Normal.Realtime;

public class MRNetworkPosePublisher : MonoBehaviour
{
    public bool isMRScene = true;

    public Transform headNet;
    public Transform leftNet;
    public Transform rightNet;

    public string headXRName = "Head Camera Target";
    public string leftXRName = "Left Cont Target";
    public string rightXRName = "Right Cont Target";

    private Transform headXR;
    private Transform leftXR;
    private Transform rightXR;

    private RealtimeView view;
    private Transform avatarRoot;

    void Start()
    {
        if (!isMRScene) return;

        view = GetComponent<RealtimeView>();
        if (view == null)
        {
            Debug.LogError("[MRNetworkPosePublisher] Missing RealtimeView.");
            return;
        }

        if (!view.isOwnedLocallySelf)
            return;

        avatarRoot = transform;

        headXR = GameObject.Find(headXRName)?.transform;
        leftXR = GameObject.Find(leftXRName)?.transform;
        rightXR = GameObject.Find(rightXRName)?.transform;

        if (headXR == null || leftXR == null || rightXR == null)
        {
            Debug.LogError("[MRNetworkPosePublisher] XR targets not found.");
            return;
        }
    }

    void LateUpdate()
    {
        if (!isMRScene || view == null || !view.isOwnedLocallySelf) return;

        headNet.localPosition = avatarRoot.InverseTransformPoint(headXR.position);
        leftNet.localPosition = avatarRoot.InverseTransformPoint(leftXR.position);
        rightNet.localPosition = avatarRoot.InverseTransformPoint(rightXR.position);

        headNet.localRotation = Quaternion.Inverse(avatarRoot.rotation) * headXR.rotation;
        leftNet.localRotation = Quaternion.Inverse(avatarRoot.rotation) * leftXR.rotation;
        rightNet.localRotation = Quaternion.Inverse(avatarRoot.rotation) * rightXR.rotation;
    }
}
