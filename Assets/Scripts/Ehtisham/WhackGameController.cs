using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class WhackGameController : MonoBehaviour
{
    [Header("Scene References")]
    public Transform holesAndMolesRoot;

    [Header("Timings (seconds)")]
    public float spawnMin = 0.20f;
    public float spawnMax = 0.40f;
    public float holdMin  = 0.20f;
    public float holdMax  = 0.50f;

    [Header("Block Settings")]
    public float blockDurationSeconds = 120f;
    public int leadTimeMs = 250;
    public int postGameDelayMs = 1500;

    [Header("Return")]
    public bool autoReturnToLobby = true;
    public string lobbySceneName = "LobbyAvtrs";

    [Header("Collision Metrics")]
    public float nearThreshold = 0.18f;
    public float collisionThreshold = 0.08f;

    private bool _sessionStarted = false;   // becomes true once we ever enter state 2 this session


    [Header("Debug")]
    public bool verboseLogs = true;

    private Realtime _realtime;
    private WhackGameStateSync _gs;
    private RealtimeView _stateView;

    private System.Random _rng;

    private bool _isAuthority = false;
    private bool _authorityInitDone = false;

    private readonly Dictionary<int, MoleVisual> _molesByIndex = new Dictionary<int, MoleVisual>();
    private readonly Dictionary<int, WhackPlayerInput> _inputsByOwner = new Dictionary<int, WhackPlayerInput>();

    private readonly HashSet<int> _seenResolveIds = new HashSet<int>();
    private readonly Queue<int> _seenResolveQueue = new Queue<int>();
    private const int SeenResolveCapacity = 96;

    private int _resolveEventCounter = 0;
    private int _lastResolvedSeq = -1;
    private int _lastMissSeq = -1;
    private readonly Dictionary<int, int> _lastAcceptedSeqByClient = new Dictionary<int, int>();

    private string _dyadId = "UnknownDyad";
    private string _condition = "Unknown";

    private int _hitsA = 0, _hitsB = 0;
    private int _missesA = 0, _missesB = 0;
    private int _nearEvents = 0, _collisionEvents = 0;
    private int _crossHitAonB = 0, _crossHitBonA = 0;

    private bool[] _pairNear = new bool[4];
    private bool[] _pairCollide = new bool[4];

    private float _nextHostClockPushTime = 0f;
    private float _nextInputScanTime = 0f;

    private Coroutine _authorityAcquireLoop;

    private void Awake()
    {
        _condition = WhackConditionUtil.GetConditionFromScene();

        _realtime = FindObjectOfType<Realtime>();
        _gs = GetComponent<WhackGameStateSync>();
        if (_gs == null) _gs = FindObjectOfType<WhackGameStateSync>(true);

        if (_gs != null)
        {
            _stateView = _gs.GetComponent<RealtimeView>();
            if (_stateView == null) _stateView = _gs.GetComponentInParent<RealtimeView>();
        }
    }

    private void Start()
    {
        StartCoroutine(Boot());
    }

    private void OnDestroy()
    {
        if (_gs != null)
        {
            _gs.ResolveEvent -= OnResolveEvent;
            _gs.ownerIDSelfDidChange -= OnGameStateOwnerChanged;
        }

        foreach (var kv in _inputsByOwner)
            if (kv.Value != null) kv.Value.HitEventReceived -= OnHitEventReceived;

        _inputsByOwner.Clear();

        if (_authorityAcquireLoop != null)
            StopCoroutine(_authorityAcquireLoop);
        _authorityAcquireLoop = null;
    }


    private IEnumerator Boot()
    {
        while (_realtime == null)
        {
            _realtime = FindObjectOfType<Realtime>();
            yield return null;
        }

        yield return new WaitUntil(() => _realtime.connected && _realtime.clientID >= 0);

        if (MRSharedAnchorManager.Instance != null && MRSharedAnchorManager.Instance.isMRScene)
            yield return new WaitUntil(() => MRSharedAnchorManager.Instance.AnchorReady);

        yield return new WaitUntil(() => RoleManager.Instance != null);

        while (_gs == null)
        {
            _gs = GetComponent<WhackGameStateSync>();
            if (_gs == null) _gs = FindObjectOfType<WhackGameStateSync>(true);
            yield return null;
        }

        yield return new WaitUntil(() => _gs.IsModelReady());

        if (_stateView == null)
        {
            _stateView = _gs.GetComponent<RealtimeView>();
            if (_stateView == null) _stateView = _gs.GetComponentInParent<RealtimeView>();
        }

        if (_stateView == null)
        {
            Debug.LogError("[WhackGameController] Missing RealtimeView on WhackGameStateSync.");
            yield break;
        }

        // Recommended: put WhackGameState under the Realtime GameObject.
        // Fallback if you do not:
        EnsureStateViewIsBoundToRealtimeInstance();
        yield return null;

        // Wait until at least one avatar exists, so RoleManager can assign teacher.
        yield return new WaitUntil(() =>
        {
            var av = GameObject.FindGameObjectsWithTag("PlayerAvatar");
            return av != null && av.Length > 0;
        });

        // Cache moles
        for (int i = 0; i < 30; i++)
        {
            CacheMoles();
            if (_molesByIndex.Count > 0) break;
            yield return null;
        }

        if (_molesByIndex.Count == 0)
        {
            Debug.LogError("[WhackGameController] No moles found. Check holesAndMolesRoot.");
            yield break;
        }

        _gs.ResolveEvent -= OnResolveEvent;
        _gs.ResolveEvent += OnResolveEvent;

        _gs.ownerIDSelfDidChange -= OnGameStateOwnerChanged;
        _gs.ownerIDSelfDidChange += OnGameStateOwnerChanged;

        if (_authorityAcquireLoop == null)
            _authorityAcquireLoop = StartCoroutine(AuthorityAcquireLoop());

        UpdateAuthorityFlag();

        Log($"Boot complete. cid={_realtime.clientID} isAuthority={_isAuthority} viewOwner={_stateView.ownerIDInHierarchy} teacher={RoleManager.Instance.GetTeacherID()} student={RoleManager.Instance.GetStudentID()}");
    }

    private void EnsureStateViewIsBoundToRealtimeInstance()
    {
        if (_realtime == null || _stateView == null) return;

        // Scene RealtimeViews bind by finding a Realtime component in their parent chain.
        // If WhackGameState is not under the Realtime GameObject, it will stay unbound (ownerIDInHierarchy = -1).
        if (_stateView.GetComponentInParent<Realtime>() == null)
        {
            _stateView.transform.SetParent(_realtime.transform, true);

            if (verboseLogs)
                Log("StateView was not under a Realtime instance. Reparented under Realtime for binding.");
        }
    }



    private void OnGameStateOwnerChanged(RealtimeComponent<WhackGameStateModel> component,int newOwnerID)
    {
        UpdateAuthorityFlag();

        if (_isAuthority && !_authorityInitDone)
            AuthorityInitIfFresh();
    }


    private void StartOrStopAuthorityAcquireLoop()
    {
        int desired = GetDesiredAuthorityClientId();
        bool shouldTryAcquire = (_realtime != null && _realtime.clientID == desired);

        if (shouldTryAcquire && _authorityAcquireLoop == null)
            _authorityAcquireLoop = StartCoroutine(AuthorityAcquireLoop());
        else if (!shouldTryAcquire && _authorityAcquireLoop != null)
        {
            StopCoroutine(_authorityAcquireLoop);
            _authorityAcquireLoop = null;
        }
    }

    private IEnumerator AuthorityAcquireLoop()
    {
        while (true)
        {
            if (_realtime == null || !_realtime.connected || _realtime.clientID < 0) { yield return null; continue; }
            if (_gs == null || !_gs.IsModelReady()) { yield return null; continue; }
            if (_stateView == null) { yield return null; continue; }
            if (RoleManager.Instance == null) { yield return null; continue; }

            EnsureStateViewIsBoundToRealtimeInstance();

            int teacherId = RoleManager.Instance.GetTeacherID();
            bool shouldOwn = (teacherId >= 0 && _realtime.clientID == teacherId);

            if (shouldOwn && !_stateView.isOwnedLocallySelf)
            {
                try { _stateView.RequestOwnership(); }
                catch { }
            }

            UpdateAuthorityFlag();

            // IMPORTANT: do not "fresh check". Reset once per scene load when teacher becomes authority.
            if (_isAuthority && !_authorityInitDone)
                AuthorityResetToWaiting();

            yield return null;
        }
    }

    private void AuthorityResetToWaiting()
    {
        _authorityInitDone = true;

        // Reset local counters
        _hitsA = _hitsB = 0;
        _missesA = _missesB = 0;
        _nearEvents = _collisionEvents = 0;
        _crossHitAonB = _crossHitBonA = 0;
        for (int i = 0; i < _pairNear.Length; i++) _pairNear[i] = false;
        for (int i = 0; i < _pairCollide.Length; i++) _pairCollide[i] = false;

        _resolveEventCounter = 0;
        _lastResolvedSeq = -1;
        _lastMissSeq = -1;
        _lastAcceptedSeqByClient.Clear();
        ClearSeenResolveIds();

        _sessionStarted = false;

        int seed = RoleManager.Instance != null ? RoleManager.Instance.GetCommonSeed() : 0;
        if (seed == 0) seed = 12345;

        _rng = new System.Random(seed);
        _gs.AuthoritySetSeed(seed);

        CacheMoles();
        int initialHole = PickRandomHoleIndex();

        int now = HostNowMsLocal();
        _gs.AuthoritySetHostNowMs(now);

        // Clear any stale persisted times (prevents instant END and lobby return)
        _gs.AuthoritySetGameTimes(0, 0, 0);

        // Set waiting-for-first-hit, and make a mole up immediately
        _gs.AuthorityScheduleMole(seq: 0, holeIndex: initialHole, startMs: now, endMs: now + 9999999);
        _gs.AuthoritySetGameState(1);

        TryResolveDyadIdFromLocalAvatar();

        Log($"AUTH RESET -> WAITING. seed={seed} initialHole={initialHole} now={now}");
    }



    private void Update()
    {
        if (_realtime == null || !_realtime.connected || _realtime.clientID < 0) return;
        if (_gs == null || !_gs.IsModelReady()) return;
        if (_stateView == null) return;

        if (_gs.GameState == 2)
            _sessionStarted = true;

        if (Time.time >= _nextInputScanTime)
        {
            _nextInputScanTime = Time.time + 0.20f;
            RefreshInputs();
        }

        // Only allow lobby return if this session actually ran
        if (autoReturnToLobby && _sessionStarted && _gs.GameState == 3)
        {
            int hostNow = _gs.EstimateHostNowMs();
            int at = _gs.ReturnToLobbyAtMs;
            if (at > 0 && hostNow >= at)
                SceneManager.LoadScene(lobbySceneName);
        }

        if (!_isAuthority) return;

        DriveAuthority();
        DriveNearCollisionMetrics();
    }



    // ---------------- Authority logic ----------------

    private void UpdateAuthorityFlag()
    {
        // Authority is the actual model owner, not "teacher by idea".
        _isAuthority = (_gs != null && _gs.IsOwnedLocally);
    }

    private void AuthorityInitIfFresh()
    {
        // Only initialize if it looks like a fresh room
        bool looksFresh =
            _gs.GameState == 0 &&
            _gs.GameStartMs == 0 &&
            _gs.GameEndMs == 0 &&
            _gs.MoleStartMs == 0 &&
            _gs.MoleEndMs == 0;

        _authorityInitDone = true;

        if (!looksFresh)
        {
            Log("Authority obtained but state is not fresh. Skipping reset.");
            return;
        }

        _resolveEventCounter = 0;
        _lastResolvedSeq = -1;
        _lastMissSeq = -1;
        _lastAcceptedSeqByClient.Clear();
        ClearSeenResolveIds();

        int seed = RoleManager.Instance != null ? RoleManager.Instance.GetCommonSeed() : 0;
        if (seed == 0) seed = 12345;

        _rng = new System.Random(seed);
        _gs.AuthoritySetSeed(seed);

        TryResolveDyadIdFromLocalAvatar();

        StartCoroutine(AuthorityWaitThenStartMole(seed));
        Log($"AUTH init started. seed={seed}");
    }

    private IEnumerator AuthorityWaitThenStartMole(int seed)
    {
        // Dyad gate
        if (RoleManager.Instance != null && !RoleManager.Instance.IsSolo())
        {
            yield return new WaitUntil(() => RoleManager.Instance.IsDyadReady());
            yield return new WaitUntil(() => CountUniqueAvatarOwners() >= 2);
        }

        if (!_isAuthority) yield break;

        CacheMoles();
        if (_molesByIndex.Count == 0) yield break;

        int now = HostNowMsLocal();
        _gs.AuthoritySetHostNowMs(now);
        _gs.AuthoritySetGameTimes(0, 0, 0);

        int initialHole = PickRandomHoleIndex();
        _gs.AuthorityScheduleMole(seq: 0, holeIndex: initialHole, startMs: now, endMs: now + 9999999);
        _gs.AuthoritySetGameState(1);

        Log($"AUTH READY. seed={seed} initialHole={initialHole} now={now}");
    }

    private void DriveAuthority()
    {
        int now = HostNowMsLocal();

        // push host clock at ~30 Hz
        if (Time.realtimeSinceStartup >= _nextHostClockPushTime)
        {
            _nextHostClockPushTime = Time.realtimeSinceStartup + 0.033f;
            _gs.AuthoritySetHostNowMs(now);
        }

        int state = _gs.GameState;

        if (state == 0) return;
        if (state == 1) return;

        if (state == 2)
        {
            int gameEnd = _gs.GameEndMs;
            if (gameEnd > 0 && now >= gameEnd)
            {
                EndBlock(now);
                return;
            }

            int moleEnd = _gs.MoleEndMs;
            int curSeq = _gs.CurrentSeq;

            if (moleEnd > 0 && now > moleEnd && _lastMissSeq != curSeq && _lastResolvedSeq != curSeq)
            {
                _lastMissSeq = curSeq;
                ResolveMiss(now);
            }
        }
    }

    private void StartRunningFromFirstHit(int now)
    {
        int startMs = now;
        int endMs = startMs + Mathf.RoundToInt(blockDurationSeconds * 1000f);
        int returnMs = endMs + postGameDelayMs;

        _gs.AuthoritySetGameTimes(startMs, endMs, returnMs);
        _gs.AuthoritySetGameState(2);

        Log($"RUNNING started. startMs={startMs} endMs={endMs}");
    }

    private void EndBlock(int now)
    {
        _gs.AuthoritySetGameState(3);
        _gs.AuthoritySetGameTimes(_gs.GameStartMs, _gs.GameEndMs, now + postGameDelayMs);

        WriteSummaryCsv();
        Log($"END. hitsA={_hitsA} hitsB={_hitsB} missesA={_missesA} missesB={_missesB} near={_nearEvents} collide={_collisionEvents}");
    }

    private void ResolveMiss(int now)
    {
        int seq = _gs.CurrentSeq;
        int holeIndex = _gs.CurrentHoleIndex;

        int closerRole = GetCloserRoleToMole(holeIndex);
        if (closerRole == 0) _missesA++;
        else if (closerRole == 1) _missesB++;

        EmitResolve(type: 2, byRole: -1, holeIndex: holeIndex, seq: seq, atHostMs: now, byClientId: -1);

        _lastResolvedSeq = seq;
        ScheduleNextFrom(now);
    }

    private void ScheduleNextFrom(int now)
    {
        int delayMs = Mathf.RoundToInt(RandomRangeSeconds(spawnMin, spawnMax) * 1000f);
        int holdMs  = Mathf.RoundToInt(RandomRangeSeconds(holdMin,  holdMax)  * 1000f);

        int nextStart = now + Mathf.Max(leadTimeMs, delayMs);
        int nextEnd   = nextStart + holdMs;

        int nextHole = PickRandomHoleIndex();
        int nextSeq  = _gs.CurrentSeq + 1;

        _gs.AuthorityScheduleMole(nextSeq, nextHole, nextStart, nextEnd);
    }

    private void EmitResolve(int type, int byRole, int holeIndex, int seq, int atHostMs, int byClientId)
    {
        _gs.AuthorityEmitResolve(seq, holeIndex, type, byRole, atHostMs, byClientId);
    }



    // ---------------- Hit intake ----------------

    private void RefreshInputs()
    {
        var inputs = FindObjectsOfType<WhackPlayerInput>(true);
        foreach (var inp in inputs)
        {
            if (inp == null) continue;

            int owner = inp.OwnerClientIdInHierarchy;
            if (owner < 0) continue;

            if (_inputsByOwner.ContainsKey(owner)) continue;

            _inputsByOwner[owner] = inp;
            inp.HitEventReceived += OnHitEventReceived;

            Log($"Registered input owner={owner} name={inp.name}");
        }
    }

    private void OnHitEventReceived(WhackPlayerInput sender, WhackPlayerInput.HitEvent e)
    {
        if (!_isAuthority) return;
        if (_gs == null || !_gs.IsModelReady()) return;

        int state = _gs.GameState;
        if (state != 1 && state != 2) return;

        int curHole = _gs.CurrentHoleIndex;
        int curSeq = _gs.CurrentSeq;

        if (e.holeIndex != curHole) return;
        if (e.seq != curSeq) return;

        int byClientId = sender.OwnerClientIdInHierarchy;
        int byRole = OwnerToRole(byClientId);

        if (_lastAcceptedSeqByClient.TryGetValue(byClientId, out int lastSeq) && lastSeq == curSeq)
            return;
        _lastAcceptedSeqByClient[byClientId] = curSeq;

        if (_lastResolvedSeq == curSeq)
            return;

        int now = HostNowMsLocal();

        if (state == 1)
        {
            StartRunningFromFirstHit(now);

            EmitResolve(type: 1, byRole: byRole, holeIndex: curHole, seq: curSeq, atHostMs: now, byClientId: byClientId);
            _lastResolvedSeq = curSeq;

            ScheduleNextFrom(now);
            return;
        }

        const int windowGraceMs = 90;
        if (now < _gs.MoleStartMs - windowGraceMs || now > _gs.MoleEndMs + windowGraceMs)
            return;

        if (byRole == 0) _hitsA++;
        else if (byRole == 1) _hitsB++;

        int closerRole = GetCloserRoleToMole(curHole);
        if (byRole == 0 && closerRole == 1) _crossHitAonB++;
        if (byRole == 1 && closerRole == 0) _crossHitBonA++;

        EmitResolve(type: 1, byRole: byRole, holeIndex: curHole, seq: curSeq, atHostMs: now, byClientId: byClientId);
        _lastResolvedSeq = curSeq;

        ScheduleNextFrom(now);
    }

    // ---------------- Resolve FX ----------------

    private void OnResolveEvent(WhackGameStateSync.ResolveInfo info)
    {
        if (info.eventId <= 0) return;

        if (_seenResolveIds.Contains(info.eventId)) return;
        _seenResolveIds.Add(info.eventId);
        _seenResolveQueue.Enqueue(info.eventId);

        while (_seenResolveQueue.Count > SeenResolveCapacity)
        {
            int oldId = _seenResolveQueue.Dequeue();
            _seenResolveIds.Remove(oldId);
        }

        if (info.type != 1) return;

        // suppress echo for local hitter
        if (_realtime != null && info.byClientId == _realtime.clientID) return;

        if (_molesByIndex.TryGetValue(info.holeIndex, out MoleVisual m) && m != null)
            m.PlayHitFx(isLocalHitter: false);
    }

    private void ClearSeenResolveIds()
    {
        _seenResolveIds.Clear();
        _seenResolveQueue.Clear();
    }

    // ---------------- Utilities ----------------

    private int GetDesiredAuthorityClientId()
    {
        if (RoleManager.Instance == null) return 0;

        // If roles not ready and not solo, do not fight ownership yet.
        if (!RoleManager.Instance.IsDyadReady() && !RoleManager.Instance.IsSolo())
            return _stateView != null && _stateView.ownerIDInHierarchy >= 0 ? _stateView.ownerIDInHierarchy : 0;

        if (RoleManager.Instance.IsSolo())
            return _realtime != null ? _realtime.clientID : 0;

        int teacher = RoleManager.Instance.GetTeacherID();
        return (teacher >= 0) ? teacher : 0;
    }

    private int HostNowMsLocal() => Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

    private float RandomRangeSeconds(float a, float b)
    {
        if (_rng == null) _rng = new System.Random(12345);
        return Mathf.Lerp(a, b, (float)_rng.NextDouble());
    }

    private void CacheMoles()
    {
        _molesByIndex.Clear();

        MoleVisual[] moles;
        if (holesAndMolesRoot != null) moles = holesAndMolesRoot.GetComponentsInChildren<MoleVisual>(true);
        else if (MRSharedAnchorManager.Instance != null && MRSharedAnchorManager.Instance.contentRoot != null)
            moles = MRSharedAnchorManager.Instance.contentRoot.GetComponentsInChildren<MoleVisual>(true);
        else moles = FindObjectsOfType<MoleVisual>(true);

        foreach (var m in moles)
        {
            if (m == null) continue;
            if (m.HoleIndex < 0) continue;
            _molesByIndex[m.HoleIndex] = m;
        }
    }

    private int PickRandomHoleIndex()
    {
        if (_molesByIndex.Count == 0) return 0;
        if (_rng == null) _rng = new System.Random(12345);

        var keys = new List<int>(_molesByIndex.Keys);
        return keys[_rng.Next(keys.Count)];
    }

    private int CountUniqueAvatarOwners()
    {
        var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        HashSet<int> owners = new HashSet<int>();

        foreach (var a in avatars)
        {
            if (a == null) continue;
            var v = a.GetComponent<RealtimeView>();
            if (v != null && v.ownerIDInHierarchy >= 0)
                owners.Add(v.ownerIDInHierarchy);
        }
        return owners.Count;
    }

    private int OwnerToRole(int ownerId)
    {
        if (RoleManager.Instance == null) return 0;
        if (RoleManager.Instance.IsSolo()) return 0;

        int teacher = RoleManager.Instance.GetTeacherID();
        int student = RoleManager.Instance.GetStudentID();

        if (ownerId == teacher) return 0;
        if (ownerId == student) return 1;
        return 0;
    }

    private int GetCloserRoleToMole(int holeIndex)
    {
        if (!_molesByIndex.TryGetValue(holeIndex, out MoleVisual mole) || mole == null) return -1;

        var a = FindAvatarRigByRole(0);
        var b = FindAvatarRigByRole(1);

        Vector3 mp = mole.transform.position;

        if (a == null && b == null) return -1;
        if (a != null && b == null) return 0;
        if (a == null && b != null) return 1;

        float da = Vector3.Distance(a.GetRolePositionFallback(), mp);
        float db = Vector3.Distance(b.GetRolePositionFallback(), mp);

        return (da <= db) ? 0 : 1;
    }

    private AvatarRigRefs FindAvatarRigByRole(int role)
    {
        var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        foreach (var av in avatars)
        {
            if (av == null) continue;

            var rv = av.GetComponent<RealtimeView>();
            if (rv == null) continue;

            int owner = rv.ownerIDInHierarchy;
            if (role == 0 && OwnerToRole(owner) == 0) return av.GetComponent<AvatarRigRefs>();
            if (role == 1 && OwnerToRole(owner) == 1) return av.GetComponent<AvatarRigRefs>();
        }
        return null;
    }

    private void DriveNearCollisionMetrics()
    {
        if (_gs.GameState != 2) return;

        var a = FindAvatarRigByRole(0);
        var b = FindAvatarRigByRole(1);
        if (a == null || b == null) return;
        if (a.hammerColliders == null || b.hammerColliders == null) return;
        if (a.hammerColliders.Count == 0 || b.hammerColliders.Count == 0) return;

        int idx = 0;
        for (int i = 0; i < a.hammerColliders.Count && i < 2; i++)
        {
            for (int j = 0; j < b.hammerColliders.Count && j < 2; j++)
            {
                var ha = a.hammerColliders[i];
                var hb = b.hammerColliders[j];
                if (ha == null || hb == null) { idx++; continue; }

                float d = Vector3.Distance(ha.transform.position, hb.transform.position);

                bool nearNow = d <= nearThreshold;
                bool colNow = d <= collisionThreshold;

                if (nearNow && !_pairNear[idx]) { _pairNear[idx] = true; _nearEvents++; }
                if (!nearNow && _pairNear[idx]) { _pairNear[idx] = false; }

                if (colNow && !_pairCollide[idx]) { _pairCollide[idx] = true; _collisionEvents++; }
                if (!colNow && _pairCollide[idx]) { _pairCollide[idx] = false; }

                idx++;
            }
        }
    }

    private void TryResolveDyadIdFromLocalAvatar()
    {
        var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        foreach (var av in avatars)
        {
            var rv = av.GetComponent<RealtimeView>();
            if (rv == null || !rv.isOwnedLocallySelf) continue;

            var logger = av.GetComponent<PlayerKinematicsLogger>();
            if (logger != null && logger.avatarConfigData != null)
            {
                _dyadId = logger.avatarConfigData.dyadID;
                return;
            }
        }
    }

    private void WriteSummaryCsv()
    {
        if (!_isAuthority) return;

        string dir = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(dir);

        string baseName = $"{_dyadId}_{_condition}_Summary.csv";
        string path = Path.Combine(dir, baseName);

        if (File.Exists(path))
        {
            for (int k = 1; k < 1000; k++)
            {
                string tryName = $"{_dyadId}_{_condition}_Summary_{k}.csv";
                string tryPath = Path.Combine(dir, tryName);
                if (!File.Exists(tryPath)) { path = tryPath; break; }
            }
        }

        using (var w = new StreamWriter(path, false, Encoding.UTF8))
        {
            w.WriteLine("DyadID,Condition,HitsA,HitsB,MissesA,MissesB,NearEvents,CollisionEvents,CrossHitAonB,CrossHitBonA");
            w.WriteLine($"{_dyadId},{_condition},{_hitsA},{_hitsB},{_missesA},{_missesB},{_nearEvents},{_collisionEvents},{_crossHitAonB},{_crossHitBonA}");
        }

        Log($"Summary written -> {path}");
    }

    private void Log(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[WhackGameController] cid={(_realtime != null ? _realtime.clientID : -1)} {msg}");
    }
}
