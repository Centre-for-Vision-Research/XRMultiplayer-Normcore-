using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class WhackGameController : MonoBehaviour
{
    [Header("Scene References")]
    public Transform holesAndMolesRoot;

    [Header("Active Moles")]
    [Range(1, 3)]
    public int activeMoles = 2;              // N slots during RUNNING
    public int leadTimeMs = 250;

    [Header("Spawn Delay Range (seconds)")]
    public float spawnMin = 0.20f;
    public float spawnMax = 0.40f;

    [Header("Hold Duration Tiers (seconds)")]
    public float holdLowMin = 0.20f;
    public float holdLowMax = 0.30f;
    public float holdMidMin = 0.30f;
    public float holdMidMax = 0.40f;
    public float holdHighMin = 0.40f;
    public float holdHighMax = 0.55f;

    [Tooltip("Weights for [Low, Mid, High]. Balanced sampling will approximate these proportions.")]
    public Vector3 holdTierWeights = new Vector3(1f, 1f, 1f);

    [Header("Mole Type Weights (balanced)")]
    [Tooltip("Weights for TeacherSmall, StudentSmall, SharedSmall, SharedBig")]
    public Vector4 kindWeights = new Vector4(1f, 1f, 1.4f, 0.35f);

    [Header("Big Mole Settings")]
    [Tooltip("Seconds allowed for the second player to complete the big mole after first hit.")]
    public float bigSecondWindowSeconds = 0.65f;

    [Header("Block Settings")]
    public float blockDurationSeconds = 120f;
    public int postGameDelayMs = 1500;

    [Header("Return")]
    public bool autoReturnToLobby = true;
    public string lobbySceneName = "LobbyAvtrs";

    [Header("Conflict Bursts (optional)")]
    public bool enableConflictBursts = false;
    public float burstEveryMinSeconds = 10f;
    public float burstEveryMaxSeconds = 18f;
    public float burstDurationSeconds = 5f;

    [Tooltip("Multiplier applied to SharedSmall and SharedBig weights during a burst.")]
    public float burstSharedWeightMultiplier = 1.8f;

    [Header("Hole Clustering (optional)")]
    public bool enableSharedClustering = true;
    public float clusterDistanceMeters = 0.22f;

    [Header("Collision Metrics")]
    public bool countNearEvents = false;
    public float nearThreshold = 0.18f;
    public float collisionThreshold = 0.08f;

    [Header("Collision Audio")]
    public AudioClip collisionSfx;
    public float collisionSfxVolume = 0.9f;
    public float collisionSfxCooldownSeconds = 0.10f;

    [Header("Scoring (for logging only)")]
    public int scoreCorrectHit = 1;
    public int scoreWrongHit = -1;
    public int scoreBigComplete = 3;
    public float scoreCollisionPenalty = -0.25f; // applied per collision event
    public bool scoreUsesCollisionPenalty = true;

    [Header("Debug")]
    public bool verboseLogs = true;

    private Realtime _realtime;
    private WhackGameStateSync _gs;
    private RealtimeView _stateView;

    private System.Random _rng;
    private bool _isAuthority = false;
    private bool _authorityInitDone = false;
    private bool _sessionStarted = false;

    private readonly Dictionary<int, MoleVisual> _molesByIndex = new Dictionary<int, MoleVisual>();
    private readonly Dictionary<int, WhackPlayerInput> _inputsByOwner = new Dictionary<int, WhackPlayerInput>();

    private readonly HashSet<int> _seenResolveIds = new HashSet<int>();
    private readonly Queue<int> _seenResolveQueue = new Queue<int>();
    private const int SeenResolveCapacity = 128;

    private int _globalSeqCounter = 0;

    // per slot bookkeeping
    private int[] _lastResolvedSeqBySlot = new int[3] { -1, -1, -1 };
    private int[] _lastMissSeqBySlot = new int[3] { -1, -1, -1 };
    private int[] _lastHoleBySlot = new int[3] { -1, -1, -1 };

    private readonly Dictionary<int, int> _lastAcceptedSeqByClient = new Dictionary<int, int>();

    // metrics
    private int _teacherCorrect = 0;
    private int _studentCorrect = 0;
    private int _teacherWrong = 0;
    private int _studentWrong = 0;

    private int _missTeacherMoles = 0;
    private int _missStudentMoles = 0;
    private int _missSharedMoles = 0;

    private int _sharedHitsByTeacher = 0;
    private int _sharedHitsByStudent = 0;

    private int _bigComplete = 0;
    private int _bigOneHitOnly = 0;
    private int _bigMiss = 0;

    private int _nearEvents = 0;
    private int _collisionEvents = 0;
    private int _collisionDuringSharedActive = 0;
    private int _collisionDuringExclusiveOnly = 0;

    // collision edge state for 4 hammer pairs
    private bool[] _pairNear = new bool[4];
    private bool[] _pairCollide = new bool[4];
    private float _lastCollisionSfxTime = -999f;

    private float _nextHostClockPushTime = 0f;
    private float _nextInputScanTime = 0f;

    // burst scheduler
    private bool _inBurst = false;
    private int _burstEndHostMs = 0;
    private int _nextBurstHostMs = 0;

    // balanced samplers
    private BalancedPicker _kindPicker;
    private BalancedPicker _holdTierPicker;

    private string _dyadId = "UnknownDyad";
    private string _condition = "Unknown";

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

        if (_authorityAcquireLoop != null) StopCoroutine(_authorityAcquireLoop);
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

        EnsureStateViewIsBoundToRealtimeInstance();
        yield return null;

        // Wait until at least one avatar exists (roles depend on it)
        yield return new WaitUntil(() =>
        {
            var av = GameObject.FindGameObjectsWithTag("PlayerAvatar");
            return av != null && av.Length > 0;
        });

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

        Log($"Boot complete. cid={_realtime.clientID} isAuthority={_isAuthority} viewOwner={_stateView.ownerIDInHierarchy}");
    }

    private void EnsureStateViewIsBoundToRealtimeInstance()
    {
        if (_realtime == null || _stateView == null) return;

        if (_stateView.GetComponentInParent<Realtime>() == null)
        {
            _stateView.transform.SetParent(_realtime.transform, true);
            Log("StateView was not under a Realtime instance. Reparented under Realtime for binding.");
        }
    }

    private void OnGameStateOwnerChanged(RealtimeComponent<WhackGameStateModel> component, int newOwnerID)
    {
        UpdateAuthorityFlag();

        if (_isAuthority && !_authorityInitDone)
            AuthorityResetToWaiting();
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
                try { _stateView.RequestOwnership(); } catch { }
            }

            UpdateAuthorityFlag();

            if (_isAuthority && !_authorityInitDone)
                AuthorityResetToWaiting();

            yield return null;
        }
    }

    private void UpdateAuthorityFlag()
    {
        _isAuthority = (_gs != null && _gs.IsOwnedLocally);
    }

    private int HostNowMsLocal() => Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

    private void AuthorityResetToWaiting()
    {
        _authorityInitDone = true;

        // metrics reset
        _teacherCorrect = _studentCorrect = 0;
        _teacherWrong = _studentWrong = 0;
        _missTeacherMoles = _missStudentMoles = _missSharedMoles = 0;
        _sharedHitsByTeacher = _sharedHitsByStudent = 0;
        _bigComplete = _bigOneHitOnly = _bigMiss = 0;
        _nearEvents = _collisionEvents = 0;
        _collisionDuringSharedActive = _collisionDuringExclusiveOnly = 0;

        for (int i = 0; i < _pairNear.Length; i++) _pairNear[i] = false;
        for (int i = 0; i < _pairCollide.Length; i++) _pairCollide[i] = false;

        for (int s = 0; s < 3; s++)
        {
            _lastResolvedSeqBySlot[s] = -1;
            _lastMissSeqBySlot[s] = -1;
            _lastHoleBySlot[s] = -1;
        }

        _seenResolveIds.Clear();
        _seenResolveQueue.Clear();
        _lastAcceptedSeqByClient.Clear();

        _sessionStarted = false;
        _globalSeqCounter = 0;

        int seed = RoleManager.Instance != null ? RoleManager.Instance.GetCommonSeed() : 0;
        if (seed == 0) seed = 12345;

        _rng = new System.Random(seed);
        _gs.AuthoritySetSeed(seed);

        _kindPicker = new BalancedPicker(new float[] { kindWeights.x, kindWeights.y, kindWeights.z, kindWeights.w }, seed + 11);
        _holdTierPicker = new BalancedPicker(new float[] { holdTierWeights.x, holdTierWeights.y, holdTierWeights.z }, seed + 23);

        // burst schedule init (host time)
        int now = HostNowMsLocal();
        _inBurst = false;
        _burstEndHostMs = 0;
        _nextBurstHostMs = now + Mathf.RoundToInt(RandomRangeSeconds(burstEveryMinSeconds, burstEveryMaxSeconds) * 1000f);

        CacheMoles();
        int initialHole = PickRandomHoleIndex(avoid: new HashSet<int>());

        _gs.AuthoritySetHostNowMs(now);
        _gs.AuthoritySetGameTimes(0, 0, 0);

        // WAITING: keep 1 slot active (backward compatible start)
        _gs.AuthoritySetActiveSlotCount(1);

        _globalSeqCounter++;
        _gs.AuthorityScheduleSlot(
            slot: 0,
            seq: _globalSeqCounter,
            holeIndex: initialHole,
            startMs: now,
            endMs: now + 9999999,
            kind: MoleKind.SharedSmall,
            bigStage: 0,
            bigFirstRole: -1
        );

        TryResolveDyadIdFromLocalAvatar();
        _gs.AuthoritySetGameState(1);

        Log($"AUTH RESET -> WAITING. seed={seed} initialHole={initialHole} now={now}");
    }

    private void Update()
    {
        if (_realtime == null || !_realtime.connected || _realtime.clientID < 0) return;
        if (_gs == null || !_gs.IsModelReady()) return;
        if (_stateView == null) return;

        if (Time.time >= _nextInputScanTime)
        {
            _nextInputScanTime = Time.time + 0.20f;
            RefreshInputs();
        }

        if (_gs.GameState == 2) _sessionStarted = true;

        // local collision audio, for both clients
        UpdateCollisionAudioLocal();

        if (autoReturnToLobby && _sessionStarted && _gs.GameState == 3)
        {
            int hostNow = _gs.EstimateHostNowMs();
            int at = _gs.ReturnToLobbyAtMs;
            if (at > 0 && hostNow >= at)
                SceneManager.LoadScene(lobbySceneName);
        }

        if (!_isAuthority) return;

        DriveAuthority();
        DriveNearCollisionMetricsAuthority();
    }

    private void DriveAuthority()
    {
        int now = HostNowMsLocal();

        if (Time.realtimeSinceStartup >= _nextHostClockPushTime)
        {
            _nextHostClockPushTime = Time.realtimeSinceStartup + 0.033f;
            _gs.AuthoritySetHostNowMs(now);
        }

        if (_gs.GameState == 0) return;
        if (_gs.GameState == 1) return; // waiting for first hit

        if (_gs.GameState == 2)
        {
            UpdateBurstState(now);

            int gameEnd = _gs.GameEndMs;
            if (gameEnd > 0 && now >= gameEnd)
            {
                EndBlock(now);
                return;
            }

            int slots = Mathf.Clamp(activeMoles, 1, 3);
            for (int s = 0; s < slots; s++)
                CheckMissForSlot(s, now);
        }
    }

    private void UpdateBurstState(int now)
    {
        if (!enableConflictBursts) { _inBurst = false; return; }

        if (!_inBurst && now >= _nextBurstHostMs)
        {
            _inBurst = true;
            _burstEndHostMs = now + Mathf.RoundToInt(burstDurationSeconds * 1000f);
        }

        if (_inBurst && now >= _burstEndHostMs)
        {
            _inBurst = false;
            _nextBurstHostMs = now + Mathf.RoundToInt(RandomRangeSeconds(burstEveryMinSeconds, burstEveryMaxSeconds) * 1000f);
        }
    }

    private void StartRunningFromFirstHit(int now)
    {
        int blockStartMs = now;
        int blockEndMs = blockStartMs + Mathf.RoundToInt(blockDurationSeconds * 1000f);
        int returnMs = blockEndMs + postGameDelayMs;

        _gs.AuthoritySetGameTimes(blockStartMs, blockEndMs, returnMs);
        _gs.AuthoritySetGameState(2);

        int slots = Mathf.Clamp(activeMoles, 1, 3);
        _gs.AuthoritySetActiveSlotCount(slots);

        // schedule additional slots (1..slots-1) with staggered starts
        HashSet<int> avoid = new HashSet<int>();

        // FIX: do NOT access _gs.model (protected). Use TryGetSlot to get slot0 hole.
        if (_gs.TryGetSlot(0,
            out int s0Seq, out int s0Hole,
            out int s0StartMs, out int s0EndMs,
            out MoleKind s0Kind, out int s0BigStage, out int s0FirstRole))
        {
            if (s0Hole >= 0) avoid.Add(s0Hole);
        }

        for (int s = 1; s < slots; s++)
        {
            ScheduleNextFromSlot(s, now, avoid, forceSoon: true);
        }

        Log($"RUNNING started. slots={slots} startMs={blockStartMs} endMs={blockEndMs}");
    }

    private void EndBlock(int now)
    {
        _gs.AuthoritySetGameState(3);
        _gs.AuthoritySetGameTimes(_gs.GameStartMs, _gs.GameEndMs, now + postGameDelayMs);

        WriteSummaryCsv();
        Log("END block.");
    }

    // ---------------------------
    // Slot miss logic
    // ---------------------------
    private void CheckMissForSlot(int slot, int now)
    {
        if (!_gs.TryGetSlot(slot,
            out int seq, out int hole,
            out int slotStartMs, out int slotEndMs,
            out MoleKind slotKind,
            out int bigStage, out int bigFirstRole))
            return;

        if (hole < 0 || seq <= 0) return;

        if (now < slotStartMs) return;
        if (now <= slotEndMs) return;

        if (_lastMissSeqBySlot[slot] == seq || _lastResolvedSeqBySlot[slot] == seq) return;

        _lastMissSeqBySlot[slot] = seq;

        if (slotKind == MoleKind.SharedBig)
        {
            if (bigStage == 0) _bigMiss++;
            else if (bigStage == 1) _bigOneHitOnly++;

            _lastResolvedSeqBySlot[slot] = seq;
            ScheduleNextFromSlot(slot, now, avoid: null, forceSoon: false);
            return;
        }

        if (slotKind == MoleKind.TeacherSmall) _missTeacherMoles++;
        else if (slotKind == MoleKind.StudentSmall) _missStudentMoles++;
        else _missSharedMoles++;

        _lastResolvedSeqBySlot[slot] = seq;
        ScheduleNextFromSlot(slot, now, avoid: null, forceSoon: false);
    }

    // ---------------------------
    // Hit intake
    // ---------------------------
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

    private bool IsCorrectHit(MoleKind kind, int byRole)
    {
        if (kind == MoleKind.SharedSmall || kind == MoleKind.SharedBig) return true;
        if (kind == MoleKind.TeacherSmall) return byRole == 0;
        if (kind == MoleKind.StudentSmall) return byRole == 1;
        return true;
    }

    private bool AnySharedActiveNow(int hostNow)
    {
        int slots = _gs.ActiveSlotCount;
        const int graceMs = 60;

        for (int s = 0; s < slots; s++)
        {
            if (!_gs.TryGetSlot(s,
                out int seq, out int hole,
                out int slotStartMs, out int slotEndMs,
                out MoleKind slotKind,
                out int bigStage, out int firstRole))
                continue;

            if (hole < 0) continue;

            bool inWindow = hostNow >= (slotStartMs - graceMs) && hostNow <= (slotEndMs + graceMs);
            if (!inWindow) continue;

            if (slotKind == MoleKind.SharedSmall || slotKind == MoleKind.SharedBig)
                return true;
        }

        return false;
    }

    private void OnHitEventReceived(WhackPlayerInput sender, WhackPlayerInput.HitEvent e)
    {
        if (!_isAuthority) return;
        if (_gs == null || !_gs.IsModelReady()) return;

        int state = _gs.GameState;
        if (state != 1 && state != 2) return;

        int slot = Mathf.Clamp(e.slotIndex, 0, 2);

        if (!_gs.TryGetSlot(slot,
            out int seq, out int hole,
            out int slotStartMs, out int slotEndMs,
            out MoleKind slotKind,
            out int bigStage, out int bigFirstRole))
            return;

        if (e.seq != seq) return;
        if (e.holeIndex != hole) return;

        int byClientId = sender.OwnerClientIdInHierarchy;
        int byRole = OwnerToRole(byClientId);

        if (_lastAcceptedSeqByClient.TryGetValue(byClientId, out int lastSeq) && lastSeq == seq)
            return;
        _lastAcceptedSeqByClient[byClientId] = seq;

        if (_lastResolvedSeqBySlot[slot] == seq)
            return;

        int now = HostNowMsLocal();

        if (state == 1)
        {
            StartRunningFromFirstHit(now);
            // fall through to process the hit
        }

        const int windowGraceMs = 90;
        if (state == 2)
        {
            if (now < slotStartMs - windowGraceMs || now > slotEndMs + windowGraceMs)
                return;
        }

        // BIG mole logic
        if (slotKind == MoleKind.SharedBig)
        {
            if (bigStage == 0)
            {
                int deadline = now + Mathf.RoundToInt(bigSecondWindowSeconds * 1000f);

                _gs.AuthorityScheduleSlot(
                    slot, seq, hole,
                    slotStartMs, deadline,
                    slotKind,
                    bigStage: 1,
                    bigFirstRole: byRole
                );

                _gs.AuthorityEmitResolve(seq, hole, slot, type: 1, byRole: byRole, atHostMs: now, byClientId: byClientId,
                    kind: ResolveKind.BigFirst, moleKind: slotKind);

                return;
            }
            else if (bigStage == 1)
            {
                if (bigFirstRole == byRole) return;

                _bigComplete++;
                _lastResolvedSeqBySlot[slot] = seq;

                _gs.AuthorityEmitResolve(seq, hole, slot, type: 1, byRole: byRole, atHostMs: now, byClientId: byClientId,
                    kind: ResolveKind.BigComplete, moleKind: slotKind);

                ScheduleNextFromSlot(slot, now, avoid: null, forceSoon: false);
                return;
            }

            return;
        }

        // NORMAL mole hit
        bool correct = IsCorrectHit(slotKind, byRole);

        if (slotKind == MoleKind.SharedSmall)
        {
            if (byRole == 0) _sharedHitsByTeacher++;
            else _sharedHitsByStudent++;
        }

        if (correct)
        {
            if (byRole == 0) _teacherCorrect++;
            else _studentCorrect++;

            _gs.AuthorityEmitResolve(seq, hole, slot, type: 1, byRole: byRole, atHostMs: now, byClientId: byClientId,
                kind: ResolveKind.HitCorrect, moleKind: slotKind);
        }
        else
        {
            if (byRole == 0) _teacherWrong++;
            else _studentWrong++;

            _gs.AuthorityEmitResolve(seq, hole, slot, type: 1, byRole: byRole, atHostMs: now, byClientId: byClientId,
                kind: ResolveKind.HitWrong, moleKind: slotKind);
        }

        _lastResolvedSeqBySlot[slot] = seq;
        ScheduleNextFromSlot(slot, now, avoid: null, forceSoon: false);
    }

    // ---------------------------
    // Resolve FX (remote playback)
    // ---------------------------
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

        if (_realtime != null && info.byClientId == _realtime.clientID) return;

        if (_molesByIndex.TryGetValue(info.holeIndex, out MoleVisual m) && m != null)
            m.PlayHitFx(info.kind);
    }

    // ---------------------------
    // Scheduling
    // ---------------------------
    private float RandomRangeSeconds(float a, float b)
    {
        if (_rng == null) _rng = new System.Random(12345);
        return Mathf.Lerp(a, b, (float)_rng.NextDouble());
    }

    private int NextSeq() { _globalSeqCounter++; return _globalSeqCounter; }

    private void ScheduleNextFromSlot(int slot, int now, HashSet<int> avoid, bool forceSoon)
    {
        if (_rng == null) _rng = new System.Random(12345);

        // figure currently used holes (avoid duplicates)
        HashSet<int> used = new HashSet<int>();
        int slots = Mathf.Clamp(activeMoles, 1, 3);

        for (int s = 0; s < slots; s++)
        {
            // FIX: do not use out var names that collide with locals later in this method
            if (!_gs.TryGetSlot(s,
                out int curSeq, out int curHole,
                out int curStartMs, out int curEndMs,
                out MoleKind curKind,
                out int curBigStage, out int curFirstRole))
                continue;

            if (curHole < 0) continue;

            if (now <= curEndMs + 80)
                used.Add(curHole);
        }

        if (avoid != null)
            foreach (var h in avoid) used.Add(h);

        // pick mole kind (balanced)
        MoleKind newKind = PickBalancedKind();

        // choose hole
        int holeIndex = PickRandomHoleIndexAvoiding(
            used,
            preferCluster: (enableSharedClustering && (newKind == MoleKind.SharedSmall || newKind == MoleKind.SharedBig))
        );

        // no immediate repeat per slot
        if (_lastHoleBySlot[slot] >= 0 && holeIndex == _lastHoleBySlot[slot])
            holeIndex = PickRandomHoleIndexAvoiding(used, preferCluster: false);

        _lastHoleBySlot[slot] = holeIndex;

        // delay
        float baseDelay = RandomRangeSeconds(spawnMin, spawnMax);
        if (forceSoon) baseDelay = Mathf.Min(baseDelay, 0.18f);

        int delayMs = Mathf.RoundToInt(baseDelay * 1000f);

        // hold (tier balanced)
        int holdTier = _holdTierPicker.PickIndex();
        float hold = SampleHoldFromTier(holdTier);
        int holdMs = Mathf.RoundToInt(hold * 1000f);

        int newStartMs = now + Mathf.Max(leadTimeMs, delayMs);
        int newEndMs = newStartMs + holdMs;

        if (newKind == MoleKind.SharedBig)
        {
            newEndMs = newStartMs + Mathf.Max(holdMs, Mathf.RoundToInt((bigSecondWindowSeconds + 0.35f) * 1000f));
        }

        int seqNew = NextSeq();

        _gs.AuthorityScheduleSlot(
            slot: slot,
            seq: seqNew,
            holeIndex: holeIndex,
            startMs: newStartMs,
            endMs: newEndMs,
            kind: newKind,
            bigStage: 0,
            bigFirstRole: -1
        );
    }

    private float SampleHoldFromTier(int tier)
    {
        if (tier == 0) return RandomRangeSeconds(holdLowMin, holdLowMax);
        if (tier == 1) return RandomRangeSeconds(holdMidMin, holdMidMax);
        return RandomRangeSeconds(holdHighMin, holdHighMax);
    }

    private MoleKind PickBalancedKind()
    {
        float wT = kindWeights.x;
        float wS = kindWeights.y;
        float wSh = kindWeights.z;
        float wBig = kindWeights.w;

        if (enableConflictBursts && _inBurst)
        {
            wSh *= burstSharedWeightMultiplier;
            wBig *= burstSharedWeightMultiplier;
        }

        _kindPicker.SetWeights(new float[] { wT, wS, wSh, wBig });
        int idx = _kindPicker.PickIndex();

        if (idx == 0) return MoleKind.TeacherSmall;
        if (idx == 1) return MoleKind.StudentSmall;
        if (idx == 2) return MoleKind.SharedSmall;
        return MoleKind.SharedBig;
    }

    // ---------------------------
    // Hole picking
    // ---------------------------
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

    private int PickRandomHoleIndex(HashSet<int> avoid)
    {
        if (_rng == null) _rng = new System.Random(12345);

        var keys = new List<int>(_molesByIndex.Keys);
        if (keys.Count == 0) return 0;

        if (avoid != null && avoid.Count > 0)
            keys.RemoveAll(k => avoid.Contains(k));

        if (keys.Count == 0) keys = new List<int>(_molesByIndex.Keys);

        return keys[_rng.Next(keys.Count)];
    }

    private int PickRandomHoleIndexAvoiding(HashSet<int> avoid, bool preferCluster)
    {
        if (_molesByIndex.Count == 0) return 0;
        if (_rng == null) _rng = new System.Random(12345);

        if (!preferCluster || _gs == null || !_gs.IsModelReady())
            return PickRandomHoleIndex(avoid);

        // try cluster near currently active holes
        List<int> candidateTargets = new List<int>();
        int slots = Mathf.Clamp(activeMoles, 1, 3);
        int hostNow = _gs.EstimateHostNowMs();

        for (int s = 0; s < slots; s++)
        {
            if (_gs.TryGetSlot(s,
                out int seq, out int hole,
                out int slotStartMs, out int slotEndMs,
                out MoleKind slotKind,
                out int bs, out int fr))
            {
                if (hole >= 0 && hostNow <= slotEndMs + 80)
                    candidateTargets.Add(hole);
            }
        }

        if (candidateTargets.Count == 0)
            return PickRandomHoleIndex(avoid);

        List<int> clustered = new List<int>();
        foreach (var kv in _molesByIndex)
        {
            int holeIdx = kv.Key;
            if (avoid != null && avoid.Contains(holeIdx)) continue;

            Vector3 p = kv.Value.transform.position;

            bool nearAny = false;
            foreach (int t in candidateTargets)
            {
                if (!_molesByIndex.TryGetValue(t, out MoleVisual mv) || mv == null) continue;
                float d = Vector3.Distance(p, mv.transform.position);
                if (d <= clusterDistanceMeters) { nearAny = true; break; }
            }

            if (nearAny) clustered.Add(holeIdx);
        }

        if (clustered.Count == 0)
            return PickRandomHoleIndex(avoid);

        return clustered[_rng.Next(clustered.Count)];
    }

    // ---------------------------
    // Collision metrics (authority) and local audio
    // ---------------------------
    private void UpdateCollisionAudioLocal()
    {
        if (collisionSfx == null) return;
        if (Time.realtimeSinceStartup - _lastCollisionSfxTime < collisionSfxCooldownSeconds) return;

        bool newCollision = DetectAnyCollisionEdge(out bool newNearEdge);

        if (newCollision)
        {
            _lastCollisionSfxTime = Time.realtimeSinceStartup;
            AudioSource.PlayClipAtPoint(collisionSfx, transform.position, collisionSfxVolume);
        }
    }

    private bool DetectAnyCollisionEdge(out bool newNearEdge)
    {
        newNearEdge = false;

        var a = FindAvatarRigByRole(0);
        var b = FindAvatarRigByRole(1);
        if (a == null || b == null) return false;
        if (a.hammerColliders == null || b.hammerColliders == null) return false;
        if (a.hammerColliders.Count == 0 || b.hammerColliders.Count == 0) return false;

        bool anyNewCollision = false;
        bool anyNewNear = false;

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

                if (countNearEvents)
                {
                    if (nearNow && !_pairNear[idx]) { anyNewNear = true; _pairNear[idx] = true; }
                    if (!nearNow && _pairNear[idx]) { _pairNear[idx] = false; }
                }

                if (colNow && !_pairCollide[idx]) { anyNewCollision = true; _pairCollide[idx] = true; }
                if (!colNow && _pairCollide[idx]) { _pairCollide[idx] = false; }

                idx++;
            }
        }

        newNearEdge = anyNewNear;
        return anyNewCollision;
    }

    private void DriveNearCollisionMetricsAuthority()
    {
        if (_gs.GameState != 2) return;

        bool newCollision = DetectAnyCollisionEdge(out bool newNear);

        if (countNearEvents && newNear) _nearEvents++;

        if (newCollision)
        {
            _collisionEvents++;

            int hostNow = _gs.EstimateHostNowMs();
            bool sharedActive = AnySharedActiveNow(hostNow);

            if (sharedActive) _collisionDuringSharedActive++;
            else _collisionDuringExclusiveOnly++;
        }
    }

    // ---------------------------
    // Avatar rig lookup helpers
    // ---------------------------
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

    // ---------------------------
    // Summary CSV
    // ---------------------------
    private float ComputeFinalScore()
    {
        float score = 0f;

        score += _teacherCorrect * scoreCorrectHit;
        score += _studentCorrect * scoreCorrectHit;

        score += _teacherWrong * scoreWrongHit;
        score += _studentWrong * scoreWrongHit;

        score += _bigComplete * scoreBigComplete;

        if (scoreUsesCollisionPenalty)
            score += _collisionEvents * scoreCollisionPenalty;

        return score;
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

        float finalScore = ComputeFinalScore();

        using (var w = new StreamWriter(path, false, Encoding.UTF8))
        {
            w.WriteLine(string.Join(",",
            "DyadID","Condition",
            "A_Correct","B_Correct","A_Wrong","B_Wrong",
            "Miss_A_Moles","Miss_B_Moles","Miss_Shared_Moles",
            "SharedHits_A","SharedHits_B",
            "Big_Complete","Big_OneHitOnly","Big_Miss",
            "Near_Events","Collision_Events","Collision_SharedActive","Collision_ExclusiveOnly",
            "FinalScore"
        ));

            w.WriteLine(string.Join(",",
                _dyadId, _condition,
                _teacherCorrect, _studentCorrect, _teacherWrong, _studentWrong,
                _missTeacherMoles, _missStudentMoles, _missSharedMoles,
                _sharedHitsByTeacher, _sharedHitsByStudent,
                _bigComplete, _bigOneHitOnly, _bigMiss,
                _nearEvents, _collisionEvents, _collisionDuringSharedActive, _collisionDuringExclusiveOnly,
                finalScore.ToString("0.###", CultureInfo.InvariantCulture)
            ));
        }

        Log($"Summary written -> {path}");
    }

    private void Log(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[WhackGameController] cid={(_realtime != null ? _realtime.clientID : -1)} {msg}");
    }

    // ---------------------------
    // Balanced picker helper
    // ---------------------------
    private class BalancedPicker
    {
        private float[] _weights;
        private int[] _counts;
        private int _total;
        private System.Random _r;

        public BalancedPicker(float[] weights, int seed)
        {
            _r = new System.Random(seed);
            SetWeights(weights);
            _counts = new int[_weights.Length];
            _total = 0;
        }

        public void SetWeights(float[] w)
        {
            _weights = new float[w.Length];
            Array.Copy(w, _weights, w.Length);
        }

        public int PickIndex()
        {
            int n = _weights.Length;

            float sum = 0f;
            for (int i = 0; i < n; i++) sum += Mathf.Max(0.0001f, _weights[i]);

            _total++;

            int best = 0;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < n; i++)
            {
                float wi = Mathf.Max(0.0001f, _weights[i]) / sum;
                float expected = _total * wi;
                float deficit = expected - _counts[i];

                float jitter = (float)_r.NextDouble() * 0.0001f;
                float score = deficit + jitter;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            _counts[best]++;
            return best;
        }
    }
}
