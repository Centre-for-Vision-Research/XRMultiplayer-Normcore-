using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using Normal.Realtime;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class MRSharedAnchorManager : RealtimeComponent<MRSharedAnchorModel> {
    public static MRSharedAnchorManager Instance { get; private set; }

    [Header("Mode")]
    public bool isMRScene = true;

    [Header("References")]
    [Tooltip("Prefab that has an OVRSpatialAnchor component on its root.")]
    public GameObject sharedAnchorPrefab;

    [Tooltip("Scene content root (table, holes, moles). Will be parented under the resolved anchor.")]
    public Transform contentRoot;

    [Tooltip("Empty GameObject used as parent for networked avatars. Will be parented under the resolved anchor.")]
    public Transform networkSpaceRoot;

    [Header("Placement (Host)")]
    public bool hostPressAtoPlace = true;
    public KeyCode editorPlaceKey = KeyCode.P;

    public float defaultTableDistanceMeters = 0.8f;
    public float defaultTableHeightMeters = 0.3f;

    [Header("Reparent Scan")]
    public float reparentScanEverySeconds = 0.5f;

    [Header("Robustness")]
    [Tooltip("Extra delay after anchor creation before attempting Save/Share.")]
    public float stabilizationDelayMs = 1500f;

    [Tooltip("Retry count for SaveAnchorsAsync.")]
    public int saveRetries = 3;

    [Tooltip("Retry count for ShareAsync.")]
    public int shareRetries = 3;

    [Tooltip("Delay between retry attempts.")]
    public int retryDelayMs = 1000;

    [Header("Debug")]
    public bool verboseLogs = true;

    public Transform AnchorTransform => _anchorGO != null ? _anchorGO.transform : null;
    public bool AnchorReady => _anchorReady;

    private Realtime _realtime;

    private GameObject _anchorGO;
    private OVRSpatialAnchor _anchor;
    private bool _anchorReady = false;

    private bool _flowStarted = false;
    private float _nextScanTime = 0f;

    private InputDevice _rightHand;
    private bool _lastAState = false;

    // Permissions (runtime prompt; manifest still required)
    private const string SPATIAL_PERMISSION = "com.oculus.permission.USE_SCENE";

    private void Awake() {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start() {
        if (!isMRScene) return;

        _realtime = FindObjectOfType<Realtime>();
        if (_realtime == null) {
            Debug.LogError("[MRSharedAnchorManager] Realtime not found.");
            return;
        }

        TryInitRightHand();
        _realtime.didConnectToRoom += OnConnected;
    }

    private void OnDestroy() {
        if (_realtime != null) _realtime.didConnectToRoom -= OnConnected;
    }

    private void OnConnected(Realtime room) {
        if (!isMRScene) return;
        if (_flowStarted) return;
        _flowStarted = true;

        if (verboseLogs)
            Debug.Log($"[MRSharedAnchorManager] Connected. clientID={_realtime.clientID}. Starting SSA flow.");

        _ = RunMainFlowAsync();
    }

    private void Update() {
        if (!isMRScene) return;

        if (_anchorReady && Time.time >= _nextScanTime) {
            _nextScanTime = Time.time + reparentScanEverySeconds;
            ReparentAllPlayerAvatarsToNetworkSpace();
        }
    }

    private async Task RunMainFlowAsync() {
        bool perm = await EnsureSpatialPermissionAsync();
        if (!perm) {
            Debug.LogError("[MRSharedAnchorManager] Spatial Data permission not granted. Anchors will not work.");
            return;
        }

        // Wait briefly for RoleManager, but do not block forever.
        float t0 = Time.time;
        while (RoleManager.Instance == null && Time.time - t0 < 10f) {
            await Task.Yield();
        }

        bool isHost = IsHostTeacher();

        // Host takes ownership so it can write the model.
        if (isHost) {
            var rv = GetComponent<RealtimeView>();
            if (rv != null) rv.RequestOwnership();
        }

        // Host creates group UUID if missing.
        if (isHost && string.IsNullOrEmpty(model.groupUuid)) {
            model.groupUuid = Guid.NewGuid().ToString();
            model.stage = 1;

            if (verboseLogs)
                Debug.Log($"[MRSharedAnchorManager] Host created group UUID: {model.groupUuid}");
        }

        // HOST: create + save + share. Abort on failure.
        if (isHost) {
            bool ok = await HostCreateSaveShareAsync();
            if (!ok) {
                Debug.LogError("[MRSharedAnchorManager] Host failed to create/save/share anchor. Aborting.");
                return;
            }

            // Host is already localized by definition. Bind content now.
            BindContentUnderAnchorAndMarkReady();
            return;
        }

        // CLIENT: wait until host shared (stage>=2), then load + localize + bind.
        bool clientOk = await ClientLoadLocalizeBindAsync();
        if (!clientOk) {
            Debug.LogError("[MRSharedAnchorManager] Client failed to load/localize/bind shared anchor.");
            return;
        }

        BindContentUnderAnchorAndMarkReady();
    }

    // ----------------------------
    // Permission
    // ----------------------------
    private async Task<bool> EnsureSpatialPermissionAsync() {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION))
            return true;

        Permission.RequestUserPermission(SPATIAL_PERMISSION);

        float start = Time.time;
        while (!Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION)) {
            if (Time.time - start > 15f) return false;
            await Task.Yield();
        }
        return true;
