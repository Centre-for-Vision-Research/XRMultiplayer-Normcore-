using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using Normal.Realtime;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class MRSharedAnchorManager : RealtimeComponent<MRSharedAnchorModel>
{
    public static MRSharedAnchorManager Instance { get; private set; }

    [Header("Mode")]
    public bool isMRScene = true;

    [Header("References")]
    public GameObject sharedAnchorPrefab;

    [Tooltip("Scene content root (table, holes, moles). Will be parented under the resolved anchor.")]
    public Transform contentRoot;

    [Tooltip("Empty GameObject used as parent for networked avatars. Will be parented under the resolved anchor.")]
    public Transform networkSpaceRoot;

    [Tooltip("Optional child under NetworkSpaceRoot where avatars should live.")]
    public string networkAvatarsChildName = "NetworkAvatars";

    [Header("Placement (Host)")]
    public bool hostPressAtoPlace = true;
    public KeyCode editorPlaceKey = KeyCode.P;
    public float defaultTableDistanceMeters = 0.8f;
    public float defaultTableHeightMeters = 0.3f;

    [Header("Peer gate (share only when peer exists)")]
    public string playerAvatarTag = "PlayerAvatar";
    public float peerScanIntervalSeconds = 0.25f;
    public float maxWaitForPeerSeconds = 120f;

    [Header("Robustness")]
    public int stabilizationDelayMs = 1500;
    public int saveRetries = 4;
    public int shareRetries = 4;
    public int retryDelayMs = 700;

    [Header("Debug")]
    public bool verboseLogs = true;

    public Transform AnchorTransform => _anchorGO != null ? _anchorGO.transform : null;
    public bool AnchorReady => _anchorReady;

    public Transform NetworkAvatarsParent
    {
        get
        {
            if (networkSpaceRoot == null) return null;
            Transform child = networkSpaceRoot.Find(networkAvatarsChildName);
            return child != null ? child : networkSpaceRoot;
        }
    }

    private Realtime _realtime;
    private RealtimeView _rv;

    private GameObject _anchorGO;
    private OVRSpatialAnchor _anchor;
    private bool _anchorReady = false;
    private bool _flowStarted = false;

    private InputDevice _rightHand;
    private bool _lastAState = false;

    private readonly List<Transform> _pendingAvatars = new List<Transform>();

    private const string SPATIAL_PERMISSION = "com.oculus.permission.USE_SCENE";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        if (!isMRScene) return;

        _realtime = FindObjectOfType<Realtime>();
        if (_realtime == null)
        {
            Debug.LogError("[MRSharedAnchorManager] Realtime not found.");
            return;
        }

        _rv = GetComponent<RealtimeView>();
        TryInitRightHand();

        // Hide content until anchor is ready (prevents “world is headlocked” visuals when SSA did not run)
        if (contentRoot != null) contentRoot.gameObject.SetActive(false);

        _realtime.didConnectToRoom += OnConnected;
    }

    private void OnDestroy()
    {
        if (_realtime != null) _realtime.didConnectToRoom -= OnConnected;
    }

    private void OnConnected(Realtime room)
    {
        if (!isMRScene) return;
        if (_flowStarted) return;
        _flowStarted = true;

        if (verboseLogs)
            Debug.Log($"[MRSharedAnchorManager] Connected. clientID={_realtime.clientID}. Starting SSA flow.");

        _ = RunMainFlowAsync();
    }

    // ----------------------------
    // Public API for avatar spawner
    // ----------------------------
    public void RegisterAvatarForReparent(Transform avatarRoot)
    {
        if (avatarRoot == null) return;

        if (_anchorReady && NetworkAvatarsParent != null)
        {
            avatarRoot.SetParent(NetworkAvatarsParent, worldPositionStays: false);
            avatarRoot.localPosition = Vector3.zero;
            avatarRoot.localRotation = Quaternion.identity;
            avatarRoot.localScale = Vector3.one;
            return;
        }

        if (!_pendingAvatars.Contains(avatarRoot))
            _pendingAvatars.Add(avatarRoot);
    }

    private void FlushPendingAvatars()
    {
        if (!_anchorReady) return;
        Transform parent = NetworkAvatarsParent;
        if (parent == null) return;

        for (int i = _pendingAvatars.Count - 1; i >= 0; i--)
        {
            Transform t = _pendingAvatars[i];
            if (t == null) { _pendingAvatars.RemoveAt(i); continue; }

            t.SetParent(parent, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            _pendingAvatars.RemoveAt(i);
        }

        // Also reparent any already-spawned avatars in scene once (no repeated scan)
        var avatars = GameObject.FindGameObjectsWithTag(playerAvatarTag);
        foreach (var a in avatars)
        {
            Transform t = a.transform;
            if (t.parent == parent) continue;
            t.SetParent(parent, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
    }

    // ----------------------------
    // Main flow
    // ----------------------------
    private async Task RunMainFlowAsync()
    {
        bool perm = await EnsureSpatialPermissionAsync();
        if (!perm)
        {
            Debug.LogError("[MRSharedAnchorManager] Spatial Data permission not granted. Anchors will not work.");
            return;
        }

        bool isHost = IsHostByLowestClientID();

        if (isHost && _rv != null) _rv.RequestOwnership();

        // Host creates group UUID once
        if (isHost && string.IsNullOrEmpty(model.groupUuid))
        {
            model.groupUuid = Guid.NewGuid().ToString();
            model.stage = 0;

            if (verboseLogs)
                Debug.Log($"[MRSharedAnchorManager] Host created group UUID: {model.groupUuid}");
        }

        if (isHost)
        {
            // Host: place + create + save (local ready even if alone)
            bool ok = await HostCreateAndSaveAsync();
            if (!ok)
            {
                Debug.LogError("[MRSharedAnchorManager] Host failed to create/save anchor.");
                return;
            }

            BindContentUnderAnchorAndMarkReady();
            FlushPendingAvatars();

            // Share only when peer exists (prevents ShareAsync failing in solo case)
            _ = HostWaitForPeerThenShareAsync();
            return;
        }

        // Client: wait for host to share, then load+localize+bind
        bool clientOk = await ClientLoadLocalizeBindAsync();
        if (!clientOk)
        {
            Debug.LogError("[MRSharedAnchorManager] Client failed to load/localize/bind shared anchor.");
            return;
        }

        BindContentUnderAnchorAndMarkReady();
        FlushPendingAvatars();
    }

    private bool IsHostByLowestClientID()
    {
        // Simple + stable, available immediately.
        // If your room always assigns 0 to first joiner, this is what you want.
        return _realtime != null && _realtime.clientID == 0;
    }

    // ----------------------------
    // Permission
    // ----------------------------
    private async Task<bool> EnsureSpatialPermissionAsync()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION))
            return true;

        Permission.RequestUserPermission(SPATIAL_PERMISSION);

        float start = Time.time;
        while (!Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION))
        {
            if (Time.time - start > 15f) return false;
            await Task.Yield();
        }
        return true;
