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
    public float holdMin = 0.20f;
    public float holdMax = 0.50f;

    [Header("Block Settings")]
    public float blockDurationSeconds = 120f;
    public int leadTimeMs = 150;
    public int postGameDelayMs = 1500;
    public bool autoReturnToLobby = true;
    public string lobbySceneName = "LobbyAvtrs";

    [Header("Collision Metrics")]
    public float nearThreshold = 0.18f;
    public float collisionThreshold = 0.08f;

    [Header("Debug")]
    public bool verboseLogs = true;

    private Realtime _realtime;
    private WhackGameStateSync _gs;

    private bool _isAuthority = false;
    private System.Random _rng;
    private bool _initialized = false;

    private readonly Dictionary<int, MoleVisual> _molesByIndex = new Dictionary<int, MoleVisual>();
    private readonly Dictionary<int, WhackPlayerInput> _inputsByOwner = new Dictionary<int, WhackPlayerInput>();

    private int _resolveEventCounter = 0;

    private string _dyadId = "UnknownDyad";
    private string _condition = "Unknown";
    private bool _countedStartHit = false;

    private int _hitsA = 0;
    private int _hitsB = 0;
    private int _missesA = 0;
    private int _missesB = 0;
    private int _nearEvents = 0;
    private int _collisionEvents = 0;
    private int _crossHitAonB = 0;
    private int _crossHitBonA = 0;

    private bool[] _pairNear = new bool[4];
    private bool[] _pairCollide = new bool[4];

    private float _nextHostClockPushTime = 0f;

    private void Awake()
    {
        _realtime = FindObjectOfType<Realtime>();
        _gs = GetComponent<WhackGameStateSync>();
        _condition = WhackConditionUtil.GetConditionFromScene();
    }

    private void Start()
    {
        StartCoroutine(Boot());
    }

    private IEnumerator Boot()
    {
        while (_realtime == null)
        {
            _realtime = FindObjectOfType<Realtime>();
            yield return null;
        }
        while (_realtime.clientID < 0) yield return null;

        if (MRSharedAnchorManager.Instance != null && MRSharedAnchorManager.Instance.isMRScene)
            yield return new WaitUntil(() => MRSharedAnchorManager.Instance.AnchorReady);

        yield return new WaitUntil(() => RoleManager.Instance != null);

        float startWait = Time.realtimeSinceStartup;
        while (!RoleManager.Instance.IsDyadReady() && !RoleManager.Instance.IsSolo())
        {
            if (Time.realtimeSinceStartup - startWait > 10f) break;
            yield return null;
        }

        // Wait for at least one avatar to exist
        yield return new WaitUntil(() =>
        {
            var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
            return avatars != null && avatars.Length > 0;
        });

        // Wait for game state model
        yield return new WaitUntil(() => _gs != null && _gs.IsModelReady());

        _isAuthority = IsLocalTeacherAuthority();

        CacheMoles();

        if (_isAuthority) {
            var rv = GetComponent<RealtimeView>();
            if (rv != null) rv.RequestOwnership();
            yield return null;

            int seed = RoleManager.Instance.GetCommonSeed();
            if (seed == 0) seed = 12345;

            _rng = new System.Random(seed);
            _gs.AuthoritySetSeed(seed);

            int now = HostNowMsLocal();
            _gs.AuthoritySetHostNowMs(now);

            // IMPORTANT: reset any stale times from previous runs
            _gs.AuthoritySetGameTimes(0, 0, 0);

            int initialHole = PickRandomHoleIndex();
            _gs.AuthorityScheduleMole(seq: 0, holeIndex: initialHole, startMs: now, endMs: now + 9999999);

            _gs.AuthoritySetGameState(1);  // WaitingForStartHit
            _countedStartHit = false;

            TryResolveDyadIdFromLocalAvatar();
            _initialized = true;

            Log($"AUTH init: seed={seed} initialHole={initialHole}");
        }

        if (_gs != null) _gs.ResolveEvent += OnResolveEvent;
        Log($"Boot done. cid={_realtime.clientID} isAuthority={_isAuthority} gameState={_gs.GameState} hole={_gs.CurrentHoleIndex}");
    }

    private void OnDestroy()
    {
        if (_gs != null) _gs.ResolveEvent -= OnResolveEvent;

        foreach (var kv in _inputsByOwner)
            if (kv.Value != null) kv.Value.HitEventReceived -= OnHitEventReceived;

        _inputsByOwner.Clear();
    }

    private void Update()
    {
        RefreshInputs();

        if (_gs == null) return;

        if (autoReturnToLobby && _gs.GameState == 3)
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

    private void DriveAuthority() {
        int now = HostNowMsLocal();

        if (Time.realtimeSinceStartup >= _nextHostClockPushTime) {
            _nextHostClockPushTime = Time.realtimeSinceStartup + 0.05f;
            _gs.AuthoritySetHostNowMs(now);
        }

        int state = _gs.GameState;

        if (state == 1) return;

        if (state == 2) {
            // IMPORTANT: only end if GameEndMs is valid
            int gameEnd = _gs.GameEndMs;
            if (gameEnd > 0 && now >= gameEnd) {
                EndBlock(now);
                return;
            }

            // Miss detection also needs valid window
            int moleEnd = _gs.MoleEndMs;
            if (moleEnd > 0 && now > moleEnd) {
                ResolveMiss(now);
            }
        }
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
        ScheduleNextFrom(now);
    }

    // UPDATED: includes byClientId
    private void EmitResolve(int type, int byRole, int holeIndex, int seq, int atHostMs, int byClientId)
    {
        _resolveEventCounter++;
        _gs.AuthorityEmitResolve(_resolveEventCounter, seq, holeIndex, type, byRole, atHostMs, byClientId);
    }

    private void ScheduleNextFrom(int now)
    {
        int delayMs = Mathf.RoundToInt(RandomRangeSeconds(spawnMin, spawnMax) * 1000f);
        int holdMs = Mathf.RoundToInt(RandomRangeSeconds(holdMin, holdMax) * 1000f);

        int nextStart = now + Mathf.Max(leadTimeMs, delayMs);
        int nextEnd = nextStart + holdMs;

        int nextHole = PickRandomHoleIndex();
        int nextSeq = _gs.CurrentSeq + 1;

        _gs.AuthorityScheduleMole(nextSeq, nextHole, nextStart, nextEnd);
    }

    private float RandomRangeSeconds(float a, float b)
    {
        if (_rng == null) _rng = new System.Random(12345);
        double t = _rng.NextDouble();
        return Mathf.Lerp(a, b, (float)t);
    }

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

            Log($"Registered input owner={owner}");
        }
    }

    private void OnHitEventReceived(WhackPlayerInput sender, WhackPlayerInput.HitEvent e)
    {
        if (!_isAuthority) return;
        if (_gs == null) return;

        int byRole = OwnerToRole(sender.OwnerClientIdInHierarchy);
        int byClientId = sender.OwnerClientIdInHierarchy;

        int curHole = _gs.CurrentHoleIndex;
        int curSeq = _gs.CurrentSeq;

        if (e.holeIndex != curHole) return;
        if (e.seq != curSeq) return;

        int now = HostNowMsLocal();

        if (_gs.GameState == 1)
        {
            StartRunningFromHit(now);
            // still emit resolve so other client gets synced hit FX if you want,
            // but do not count hit. Also pass byClientId so hitter suppresses echo.
            EmitResolve(type: 1, byRole: byRole, holeIndex: curHole, seq: curSeq, atHostMs: now, byClientId: byClientId);
            ScheduleNextFrom(now);
            return;
        }

        if (_gs.GameState != 2) return;

        if (_countedStartHit)
        {
            if (byRole == 0) _hitsA++;
            else if (byRole == 1) _hitsB++;

            int closerRole = GetCloserRoleToMole(curHole);
            if (byRole == 0 && closerRole == 1) _crossHitAonB++;
            if (byRole == 1 && closerRole == 0) _crossHitBonA++;
        }

        EmitResolve(type: 1, byRole: byRole, holeIndex: curHole, seq: curSeq, atHostMs: now, byClientId: byClientId);
        ScheduleNextFrom(now);
    }

    private void StartRunningFromHit(int now)
    {
        int startMs = now;
        int endMs = startMs + Mathf.RoundToInt(blockDurationSeconds * 1000f);
        int returnMs = endMs + postGameDelayMs;

        _gs.AuthoritySetGameTimes(startMs, endMs, returnMs);
        _gs.AuthoritySetGameState(2);

        _countedStartHit = true;
        Log("RUNNING started from first hit");
    }

    private void OnResolveEvent(WhackGameStateSync.ResolveInfo info)
    {
        if (!_initialized && !_isAuthority) return;

        if (info.type != 1) return;

        // IMPORTANT: suppress echo on the hitter client (solo + multiplayer)
        if (_realtime != null && info.byClientId == _realtime.clientID)
            return;

        if (_molesByIndex.TryGetValue(info.holeIndex, out MoleVisual m) && m != null)
        {
            Log($"Playing HIT FX (synced) on mole='{m.name}' holeIndex={info.holeIndex} byClientId={info.byClientId}");
            m.PlayHitFx(isLocalHitter: false);
        }
        else
        {
            Log($"ResolveEvent type=1 but no MoleVisual cached for holeIndex={info.holeIndex}");
        }
    }

    private bool IsLocalTeacherAuthority()
    {
        if (RoleManager.Instance == null || _realtime == null) return false;
        int cid = _realtime.clientID;
        if (RoleManager.Instance.IsSolo()) return true;
        return RoleManager.Instance.IsTeacher(cid);
    }

    private int HostNowMsLocal()
    {
        return Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);
    }

    private void CacheMoles()
    {
        _molesByIndex.Clear();

        if (holesAndMolesRoot == null)
        {
            var go = GameObject.Find("Holes & Moles");
            holesAndMolesRoot = go != null ? go.transform : null;
        }

        var moles = FindObjectsOfType<MoleVisual>(true);
        foreach (var m in moles)
        {
            if (m == null) continue;
            if (m.HoleIndex < 0) continue;

            _molesByIndex[m.HoleIndex] = m;
        }

        Log($"Cached moles: {_molesByIndex.Count}");
    }

    private int PickRandomHoleIndex()
    {
        if (_molesByIndex.Count > 0)
        {
            var keys = new List<int>(_molesByIndex.Keys);
            return keys[_rng.Next(keys.Count)];
        }
        return _rng.Next(0, 13);
    }

    private int OwnerToRole(int ownerId)
    {
        if (RoleManager.Instance == null) return 0;

        int teacher = RoleManager.Instance.GetTeacherID();
        int student = RoleManager.Instance.GetStudentID();

        if (RoleManager.Instance.IsSolo()) return 0;
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
            if (role == 0 && OwnerToRole(owner) == 0)
                return av.GetComponent<AvatarRigRefs>();
            if (role == 1 && OwnerToRole(owner) == 1)
                return av.GetComponent<AvatarRigRefs>();
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
