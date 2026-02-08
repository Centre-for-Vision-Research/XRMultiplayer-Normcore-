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

    [Tooltip("Scene content root (table, holes, moles). Will be parented under the resolved anchor root.")]
    public Transform contentRoot;

    [Tooltip("Empty GameObject used as parent for networked avatars. Will be parented under the resolved anchor root.")]
    public Transform networkSpaceRoot;

    [Tooltip("Optional child under NetworkSpaceRoot where avatars should live.")]
    public string networkAvatarsChildName = "NetworkAvatars";

    [Header("Stable Root (prevents anchor micro-wobble)")]
    [Tooltip("If true, parent content/network under a StableSharedRoot. In Option 2, this root can follow the live anchor with smoothing.")]
    public bool useStableSharedRoot = true;

    [Tooltip("Name of the StableSharedRoot GameObject created at runtime.")]
    public string stableSharedRootName = "StableSharedRoot";

    [Header("Option 2: Dynamic correction (recommended)")]
    [Tooltip("If true and useStableSharedRoot is enabled, StableSharedRoot will follow the live anchor pose with smoothing + snap correction.")]
    public bool followAnchorContinuously = true;

    [Tooltip("Seconds. Larger means smoother but slower corrections.")]
    public float positionSmoothTime = 0.25f;

    [Tooltip("Seconds. Larger means smoother but slower corrections.")]
    public float rotationSmoothTime = 0.25f;

    [Tooltip("Meters. Ignore position changes smaller than this (jitter deadzone).")]
    public float positionDeadzoneMeters = 0.002f; // 2 mm

    [Tooltip("Degrees. Ignore rotation changes smaller than this (jitter deadzone).")]
    public float rotationDeadzoneDeg = 0.20f;

    [Tooltip("Meters. If StableSharedRoot differs from anchor more than this, snap (discrete correction).")]
    public float snapPositionMeters = 0.08f; // 8 cm

    [Tooltip("Degrees. If StableSharedRoot differs from anchor more than this, snap (discrete correction).")]
    public float snapRotationDeg = 8f;

    [Header("Anchor stability gate (improves initial alignment)")]
    [Tooltip("Wait until the anchor pose settles before enabling content/network (best effort, with timeout).")]
    public bool waitForAnchorStability = true;

    [Tooltip("Meters. Pose is considered stable if per-sample movement is below this.")]
    public float stablePosEpsilonMeters = 0.003f; // 3 mm

    [Tooltip("Degrees. Pose is considered stable if per-sample rotation change is below this.")]
    public float stableRotEpsilonDeg = 0.35f;

    [Tooltip("Seconds. Need this much continuous stability to pass the gate.")]
    public float requiredStableSeconds = 0.75f;

    [Tooltip("Seconds. Max time to wait for stability before proceeding anyway.")]
    public float maxWaitForStabilitySeconds = 8f;

    [Header("Placement (Host)")]
    public bool hostPressAtoPlace = true;
    public KeyCode editorPlaceKey = KeyCode.P;
    public float defaultTableDistanceMeters = 0.8f;
    public float defaultTableHeightMeters = 0.3f;

    [Header("Peer gate (share only when peer exists)")]
    public string playerAvatarTag = "PlayerAvatar";
    public float peerScanIntervalSeconds = 0.25f;
    public float maxWaitForPeerSeconds = 120f;

    [Header("Late joiner fix (reparent remote avatars that spawn after AnchorReady)")]
    [Tooltip("If true, after AnchorReady this script will keep scanning for newly spawned avatars and reparent them.")]
    public bool keepReparentingAfterAnchorReady = true;

    [Tooltip("How often to scan for newly spawned avatars after AnchorReady.")]
    public float reparentScanIntervalSeconds = 0.25f;

    [Header("Robustness")]
    public int stabilizationDelayMs = 1500;
    public int saveRetries = 4;
    public int shareRetries = 4;
    public int retryDelayMs = 700;

    [Header("Debug")]
    public bool verboseLogs = true;

    public Transform AnchorTransform => _anchorGO != null ? _anchorGO.transform : null;

    // AnchorReady means "shared world root is ready" (anchor OR stable root depending on mode)
    public bool AnchorReady => _anchorReady;

    public Transform StableRootTransform => _stableRootGO != null ? _stableRootGO.transform : null;

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

    private GameObject _stableRootGO;

    private bool _anchorReady = false;
    private bool _flowStarted = false;

    private InputDevice _rightHand;
    private bool _lastAState = false;

    private readonly List<Transform> _pendingAvatars = new List<Transform>();
    private float _nextReparentScanTime = 0f;

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

        // Hide content until anchor is ready
        if (contentRoot != null) contentRoot.gameObject.SetActive(false);

        _realtime.didConnectToRoom += OnConnected;
    }

    private void OnDestroy()
    {
        if (_realtime != null) _realtime.didConnectToRoom -= OnConnected;
    }

    private void Update()
    {
        if (!isMRScene) return;

        // Option 2: drive StableSharedRoot every frame once ready
        if (_anchorReady && useStableSharedRoot && followAnchorContinuously)
        {
            DriveStableRootTowardsAnchor(Time.deltaTime);
        }

        // Late joiner reparent scan
        if (!_anchorReady) return;
        if (!keepReparentingAfterAnchorReady) return;

        if (Time.time < _nextReparentScanTime) return;
        _nextReparentScanTime = Time.time + Mathf.Max(0.05f, reparentScanIntervalSeconds);

        ReparentAllTaggedAvatarsIfNeeded();
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

        ReparentAllTaggedAvatarsIfNeeded();
    }

    private void ReparentAllTaggedAvatarsIfNeeded()
    {
        Transform parent = NetworkAvatarsParent;
        if (parent == null) return;

        var avatars = GameObject.FindGameObjectsWithTag(playerAvatarTag);
        for (int i = 0; i < avatars.Length; i++)
        {
            Transform t = avatars[i] != null ? avatars[i].transform : null;
            if (t == null) continue;

            if (t.IsChildOf(parent)) continue;

            t.SetParent(parent, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            if (verboseLogs)
                Debug.Log($"[MRSharedAnchorManager] Reparented late/spawned avatar '{t.name}' under NetworkAvatarsParent.");
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

        if (isHost && string.IsNullOrEmpty(model.groupUuid))
        {
            model.groupUuid = Guid.NewGuid().ToString();
            model.stage = 0;

            if (verboseLogs)
                Debug.Log($"[MRSharedAnchorManager] Host created group UUID: {model.groupUuid}");
        }

        if (isHost)
        {
            bool ok = await HostCreateAndSaveAsync();
            if (!ok)
            {
                Debug.LogError("[MRSharedAnchorManager] Host failed to create/save anchor.");
                return;
            }

            // Option 2: wait for anchor pose to settle before enabling content/network
            if (waitForAnchorStability)
            {
                bool stable = await WaitForAnchorStablePoseAsync(maxWaitForStabilitySeconds);
                if (verboseLogs)
                    Debug.Log($"[MRSharedAnchorManager] Host stability gate result: {stable}");
            }

            BindContentUnderStableRootAndMarkReady();
            FlushPendingAvatars();

            _ = HostWaitForPeerThenShareAsync();
            return;
        }

        bool clientOk = await ClientLoadLocalizeBindAsync();
        if (!clientOk)
        {
            Debug.LogError("[MRSharedAnchorManager] Client failed to load/localize/bind shared anchor.");
            return;
        }

        // Option 2: wait for anchor pose to settle before enabling content/network
        if (waitForAnchorStability)
        {
            bool stable = await WaitForAnchorStablePoseAsync(maxWaitForStabilitySeconds);
            if (verboseLogs)
                Debug.Log($"[MRSharedAnchorManager] Client stability gate result: {stable}");
        }

        BindContentUnderStableRootAndMarkReady();
        FlushPendingAvatars();
    }

    private bool IsHostByLowestClientID()
    {
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
        _anchorGO.transform.SetParent(null, true);

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
            Debug.Log($"[MRSharedAnchorManager] Host created anchor. uuid={model.anchorUuid}. Initial settle delay...");

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

    // ----------------------------
    // Stable root binding
    // ----------------------------
    private void BindContentUnderStableRootAndMarkReady()
    {
        if (_anchorGO == null || _anchor == null || !_anchor.Created)
        {
            Debug.LogError("[MRSharedAnchorManager] Cannot bind content. Anchor invalid.");
            return;
        }

        Transform parentForWorld = _anchorGO.transform;

        if (useStableSharedRoot)
        {
            if (_stableRootGO == null)
            {
                _stableRootGO = new GameObject(stableSharedRootName);
                _stableRootGO.transform.SetParent(null, true);
            }

            // Initialize stable root from current anchor pose
            _stableRootGO.transform.position = _anchorGO.transform.position;
            _stableRootGO.transform.rotation = _anchorGO.transform.rotation;
            _stableRootGO.transform.localScale = Vector3.one;

            parentForWorld = _stableRootGO.transform;

            if (verboseLogs)
                Debug.Log("[MRSharedAnchorManager] StableSharedRoot initialized from anchor pose.");
        }

        if (contentRoot != null)
        {
            contentRoot.SetParent(parentForWorld, worldPositionStays: false);
            contentRoot.localPosition = Vector3.zero;
            contentRoot.localRotation = Quaternion.identity;
            contentRoot.gameObject.SetActive(true);
        }

        if (networkSpaceRoot != null)
        {
            networkSpaceRoot.SetParent(parentForWorld, worldPositionStays: false);
            networkSpaceRoot.localPosition = Vector3.zero;
            networkSpaceRoot.localRotation = Quaternion.identity;
        }

        _anchorReady = true;
        _nextReparentScanTime = Time.time; // start scanning immediately

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Anchor ready. Content/network are under shared root.");
    }

    // ----------------------------
    // Option 2: stability gate + dynamic correction
    // ----------------------------
    private async Task<bool> WaitForAnchorStablePoseAsync(float timeoutSeconds)
    {
        if (_anchorGO == null) return false;

        float startTime = Time.time;
        float stableAccum = 0f;

        Vector3 prevPos = _anchorGO.transform.position;
        Quaternion prevRot = _anchorGO.transform.rotation;
        float prevT = Time.time;

        while (Time.time - startTime < Mathf.Max(0.1f, timeoutSeconds))
        {
            await Task.Yield();

            float nowT = Time.time;
            float dt = Mathf.Max(0.0001f, nowT - prevT);
            prevT = nowT;

            Vector3 pos = _anchorGO.transform.position;
            Quaternion rot = _anchorGO.transform.rotation;

            float dp = Vector3.Distance(pos, prevPos);
            float dr = Quaternion.Angle(rot, prevRot);

            prevPos = pos;
            prevRot = rot;

            if (dp <= stablePosEpsilonMeters && dr <= stableRotEpsilonDeg)
            {
                stableAccum += dt;
                if (stableAccum >= requiredStableSeconds)
                    return true;
            }
            else
            {
                stableAccum = 0f;
            }
        }

        if (verboseLogs)
            Debug.LogWarning("[MRSharedAnchorManager] Stability gate timed out. Proceeding with best current pose.");

        return false;
    }

    private void DriveStableRootTowardsAnchor(float dt)
    {
        if (_stableRootGO == null || _anchorGO == null) return;

        Transform sr = _stableRootGO.transform;
        Transform a = _anchorGO.transform;

        Vector3 targetPos = a.position;
        Quaternion targetRot = a.rotation;

        float posErr = Vector3.Distance(sr.position, targetPos);
        float rotErr = Quaternion.Angle(sr.rotation, targetRot);

        // If anchor has significantly corrected, snap (discrete correction)
        if (posErr > snapPositionMeters || rotErr > snapRotationDeg)
        {
            sr.position = targetPos;
            sr.rotation = targetRot;

            if (verboseLogs)
                Debug.LogWarning($"[MRSharedAnchorManager] Snap correction applied. posErr={posErr:F3}m rotErr={rotErr:F2}deg");

            return;
        }

        // Ignore tiny jitter
        if (posErr < positionDeadzoneMeters && rotErr < rotationDeadzoneDeg)
            return;

        float aPos = ExpAlpha(dt, positionSmoothTime);
        float aRot = ExpAlpha(dt, rotationSmoothTime);

        sr.position = Vector3.Lerp(sr.position, targetPos, aPos);
        sr.rotation = Quaternion.Slerp(sr.rotation, targetRot, aRot);
    }

    private float ExpAlpha(float dt, float timeConstant)
    {
        if (timeConstant <= 0f) return 1f;
        return 1f - Mathf.Exp(-dt / timeConstant);
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