#else
        return true;
#endif
    }

    // ----------------------------
    // Host: create + save (no share yet)
    // ----------------------------
    private async Task<bool> HostCreateAndSaveAsync()
    {
        if (sharedAnchorPrefab == null)
        {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab not assigned.");
            return false;
        }

        if (string.IsNullOrEmpty(model.groupUuid))
        {
            Debug.LogError("[MRSharedAnchorManager] groupUuid missing.");
            return false;
        }

        await Task.Delay(250);

        if (hostPressAtoPlace)
        {
            if (verboseLogs)
                Debug.Log("[MRSharedAnchorManager] Host: press A (or P in editor) to place.");

            while (!WasPlacePressedThisFrame())
                await Task.Yield();
        }

        Pose pose = ComputeDefaultTablePose();

        _anchorGO = Instantiate(sharedAnchorPrefab, pose.position, pose.rotation);
        _anchorGO.transform.SetParent(null, true); // force scene root

        _anchor = _anchorGO.GetComponent<OVRSpatialAnchor>();
        if (_anchor == null)
        {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab must have OVRSpatialAnchor on root.");
            return false;
        }

        float start = Time.time;
        while (!_anchor.Created)
        {
            if (Time.time - start > 20f)
            {
                Debug.LogError("[MRSharedAnchorManager] Timed out waiting for anchor.Created.");
                return false;
            }
            await Task.Yield();
        }

        model.anchorUuid = _anchor.Uuid.ToString();
        model.stage = 1;

        if (verboseLogs)
            Debug.Log($"[MRSharedAnchorManager] Host created anchor. uuid={model.anchorUuid}. Stabilizing...");

        await Task.Delay(Mathf.Max(0, stabilizationDelayMs));

        var anchors = new List<OVRSpatialAnchor> { _anchor };

        bool saved = await RetryBoolAsync(
            () => SaveAnchorsBoolAsync(anchors),
            saveRetries,
            "SaveAnchorsAsync"
        );


        if (!saved) return false;

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Host saved anchor.");

        return true;
    }

    private async Task HostWaitForPeerThenShareAsync()
    {
        float start = Time.time;

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Host waiting for peer before ShareAsync...");

        while (CountUniqueAvatarOwners() < 2)
        {
            if (Time.time - start > maxWaitForPeerSeconds)
            {
                Debug.LogWarning("[MRSharedAnchorManager] No peer detected within timeout. Not sharing yet.");
                return;
            }
            await Task.Delay(250);
        }

        // Share now
        Guid groupGuid;
        try { groupGuid = Guid.Parse(model.groupUuid); }
        catch
        {
            Debug.LogError("[MRSharedAnchorManager] groupUuid not a valid GUID.");
            return;
        }

        var anchors = new List<OVRSpatialAnchor> { _anchor };

        bool shared = await RetryBoolAsync(
            () => ShareAnchorsBoolAsync(anchors, groupGuid),
            shareRetries,
            "ShareAsync"
        );


        if (!shared)
        {
            Debug.LogError("[MRSharedAnchorManager] ShareAsync failed after retries.");
            return;
        }

        model.stage = 2;

        if (verboseLogs)
            Debug.Log($"[MRSharedAnchorManager] Host shared anchor to group {model.groupUuid}");
    }

    // ----------------------------
    // Client: wait -> load -> localize -> bind
    // ----------------------------
    private async Task<bool> ClientLoadLocalizeBindAsync()
    {
        while (string.IsNullOrEmpty(model.groupUuid))
            await Task.Yield();

        Guid groupGuid;
        try { groupGuid = Guid.Parse(model.groupUuid); }
        catch
        {
            Debug.LogError("[MRSharedAnchorManager] groupUuid not a valid GUID.");
            return false;
        }

        float start = Time.time;
        while (model.stage < 2)
        {
            if (Time.time - start > 180f)
            {
                Debug.LogError("[MRSharedAnchorManager] Timed out waiting for host to share (stage < 2).");
                return false;
            }
            await Task.Yield();
        }

        var unbound = new List<OVRSpatialAnchor.UnboundAnchor>();

        start = Time.time;
        while (true)
        {
            unbound.Clear();
            bool loaded = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupGuid, unbound);
            if (loaded && unbound.Count > 0) break;

            if (Time.time - start > 90f)
            {
                Debug.LogError("[MRSharedAnchorManager] Timed out loading shared anchors.");
                return false;
            }

            await Task.Delay(300);
        }

        var ua = unbound[0];

        bool localized = await ua.LocalizeAsync();
        if (!localized)
        {
            Debug.LogError("[MRSharedAnchorManager] LocalizeAsync returned false.");
            return false;
        }

        Pose pose = ua.Pose;

        _anchorGO = Instantiate(sharedAnchorPrefab, pose.position, pose.rotation);
        _anchorGO.transform.SetParent(null, true);

        _anchor = _anchorGO.GetComponent<OVRSpatialAnchor>();
        if (_anchor == null)
        {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab must have OVRSpatialAnchor on root.");
            return false;
        }

        ua.BindTo(_anchor);

        start = Time.time;
        while (!_anchor.Created)
        {
            if (Time.time - start > 20f)
            {
                Debug.LogError("[MRSharedAnchorManager] Timed out waiting for bound anchor.Created.");
                return false;
            }
            await Task.Yield();
        }

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Client localized and bound shared anchor.");

        return true;
    }

    private void BindContentUnderAnchorAndMarkReady()
    {
        if (_anchorGO == null || _anchor == null || !_anchor.Created)
        {
            Debug.LogError("[MRSharedAnchorManager] Cannot bind content. Anchor invalid.");
            return;
        }

        if (contentRoot != null)
        {
            contentRoot.SetParent(_anchorGO.transform, worldPositionStays: false);
            contentRoot.localPosition = Vector3.zero;
            contentRoot.localRotation = Quaternion.identity;
            contentRoot.gameObject.SetActive(true);
        }

        if (networkSpaceRoot != null)
        {
            networkSpaceRoot.SetParent(_anchorGO.transform, worldPositionStays: false);
            networkSpaceRoot.localPosition = Vector3.zero;
            networkSpaceRoot.localRotation = Quaternion.identity;
        }

        _anchorReady = true;

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Anchor ready. Content/network are under anchor.");
    }

    // ----------------------------
    // Helpers
    // ----------------------------
    private int CountUniqueAvatarOwners()
    {
        var avatars = GameObject.FindGameObjectsWithTag(playerAvatarTag);
        HashSet<int> owners = new HashSet<int>();

        foreach (var a in avatars)
        {
            var v = a.GetComponent<RealtimeView>();
            if (v != null && v.ownerIDInHierarchy >= 0)
                owners.Add(v.ownerIDInHierarchy);
        }

        return owners.Count;
    }

    private async Task<bool> RetryBoolAsync(Func<Task<bool>> op, int retries, string name)
    {
        int attempts = Mathf.Max(1, retries);

        for (int i = 1; i <= attempts; i++)
        {
            bool ok = false;
            try { ok = await op(); }
            catch (Exception e)
            {
                Debug.LogWarning($"[MRSharedAnchorManager] {name} threw: {e.Message}");
                ok = false;
            }

            if (ok) return true;

            int delay = Mathf.Max(0, i * retryDelayMs);
            Debug.LogWarning($"[MRSharedAnchorManager] {name} failed (attempt {i}/{attempts}). Retrying in {delay}ms...");
            await Task.Delay(delay);
        }

        return false;
    }

    private bool WasPlacePressedThisFrame()
    {
        if (Input.GetKeyDown(editorPlaceKey)) return true;

        if (!_rightHand.isValid) TryInitRightHand();
        if (_rightHand.isValid)
        {
            bool aNow;
            if (_rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out aNow))
            {
                bool rising = aNow && !_lastAState;
                _lastAState = aNow;
                if (rising) return true;
            }
        }
        return false;
    }

    private void TryInitRightHand()
    {
        _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    private Pose ComputeDefaultTablePose()
    {
        Camera cam = Camera.main;
        if (cam == null) return new Pose(Vector3.zero, Quaternion.identity);

        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 p = cam.transform.position + fwd * defaultTableDistanceMeters;
        p.y = defaultTableHeightMeters;

        float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);

        return new Pose(p, r);
    }

    private async Task<bool> SaveAnchorsBoolAsync(List<OVRSpatialAnchor> anchors)
    {
        var result = await OVRSpatialAnchor.SaveAnchorsAsync(anchors);
        return result.Success;
    }

    private async Task<bool> ShareAnchorsBoolAsync(List<OVRSpatialAnchor> anchors, Guid groupGuid)
    {
        var result = await OVRSpatialAnchor.ShareAsync(anchors, groupGuid);
        return result.Success;
    }

}
