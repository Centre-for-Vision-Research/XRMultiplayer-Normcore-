using UnityEngine;
using Normal.Realtime;
using System.Collections.Generic;

public class MRRemoteAvatarMirrorPlacer : MonoBehaviour
{
    [Header("Enable only in MR")]
    public bool isMRScene = true;

    [Header("Roots (under GameRoot)")]
    public Transform localAvatarsRoot;     // LocalAvatarsRoot
    public Transform remoteMirrorRoot;     // RemoteMirrorRoot

    [Header("Mirror Placement")]
    [Tooltip("How far across the table to place the mirrored remote group, in GameRoot local Z.")]
    public float remoteAcrossDistance = 1.6f;

    [Tooltip("If true, the remote group is rotated 180 degrees around Y.")]
    public bool rotateRemote180 = true;

    [Header("Debug")]
    public bool verboseLogs = true;
    public float rescanEverySeconds = 0.5f;

    private float nextScanTime = 0f;

    // Track what we've already handled so we do not keep reparenting
    private readonly HashSet<int> processedViews = new HashSet<int>();

    void Start()
    {
        if (!isMRScene) return;
        ApplyMirrorRootTransform();
    }

    void Update()
    {
        if (!isMRScene) return;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + rescanEverySeconds;

            ApplyMirrorRootTransform();
            ScanAndParentAvatars();
        }
    }

    private void ApplyMirrorRootTransform()
    {
        if (remoteMirrorRoot == null) return;

        remoteMirrorRoot.localPosition = new Vector3(0f, 0f, remoteAcrossDistance);

        if (rotateRemote180)
            remoteMirrorRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
        else
            remoteMirrorRoot.localRotation = Quaternion.identity;
    }

    private void ScanAndParentAvatars()
    {
        if (localAvatarsRoot == null || remoteMirrorRoot == null) return;

        GameObject[] avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        foreach (GameObject a in avatars)
        {
            RealtimeView v = a.GetComponent<RealtimeView>();
            if (v == null) continue;

            int viewID = v.viewUUID.GetHashCode(); // stable-ish identifier for this run
            if (processedViews.Contains(viewID))
                continue;

            // Parent exactly once
            if (v.isOwnedLocallySelf)
            {
                if (a.transform.parent != localAvatarsRoot)
                {
                    a.transform.SetParent(localAvatarsRoot, true);
                    if (verboseLogs) Debug.Log($"[MRRemoteAvatarMirrorPlacer] Local -> LocalAvatarsRoot ({a.name})");
                }
            }
            else
            {
                if (a.transform.parent != remoteMirrorRoot)
                {
                    a.transform.SetParent(remoteMirrorRoot, true);
                    if (verboseLogs) Debug.Log($"[MRRemoteAvatarMirrorPlacer] Remote -> RemoteMirrorRoot ({a.name})");
                }
            }

            processedViews.Add(viewID);
        }
    }
}