#else
        return true;
#endif
    }

    // ----------------------------
    // Host: create -> save -> share
    // ----------------------------
    private async Task<bool> HostCreateSaveShareAsync() {
        if (sharedAnchorPrefab == null) {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab is not assigned.");
            return false;
        }

        if (string.IsNullOrEmpty(model.groupUuid)) {
            Debug.LogError("[MRSharedAnchorManager] groupUuid missing on host.");
            return false;
        }

        // Small guard so "press A immediately on connect" is less likely to fail.
        await Task.Delay(500);

        if (hostPressAtoPlace) {
            if (verboseLogs)
                Debug.Log("[MRSharedAnchorManager] Host: press A (or P in editor) to place the shared anchor.");

            while (true) {
                if (WasPlacePressedThisFrame()) break;
                await Task.Yield();
            }
        }

        Pose pose = ComputeDefaultTablePose();

        _anchorGO = Instantiate(sharedAnchorPrefab, pose.position, pose.rotation);
        _anchor = _anchorGO.GetComponent<OVRSpatialAnchor>();
        if (_anchor == null) {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab must have OVRSpatialAnchor on root.");
            return false;
        }

        // Wait for creation to complete (Created true)
        float start = Time.time;
        while (!_anchor.Created) {
            if (Time.time - start > 20f) {
                Debug.LogError("[MRSharedAnchorManager] Timed out waiting for anchor.Created. This usually indicates permission, platform support, or anchor creation failure.");
                return false;
            }
            await Task.Yield();
        }

        model.anchorUuid = _anchor.Uuid.ToString();

        if (verboseLogs)
            Debug.Log($"[MRSharedAnchorManager] Host created anchor. anchorUuid={model.anchorUuid}");

        // IMPORTANT: Created does not mean sharable/storable/cloud-ready.
        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Waiting for spatial stabilization before Save/Share...");

        await Task.Delay(Mathf.Max(0, (int)stabilizationDelayMs));

        var anchors = new List<OVRSpatialAnchor> { _anchor };

        // Retry save
        bool saved = false;
        for (int attempt = 1; attempt <= Mathf.Max(1, saveRetries); attempt++) {
            saved = await OVRSpatialAnchor.SaveAnchorsAsync(anchors);
            if (saved) break;

            Debug.LogWarning($"[MRSharedAnchorManager] SaveAnchorsAsync failed (attempt {attempt}/{saveRetries}). Retrying...");
            await Task.Delay(Mathf.Max(0, retryDelayMs));
        }

        if (!saved) {
            Debug.LogError("[MRSharedAnchorManager] SaveAnchorsAsync failed after retries.");
            return false;
        }

        Guid groupGuid = Guid.Parse(model.groupUuid);

        // Retry share
        bool shared = false;
        for (int attempt = 1; attempt <= Mathf.Max(1, shareRetries); attempt++) {
            shared = await OVRSpatialAnchor.ShareAsync(anchors, groupGuid);
            if (shared) break;

            Debug.LogWarning($"[MRSharedAnchorManager] ShareAsync failed (attempt {attempt}/{shareRetries}). Retrying...");
            await Task.Delay(Mathf.Max(0, retryDelayMs));
        }

        if (!shared) {
            Debug.LogError("[MRSharedAnchorManager] ShareAsync failed after retries.");
            return false;
        }

        model.stage = 2;

        if (verboseLogs)
            Debug.Log($"[MRSharedAnchorManager] Host shared anchor to group {model.groupUuid}");

        return true;
    }

    // ----------------------------
    // Client: wait -> load -> localize -> bind
    // ----------------------------
    private async Task<bool> ClientLoadLocalizeBindAsync() {
        // Wait for group uuid
        while (string.IsNullOrEmpty(model.groupUuid)) {
            await Task.Yield();
        }
        Guid groupGuid = Guid.Parse(model.groupUuid);

        // Wait for host to share
        float start = Time.time;
        while (model.stage < 2) {
            if (Time.time - start > 60f) {
                Debug.LogError("[MRSharedAnchorManager] Timed out waiting for host to share (stage < 2).");
                return false;
            }
            await Task.Yield();
        }

        if (sharedAnchorPrefab == null) {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab is not assigned.");
            return false;
        }

        var unbound = new List<OVRSpatialAnchor.UnboundAnchor>();

        start = Time.time;
        while (true) {
            unbound.Clear();

            bool loaded = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupGuid, unbound);
            if (loaded && unbound.Count > 0) break;

            if (Time.time - start > 60f) {
                Debug.LogError("[MRSharedAnchorManager] Timed out loading shared anchors for group.");
                return false;
            }

            await Task.Delay(250);
        }

        var ua = unbound[0];

        bool localized = await ua.LocalizeAsync();
        if (!localized) {
            Debug.LogError("[MRSharedAnchorManager] LocalizeAsync returned false.");
            return false;
        }

        Pose pose = ua.Pose;

        _anchorGO = Instantiate(sharedAnchorPrefab, pose.position, pose.rotation);
        _anchor = _anchorGO.GetComponent<OVRSpatialAnchor>();
        if (_anchor == null) {
            Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab must have OVRSpatialAnchor on root.");
            return false;
        }

        ua.BindTo(_anchor);

        // Wait for Created after bind, just to be safe.
        start = Time.time;
        while (!_anchor.Created) {
            if (Time.time - start > 20f) {
                Debug.LogError("[MRSharedAnchorManager] Timed out waiting for bound anchor.Created on client.");
                return false;
            }
            await Task.Yield();
        }

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Client localized and bound shared anchor.");

        return true;
    }

    private void BindContentUnderAnchorAndMarkReady() {
        if (_anchorGO == null || _anchor == null || !_anchor.Created) {
            Debug.LogError("[MRSharedAnchorManager] Cannot bind content. Anchor is not valid/created.");
            return;
        }

        if (contentRoot != null) {
            contentRoot.SetParent(_anchorGO.transform, worldPositionStays: false);
            contentRoot.localPosition = Vector3.zero;
            contentRoot.localRotation = Quaternion.identity;
        }

        if (networkSpaceRoot != null) {
            networkSpaceRoot.SetParent(_anchorGO.transform, worldPositionStays: false);
            networkSpaceRoot.localPosition = Vector3.zero;
            networkSpaceRoot.localRotation = Quaternion.identity;
        }

        _anchorReady = true;

        ReparentAllPlayerAvatarsToNetworkSpace();

        if (verboseLogs)
            Debug.Log("[MRSharedAnchorManager] Anchor ready. Content and avatars now share anchor space.");
    }

    // ----------------------------
    // Inputs and helpers
    // ----------------------------
    private bool IsHostTeacher() {
        return RoleManager.Instance != null &&
               _realtime != null &&
               RoleManager.Instance.IsTeacher(_realtime.clientID);
    }

    private bool WasPlacePressedThisFrame() {
        if (Input.GetKeyDown(editorPlaceKey)) return true;

        if (!_rightHand.isValid) TryInitRightHand();
        if (_rightHand.isValid) {
            bool aNow = false;
            if (_rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out aNow)) {
                bool rising = aNow && !_lastAState;
                _lastAState = aNow;
                if (rising) return true;
            }
        }
        return false;
    }

    private void TryInitRightHand() {
        _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
    }

    private Pose ComputeDefaultTablePose() {
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

    private void ReparentAllPlayerAvatarsToNetworkSpace() {
        if (networkSpaceRoot == null) return;

        // Optional: if you have a child named NetworkAvatars under networkSpaceRoot, prefer it.
        Transform netAvatars = networkSpaceRoot.Find("NetworkAvatars");
        if (netAvatars == null) netAvatars = networkSpaceRoot;

        GameObject[] avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        foreach (var a in avatars) {
            var t = a.transform;
            if (t.parent == netAvatars) continue;

            t.SetParent(netAvatars, worldPositionStays: false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
    }
}





// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using UnityEngine;
// using UnityEngine.XR;
// using Normal.Realtime;

// #if UNITY_ANDROID && !UNITY_EDITOR
// using UnityEngine.Android;
// #endif

// public class MRSharedAnchorManager : RealtimeComponent<MRSharedAnchorModel> {
//     public static MRSharedAnchorManager Instance { get; private set; }

//     [Header("Mode")]
//     public bool isMRScene = true;

//     [Header("References")]
//     [Tooltip("Prefab that has an OVRSpatialAnchor component on its root.")]
//     public GameObject sharedAnchorPrefab;

//     [Tooltip("Scene content root (table, holes, moles). Will be parented under the resolved anchor.")]
//     public Transform contentRoot;

//     [Tooltip("Empty GameObject used as parent for networked avatars. Will be parented under the resolved anchor.")]
//     public Transform networkSpaceRoot;

//     [Header("Placement (Host)")]
//     public bool hostPressAtoPlace = true;
//     public KeyCode editorPlaceKey = KeyCode.P;

//     public float defaultTableDistanceMeters = 0.8f;
//     public float defaultTableHeightMeters = 0.3f;

//     [Header("Reparent Scan")]
//     public float reparentScanEverySeconds = 0.5f;

//     [Header("Debug")]
//     public bool verboseLogs = true;

//     public Transform AnchorTransform => _anchorGO != null ? _anchorGO.transform : null;
//     public bool AnchorReady => _anchorReady;

//     private Realtime _realtime;

//     private GameObject _anchorGO;
//     private OVRSpatialAnchor _anchor;
//     private bool _anchorReady = false;

//     private bool _flowStarted = false;
//     private float _nextScanTime = 0f;

//     private InputDevice _rightHand;
//     private bool _lastAState = false;

//     // Permissions
//     private const string SPATIAL_PERMISSION = "com.oculus.permission.USE_SCENE";

//     private void Awake() {
//         if (Instance == null) Instance = this;
//         else Destroy(gameObject);
//     }

//     private void Start() {
//         if (!isMRScene) return;

//         _realtime = FindObjectOfType<Realtime>();
//         if (_realtime == null) {
//             Debug.LogError("[MRSharedAnchorManager] Realtime not found.");
//             return;
//         }

//         TryInitRightHand();
//         _realtime.didConnectToRoom += OnConnected;
//     }

//     private void OnDestroy() {
//         if (_realtime != null) _realtime.didConnectToRoom -= OnConnected;
//     }

//     private void OnConnected(Realtime room) {
//         if (!isMRScene) return;
//         if (_flowStarted) return;
//         _flowStarted = true;

//         if (verboseLogs)
//             Debug.Log($"[MRSharedAnchorManager] Connected. clientID={_realtime.clientID}. Starting SSA flow.");

//         _ = RunMainFlowAsync();
//     }

//     private void Update() {
//         if (!isMRScene) return;

//         if (_anchorReady && Time.time >= _nextScanTime) {
//             _nextScanTime = Time.time + reparentScanEverySeconds;
//             ReparentAllPlayerAvatarsToNetworkSpace();
//         }
//     }

//     private async Task RunMainFlowAsync() {
//         bool perm = await EnsureSpatialPermissionAsync();
//         if (!perm) {
//             Debug.LogError("[MRSharedAnchorManager] Spatial Data permission not granted. Anchors will not work.");
//             return;
//         }

//         // Wait briefly for RoleManager, but do not block forever.
//         float t0 = Time.time;
//         while (RoleManager.Instance == null && Time.time - t0 < 10f) {
//             await Task.Yield();
//         }

//         bool isHost = IsHostTeacher();

//         // Host takes ownership so it can write the model.
//         if (isHost) {
//             var rv = GetComponent<RealtimeView>();
//             if (rv != null) rv.RequestOwnership();
//         }

//         // Host creates group UUID if missing.
//         if (isHost && string.IsNullOrEmpty(model.groupUuid)) {
//             model.groupUuid = Guid.NewGuid().ToString();
//             model.stage = 1;

//             if (verboseLogs)
//                 Debug.Log($"[MRSharedAnchorManager] Host created group UUID: {model.groupUuid}");
//         }

//         // HOST: create + save + share. Abort on failure.
//         if (isHost) {
//             bool ok = await HostCreateSaveShareAsync();
//             if (!ok) {
//                 Debug.LogError("[MRSharedAnchorManager] Host failed to create/save/share anchor. Aborting.");
//                 return;
//             }

//             // Host is already localized by definition. Bind content now.
//             BindContentUnderAnchorAndMarkReady();
//             return;
//         }

//         // CLIENT: wait until host shared (stage>=2), then load + localize + bind.
//         bool clientOk = await ClientLoadLocalizeBindAsync();
//         if (!clientOk) {
//             Debug.LogError("[MRSharedAnchorManager] Client failed to load/localize/bind shared anchor.");
//             return;
//         }

//         BindContentUnderAnchorAndMarkReady();
//     }

//     // ----------------------------
//     // Permission
//     // ----------------------------
//     private async Task<bool> EnsureSpatialPermissionAsync() {
// #if UNITY_ANDROID && !UNITY_EDITOR
//         if (Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION))
//             return true;

//         Permission.RequestUserPermission(SPATIAL_PERMISSION);

//         float start = Time.time;
//         while (!Permission.HasUserAuthorizedPermission(SPATIAL_PERMISSION)) {
//             if (Time.time - start > 15f) return false;
//             await Task.Yield();
//         }
//         return true;
// #else
//         return true;
// #endif
//     }

//     // ----------------------------
//     // Host: create -> save -> share
//     // ----------------------------
//     private async Task<bool> HostCreateSaveShareAsync() {
//         if (sharedAnchorPrefab == null) {
//             Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab is not assigned.");
//             return false;
//         }

//         if (string.IsNullOrEmpty(model.groupUuid)) {
//             Debug.LogError("[MRSharedAnchorManager] groupUuid missing on host.");
//             return false;
//         }

//         if (hostPressAtoPlace) {
//             if (verboseLogs)
//                 Debug.Log("[MRSharedAnchorManager] Host: press A (or P in editor) to place the shared anchor.");

//             while (true) {
//                 if (WasPlacePressedThisFrame()) break;
//                 await Task.Yield();
//             }
//         }

//         Pose pose = ComputeDefaultTablePose();

//         _anchorGO = Instantiate(sharedAnchorPrefab, pose.position, pose.rotation);
//         _anchor = _anchorGO.GetComponent<OVRSpatialAnchor>();
//         if (_anchor == null) {
//             Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab must have OVRSpatialAnchor on root.");
//             return false;
//         }

//         // Wait for creation to complete (Created true) with a longer timeout and better logging.
//         float start = Time.time;
//         while (!_anchor.Created) {
//             if (Time.time - start > 20f) {
//                 Debug.LogError("[MRSharedAnchorManager] Timed out waiting for anchor.Created. This usually indicates permission, platform support, or anchor creation failure.");
//                 return false;
//             }
//             await Task.Yield();
//         }

//         model.anchorUuid = _anchor.Uuid.ToString();

//         if (verboseLogs)
//             Debug.Log($"[MRSharedAnchorManager] Host created anchor. anchorUuid={model.anchorUuid}");

//         var anchors = new List<OVRSpatialAnchor> { _anchor };

//         // v83 style APIs often return bool for these async helpers.
//         bool saved = await OVRSpatialAnchor.SaveAnchorsAsync(anchors);
//         if (!saved) {
//             Debug.LogError("[MRSharedAnchorManager] SaveAnchorsAsync returned false.");
//             return false;
//         }

//         Guid groupGuid = Guid.Parse(model.groupUuid);

//         bool shared = await OVRSpatialAnchor.ShareAsync(anchors, groupGuid);
//         if (!shared) {
//             Debug.LogError("[MRSharedAnchorManager] ShareAsync returned false.");
//             return false;
//         }

//         model.stage = 2;

//         if (verboseLogs)
//             Debug.Log($"[MRSharedAnchorManager] Host shared anchor to group {model.groupUuid}");

//         return true;
//     }

//     // ----------------------------
//     // Client: wait -> load -> localize -> bind
//     // ----------------------------
//     private async Task<bool> ClientLoadLocalizeBindAsync() {
//         // Wait for group uuid
//         while (string.IsNullOrEmpty(model.groupUuid)) {
//             await Task.Yield();
//         }
//         Guid groupGuid = Guid.Parse(model.groupUuid);

//         // Wait for host to share
//         float start = Time.time;
//         while (model.stage < 2) {
//             if (Time.time - start > 60f) {
//                 Debug.LogError("[MRSharedAnchorManager] Timed out waiting for host to share (stage < 2).");
//                 return false;
//             }
//             await Task.Yield();
//         }

//         if (sharedAnchorPrefab == null) {
//             Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab is not assigned.");
//             return false;
//         }

//         var unbound = new List<OVRSpatialAnchor.UnboundAnchor>();

//         start = Time.time;
//         while (true) {
//             unbound.Clear();

//             bool loaded = await OVRSpatialAnchor.LoadUnboundSharedAnchorsAsync(groupGuid, unbound);
//             if (loaded && unbound.Count > 0) break;

//             if (Time.time - start > 60f) {
//                 Debug.LogError("[MRSharedAnchorManager] Timed out loading shared anchors for group.");
//                 return false;
//             }

//             await Task.Delay(250);
//         }

//         var ua = unbound[0];

//         bool localized = await ua.LocalizeAsync();
//         if (!localized) {
//             Debug.LogError("[MRSharedAnchorManager] LocalizeAsync returned false.");
//             return false;
//         }

//         Pose pose = ua.Pose;

//         _anchorGO = Instantiate(sharedAnchorPrefab, pose.position, pose.rotation);
//         _anchor = _anchorGO.GetComponent<OVRSpatialAnchor>();
//         if (_anchor == null) {
//             Debug.LogError("[MRSharedAnchorManager] sharedAnchorPrefab must have OVRSpatialAnchor on root.");
//             return false;
//         }

//         ua.BindTo(_anchor);

//         // Wait for Created after bind, just to be safe.
//         start = Time.time;
//         while (!_anchor.Created) {
//             if (Time.time - start > 20f) {
//                 Debug.LogError("[MRSharedAnchorManager] Timed out waiting for bound anchor.Created on client.");
//                 return false;
//             }
//             await Task.Yield();
//         }

//         if (verboseLogs)
//             Debug.Log("[MRSharedAnchorManager] Client localized and bound shared anchor.");

//         return true;
//     }

//     private void BindContentUnderAnchorAndMarkReady() {
//         if (_anchorGO == null || _anchor == null || !_anchor.Created) {
//             Debug.LogError("[MRSharedAnchorManager] Cannot bind content. Anchor is not valid/created.");
//             return;
//         }

//         if (contentRoot != null) {
//             contentRoot.SetParent(_anchorGO.transform, worldPositionStays: false);
//             contentRoot.localPosition = Vector3.zero;
//             contentRoot.localRotation = Quaternion.identity;
//         }

//         if (networkSpaceRoot != null) {
//             networkSpaceRoot.SetParent(_anchorGO.transform, worldPositionStays: false);
//             networkSpaceRoot.localPosition = Vector3.zero;
//             networkSpaceRoot.localRotation = Quaternion.identity;
//         }

//         _anchorReady = true;

//         ReparentAllPlayerAvatarsToNetworkSpace();

//         if (verboseLogs)
//             Debug.Log("[MRSharedAnchorManager] Anchor ready. Content and avatars now share anchor space.");
//     }

//     // ----------------------------
//     // Inputs and helpers
//     // ----------------------------
//     private bool IsHostTeacher() {
//         return RoleManager.Instance != null &&
//                _realtime != null &&
//                RoleManager.Instance.IsTeacher(_realtime.clientID);
//     }

//     private bool WasPlacePressedThisFrame() {
//         if (Input.GetKeyDown(editorPlaceKey)) return true;

//         if (!_rightHand.isValid) TryInitRightHand();
//         if (_rightHand.isValid) {
//             bool aNow = false;
//             if (_rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out aNow)) {
//                 bool rising = aNow && !_lastAState;
//                 _lastAState = aNow;
//                 if (rising) return true;
//             }
//         }
//         return false;
//     }

//     private void TryInitRightHand() {
//         _rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
//     }

//     private Pose ComputeDefaultTablePose() {
//         Camera cam = Camera.main;
//         if (cam == null) return new Pose(Vector3.zero, Quaternion.identity);

//         Vector3 fwd = cam.transform.forward;
//         fwd.y = 0f;
//         if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
//         fwd.Normalize();

//         Vector3 p = cam.transform.position + fwd * defaultTableDistanceMeters;
//         p.y = defaultTableHeightMeters;

//         float yaw = Quaternion.LookRotation(fwd, Vector3.up).eulerAngles.y;
//         Quaternion r = Quaternion.Euler(0f, yaw, 0f);

//         return new Pose(p, r);
//     }

//     private void ReparentAllPlayerAvatarsToNetworkSpace() {
//         if (networkSpaceRoot == null) return;

//         // You said you have NetworkSpaceRoot/NetworkAvatars
//         Transform netAvatars = networkSpaceRoot.Find("NetworkAvatars");
//         if (netAvatars == null) netAvatars = networkSpaceRoot;

//         GameObject[] avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
//         foreach (var a in avatars) {
//             var t = a.transform;
//             if (t.parent == netAvatars) continue;

//             t.SetParent(netAvatars, worldPositionStays: false);
//             t.localPosition = Vector3.zero;
//             t.localRotation = Quaternion.identity;
//             t.localScale = Vector3.one;
//         }
//     }
// }
