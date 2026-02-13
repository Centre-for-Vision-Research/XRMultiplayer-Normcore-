using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Normal.Realtime;

public class WhackTutorialController : MonoBehaviour
{
    [Header("Scene References")]
    public Transform holesAndMolesRoot;
    public TMP_Text instructionText;

    [Header("UI Buttons (optional if you wire manually)")]
    public UnityEngine.UI.Button replayButton;
    public UnityEngine.UI.Button quitButton;

    [Header("Practice")]
    public float practiceDurationSeconds = 30f;
    public int practiceRespawnDelayMs = 250;

    [Header("Big Mole")]
    public float bigSecondWindowSeconds = 0.65f;

    [Header("Return")]
    public string lobbySceneName = "LobbyAvtrs";

    [Header("Hammer Visuals")]
    public Material hammerMatA;   // Teacher / Role A (e.g., Blue)
    public Material hammerMatB;   // Student / Role B (e.g., Green)
    public string hammerTag = "Hammer";
    public bool hammerUseTagSearch = true;
    public bool hammerAlsoSearchByName = true;

    [Header("Debug")]
    public bool verboseLogs = true;

    private Realtime _realtime;
    private WhackGameStateSync _gs;
    private RealtimeView _stateView;

    private readonly Dictionary<int, MoleVisual> _molesByIndex = new Dictionary<int, MoleVisual>();
    private readonly Dictionary<int, WhackPlayerInput> _inputsByOwner = new Dictionary<int, WhackPlayerInput>();
    private readonly HashSet<int> _seenResolveIds = new HashSet<int>();
    private readonly Queue<int> _seenResolveQueue = new Queue<int>();
    private const int SeenResolveCapacity = 128;

    private System.Random _rng;
    private int _seqCounter = 0;

    private bool _isAuthority;
    private bool _authorityInitDone;

    // Step completion flags (authority only)
    private bool _aDone, _bDone;
    private bool _brown0Done, _brown1Done;

    private float _nextHostClockPush = 0f;
    private float _nextInputScanTime = 0f;

    // hammer styling
    private readonly HashSet<int> _styledHammerOwners = new HashSet<int>();
    private bool _hammerStyleReady = false;

    private enum TutorialPhase
    {
        Colors = 1,
        Brown = 2,
        Big = 3,
        Practice = 4,
        PracticeEnded = 5
    }

    private void Start()
    {
        if (replayButton != null) replayButton.onClick.AddListener(ReplayTutorial);
        if (quitButton != null) quitButton.onClick.AddListener(QuitToLobby);

        StartCoroutine(Boot());
    }

    private void OnDestroy()
    {
        if (_gs != null)
            _gs.ResolveEvent -= OnResolveEvent;

        foreach (var kv in _inputsByOwner)
            if (kv.Value != null) kv.Value.HitEventReceived -= OnHitEventReceived;

        _inputsByOwner.Clear();
    }

    private IEnumerator Boot()
    {
        _realtime = FindObjectOfType<Realtime>();
        while (_realtime == null) { _realtime = FindObjectOfType<Realtime>(); yield return null; }

        yield return new WaitUntil(() => _realtime.connected && _realtime.clientID >= 0);

        if (MRSharedAnchorManager.Instance != null && MRSharedAnchorManager.Instance.isMRScene)
            yield return new WaitUntil(() => MRSharedAnchorManager.Instance.AnchorReady);

        yield return new WaitUntil(() => RoleManager.Instance != null);

        _gs = FindObjectOfType<WhackGameStateSync>(true);
        if (_gs == null)
        {
            Debug.LogError("[WhackTutorialController] Missing WhackGameStateSync in scene.");
            yield break;
        }

        yield return new WaitUntil(() => _gs.IsModelReady());

        _stateView = _gs.GetComponent<RealtimeView>();
        if (_stateView == null) _stateView = _gs.GetComponentInParent<RealtimeView>();
        if (_stateView == null)
        {
            Debug.LogError("[WhackTutorialController] Missing RealtimeView for WhackGameStateSync.");
            yield break;
        }

        EnsureStateViewIsBoundToRealtimeInstance();

        CacheMoles();
        if (_molesByIndex.Count == 0)
        {
            Debug.LogError("[WhackTutorialController] No moles found. Assign holesAndMolesRoot.");
            yield break;
        }

        _gs.ResolveEvent += OnResolveEvent;

        // Authority loop runs implicitly in Update by repeated checks
        UpdateAuthorityFlag();

        Log($"Boot complete. cid={_realtime.clientID} isAuthority={_isAuthority}");
    }

    private void EnsureStateViewIsBoundToRealtimeInstance()
    {
        if (_realtime == null || _stateView == null) return;
        if (_stateView.GetComponentInParent<Realtime>() == null)
            _stateView.transform.SetParent(_realtime.transform, true);
    }

    private void UpdateAuthorityFlag()
    {
        _isAuthority = (_gs != null && _gs.IsOwnedLocally);
    }

    private void Update()
    {
        if (_realtime == null || !_realtime.connected || _realtime.clientID < 0) return;
        if (_gs == null || !_gs.IsModelReady()) return;
        if (_stateView == null) return;

        // Try to claim authority if we are teacher
        TryClaimAuthority();

        // Local UI refresh
        UpdateInstructionUI();

        // Periodic input discovery + hammer styling
        if (Time.time >= _nextInputScanTime)
        {
            _nextInputScanTime = Time.time + 0.20f;
            RefreshInputs();
            ApplyHammerMaterialsLocal();
        }

        if (!_isAuthority) return;

        // Push host clock for everyone
        if (Time.realtimeSinceStartup >= _nextHostClockPush)
        {
            _nextHostClockPush = Time.realtimeSinceStartup + 0.033f;
            _gs.AuthoritySetHostNowMs(HostNowMsLocal());
        }

        if (!_authorityInitDone)
            AuthorityInitIfNeeded();

        // End practice when timer expires
        var phase = InferPhase(out bool practiceRunning, out bool practiceEnded);
        if (phase == TutorialPhase.Practice && practiceEnded)
        {
            AuthorityEndPractice();
        }
    }

    private void TryClaimAuthority()
    {
        if (RoleManager.Instance == null) return;

        int teacherId = RoleManager.Instance.GetTeacherID();
        bool shouldOwn = (teacherId >= 0 && _realtime.clientID == teacherId);

        if (shouldOwn && !_stateView.isOwnedLocallySelf)
        {
            try { _stateView.RequestOwnership(); } catch { }
        }

        UpdateAuthorityFlag();
    }

    private int HostNowMsLocal() => Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

    private void AuthorityInitIfNeeded()
    {
        if (!_isAuthority) return;

        _authorityInitDone = true;

        int seed = RoleManager.Instance != null ? RoleManager.Instance.GetCommonSeed() : 12345;
        if (seed == 0) seed = 12345;
        _rng = new System.Random(seed + 777);

        _seqCounter = 0;
        _aDone = _bDone = false;
        _brown0Done = _brown1Done = false;

        int now = HostNowMsLocal();
        _gs.AuthoritySetHostNowMs(now);

        // Use state 2 so MoleVisual stays active
        _gs.AuthoritySetGameState(2);

        // Clear any old practice timer info
        _gs.AuthoritySetGameTimes(0, 0, 0);

        SetupStep1(now);

        Log("Authority init complete. Tutorial Step 1 scheduled.");
    }

    // --------------------------
    // Phase inference (late join safe)
    // --------------------------
    private TutorialPhase InferPhase(out bool practiceRunning, out bool practiceEnded)
    {
        practiceRunning = false;
        practiceEnded = false;

        int hostNow = _gs.EstimateHostNowMs();
        int end = _gs.GameEndMs;
        if (end > 0)
        {
            if (hostNow < end) practiceRunning = true;
            else practiceEnded = true;
        }

        int slots = _gs.ActiveSlotCount;

        if (slots == 2)
        {
            _gs.TryGetSlot(0, out _, out _, out _, out _, out MoleKind k0, out _, out _);
            _gs.TryGetSlot(1, out _, out _, out _, out _, out MoleKind k1, out _, out _);

            bool colors = (k0 == MoleKind.TeacherSmall && k1 == MoleKind.StudentSmall) ||
                          (k1 == MoleKind.TeacherSmall && k0 == MoleKind.StudentSmall);

            if (colors) return TutorialPhase.Colors;

            if (k0 == MoleKind.SharedSmall && k1 == MoleKind.SharedSmall)
                return TutorialPhase.Brown;
        }

        if (slots == 1)
        {
            _gs.TryGetSlot(0, out _, out _, out _, out _, out MoleKind k0, out _, out _);

            if (k0 == MoleKind.SharedBig) return TutorialPhase.Big;

            if (k0 == MoleKind.SharedSmall)
            {
                if (practiceEnded) return TutorialPhase.PracticeEnded;
                return TutorialPhase.Practice;
            }
        }

        return TutorialPhase.Colors;
    }

    private void UpdateInstructionUI()
    {
        if (instructionText == null) return;

        var phase = InferPhase(out bool practiceRunning, out bool practiceEnded);

        if (phase == TutorialPhase.Colors)
        {
            instructionText.text =
                "Goal: score points by hitting the moles.\n\n" +
                "Player A (blue hammers): do not hit green moles.\n" +
                "Player B (green hammers): do not hit blue moles.\n\n" +
                "Hit the mole that matches your hammer color to continue.";
            return;
        }

        if (phase == TutorialPhase.Brown)
        {
            instructionText.text =
                "Brown moles: either player can hit them.\n\n" +
                "Avoid hitting your partner's hammers.\n" +
                "Hammer Collisions reduce score a little, but hits increase score more.\n\n" +
                "Hit both brown moles to continue.";
            return;
        }

        if (phase == TutorialPhase.Big)
        {
            instructionText.text =
                "Big brown mole: both players must hit it one after the other.\n\n" +
                "It completes only when both players hit it.\n\n" +
                "It is worth 3x points! Give it a try!";
            return;
        }

        if (phase == TutorialPhase.PracticeEnded)
        {
            instructionText.text =
                "Practice finished.\n\n" +
                "You can restart the tutorial or quit to the lobby.";
            return;
        }

        // Practice phase (waiting or running)
        int hostNow = _gs.EstimateHostNowMs();
        int end = _gs.GameEndMs;

        if (!practiceRunning && end == 0)
        {
            instructionText.text =
                "Practice: hit a brown mole to start a practice round.\n\n" +
                "You can quit anytime.";
        }
        else
        {
            float left = Mathf.Max(0f, (end - hostNow) / 1000f);
            instructionText.text =
                $"Practice running. Time left: {left:0}s\n\n" +
                "Keep hitting brown moles.";
        }
    }

    // --------------------------
    // Step scheduling (authority)
    // --------------------------
    private int NextSeq() { _seqCounter++; return _seqCounter; }

    private void SetupStep1(int now)
    {
        _aDone = _bDone = false;

        _gs.AuthoritySetActiveSlotCount(2);

        int holeA = PickRandomHoleIndex(null);
        int holeB = PickRandomHoleIndex(new HashSet<int> { holeA });

        int end = now + (60 * 60 * 1000); // 1 hour

        _gs.AuthorityScheduleSlot(0, NextSeq(), holeA, now, end, MoleKind.TeacherSmall, 0, -1);
        _gs.AuthorityScheduleSlot(1, NextSeq(), holeB, now, end, MoleKind.StudentSmall, 0, -1);

        _gs.AuthoritySetGameTimes(0, 0, 0);
    }

    private void SetupStep2(int now)
    {
        _brown0Done = _brown1Done = false;

        _gs.AuthoritySetActiveSlotCount(2);

        int hole0 = PickRandomHoleIndex(null);
        int hole1 = PickRandomHoleIndex(new HashSet<int> { hole0 });

        int end = now + (60 * 60 * 1000);

        _gs.AuthorityScheduleSlot(0, NextSeq(), hole0, now, end, MoleKind.SharedSmall, 0, -1);
        _gs.AuthorityScheduleSlot(1, NextSeq(), hole1, now, end, MoleKind.SharedSmall, 0, -1);

        _gs.AuthoritySetGameTimes(0, 0, 0);
    }

    private void SetupStep3(int now)
    {
        _gs.AuthoritySetActiveSlotCount(1);

        int hole = PickRandomHoleIndex(null);
        int end = now + (60 * 60 * 1000);

        _gs.AuthorityScheduleSlot(0, NextSeq(), hole, now, end, MoleKind.SharedBig, 0, -1);

        _gs.AuthoritySetGameTimes(0, 0, 0);
    }

    private void SetupStep4Practice(int now)
    {
        _gs.AuthoritySetActiveSlotCount(1);

        int hole = PickRandomHoleIndex(null);
        int end = now + (60 * 60 * 1000);

        _gs.AuthorityScheduleSlot(0, NextSeq(), hole, now, end, MoleKind.SharedSmall, 0, -1);

        // Timer not started yet
        _gs.AuthoritySetGameTimes(0, 0, 0);
    }

    private void ScheduleNextPracticeMole(int now)
    {
        int endHost = _gs.GameEndMs;
        if (endHost > 0 && now >= endHost) return;

        int hole = PickRandomHoleIndex(null);

        int start = now + Mathf.Max(120, practiceRespawnDelayMs);
        int end = start + (60 * 60 * 1000);

        _gs.AuthorityScheduleSlot(0, NextSeq(), hole, start, end, MoleKind.SharedSmall, 0, -1);
    }

    private void AuthorityEndPractice()
    {
        int now = HostNowMsLocal();

        // Hide everything by scheduling a "no hole"
        _gs.AuthoritySetActiveSlotCount(1);
        _gs.AuthorityScheduleSlot(0, NextSeq(), -1, now, now, MoleKind.SharedSmall, 0, -1);

        Log("Practice ended.");
    }

    // IMPORTANT: Wrong-hit reset for Step 1 so colliders do not stay disabled.
    // This forces a tiny despawn/respawn with a fresh seq.
    private void AuthorityResetMoleAfterWrongHit(int slot, int hole, MoleKind kind, int now)
    {
        if (!_isAuthority) return;

        int offSeq = NextSeq();
        _gs.AuthorityScheduleSlot(slot, offSeq, -1, now, now + 80, kind, 0, -1);

        int onSeq = NextSeq();
        int start = now + 120;
        int end = now + (60 * 60 * 1000);
        _gs.AuthorityScheduleSlot(slot, onSeq, hole, start, end, kind, 0, -1);
    }

    // --------------------------
    // Hit intake (authority)
    // --------------------------
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

    private void OnHitEventReceived(WhackPlayerInput sender, WhackPlayerInput.HitEvent e)
    {
        if (!_isAuthority) return;
        if (_gs == null || !_gs.IsModelReady()) return;

        int slot = Mathf.Clamp(e.slotIndex, 0, 2);

        if (!_gs.TryGetSlot(slot,
            out int seq, out int hole,
            out int slotStartMs, out int slotEndMs,
            out MoleKind kind,
            out int bigStage, out int bigFirstRole))
            return;

        if (e.seq != seq) return;
        if (e.holeIndex != hole) return;

        int byClientId = sender.OwnerClientIdInHierarchy;
        int byRole = OwnerToRole(byClientId);

        int now = HostNowMsLocal();
        var phase = InferPhase(out bool practiceRunning, out bool practiceEnded);

        // Colors phase
        if (phase == TutorialPhase.Colors)
        {
            bool correct = IsCorrectHit(kind, byRole);

            _gs.AuthorityEmitResolve(seq, hole, slot, 1, byRole, now, byClientId,
                correct ? ResolveKind.HitCorrect : ResolveKind.HitWrong, kind);

            if (!correct)
            {
                // Force a quick respawn so the mole stays hittable (collider does not get stuck off).
                AuthorityResetMoleAfterWrongHit(slot, hole, kind, now);
                return;
            }

            // Hide the correct mole
            _gs.AuthorityScheduleSlot(slot, seq, hole, slotStartMs, now - 1, kind, 0, -1);

            if (kind == MoleKind.TeacherSmall && byRole == 0) _aDone = true;
            if (kind == MoleKind.StudentSmall && byRole == 1) _bDone = true;

            if (_aDone && _bDone)
                SetupStep2(now);

            return;
        }

        // Brown phase
        if (phase == TutorialPhase.Brown)
        {
            _gs.AuthorityEmitResolve(seq, hole, slot, 1, byRole, now, byClientId,
                ResolveKind.HitCorrect, kind);

            _gs.AuthorityScheduleSlot(slot, seq, hole, slotStartMs, now - 1, kind, 0, -1);

            if (slot == 0) _brown0Done = true;
            if (slot == 1) _brown1Done = true;

            if (_brown0Done && _brown1Done)
                SetupStep3(now);

            return;
        }

        // Big phase
        if (phase == TutorialPhase.Big)
        {
            if (kind != MoleKind.SharedBig) return;

            if (bigStage == 0)
            {
                int deadline = now + Mathf.RoundToInt(bigSecondWindowSeconds * 1000f);

                _gs.AuthorityScheduleSlot(slot, seq, hole, slotStartMs, deadline, kind, 1, byRole);

                _gs.AuthorityEmitResolve(seq, hole, slot, 1, byRole, now, byClientId,
                    ResolveKind.BigFirst, kind);

                return;
            }

            if (bigStage == 1)
            {
                if (bigFirstRole == byRole) return;

                _gs.AuthorityEmitResolve(seq, hole, slot, 1, byRole, now, byClientId,
                    ResolveKind.BigComplete, kind);

                _gs.AuthorityScheduleSlot(slot, seq, hole, slotStartMs, now - 1, kind, 0, -1);

                SetupStep4Practice(now);
                return;
            }

            return;
        }

        // Practice phase
        if (phase == TutorialPhase.Practice)
        {
            if (practiceEnded) return;

            // Start timer on first practice hit
            if (!practiceRunning && _gs.GameEndMs == 0)
            {
                int start = now;
                int end = start + Mathf.RoundToInt(practiceDurationSeconds * 1000f);
                _gs.AuthoritySetGameTimes(start, end, 0);
            }

            _gs.AuthorityEmitResolve(seq, hole, slot, 1, byRole, now, byClientId,
                ResolveKind.HitCorrect, kind);

            // Hide current, then schedule next
            _gs.AuthorityScheduleSlot(slot, seq, hole, slotStartMs, now - 1, kind, 0, -1);
            ScheduleNextPracticeMole(now);

            return;
        }
    }

    // --------------------------
    // Resolve FX playback on non-hitter clients
    // --------------------------
    private void OnResolveEvent(WhackGameStateSync.ResolveInfo info)
    {
        if (info.eventId <= 0) return;

        if (_seenResolveIds.Contains(info.eventId)) return;
        _seenResolveIds.Add(info.eventId);
        _seenResolveQueue.Enqueue(info.eventId);

        while (_seenResolveQueue.Count > SeenResolveCapacity)
        {
            int old = _seenResolveQueue.Dequeue();
            _seenResolveIds.Remove(old);
        }

        if (_realtime != null && info.byClientId == _realtime.clientID) return;

        if (_molesByIndex.TryGetValue(info.holeIndex, out MoleVisual m) && m != null)
            m.PlayHitFx(info.kind);
    }

    // --------------------------
    // Mole indexing
    // --------------------------
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
        var keys = new List<int>(_molesByIndex.Keys);
        if (keys.Count == 0) return 0;

        if (avoid != null && avoid.Count > 0)
            keys.RemoveAll(k => avoid.Contains(k));

        if (keys.Count == 0) keys = new List<int>(_molesByIndex.Keys);

        if (_rng == null) _rng = new System.Random(12345);
        return keys[_rng.Next(keys.Count)];
    }

    // --------------------------
    // Hammer visuals (tutorial scene)
    // --------------------------
    private void ApplyHammerMaterialsLocal()
    {
        if (RoleManager.Instance == null) return;
        if (_realtime == null || !_realtime.connected || _realtime.clientID < 0) return;
        if (hammerMatA == null && hammerMatB == null) return;

        var avatars = GameObject.FindGameObjectsWithTag("PlayerAvatar");
        if (avatars == null || avatars.Length == 0) return;

        bool sawNewOwner = false;
        foreach (var av in avatars)
        {
            if (av == null) continue;
            var rv = av.GetComponent<RealtimeView>();
            if (rv == null) continue;

            int owner = rv.ownerIDInHierarchy;
            if (owner < 0) continue;

            if (!_styledHammerOwners.Contains(owner))
            {
                sawNewOwner = true;
                break;
            }
        }

        if (!sawNewOwner && _hammerStyleReady) return;

        foreach (var av in avatars)
        {
            if (av == null) continue;

            var rv = av.GetComponent<RealtimeView>();
            if (rv == null) continue;

            int owner = rv.ownerIDInHierarchy;
            if (owner < 0) continue;

            int role = OwnerToRole(owner); // 0=A, 1=B
            Material targetMat = (role == 0) ? hammerMatA : hammerMatB;

            if (targetMat != null)
                ApplyHammersUnderAvatar(av.transform, targetMat);

            _styledHammerOwners.Add(owner);
        }

        _hammerStyleReady = RoleManager.Instance.IsSolo() ? (_styledHammerOwners.Count >= 1) : (_styledHammerOwners.Count >= 2);
    }

    private void ApplyHammersUnderAvatar(Transform avatarRoot, Material mat)
    {
        if (hammerUseTagSearch)
        {
            var tagged = GameObject.FindGameObjectsWithTag(hammerTag);
            foreach (var go in tagged)
            {
                if (go == null) continue;
                if (!go.transform.IsChildOf(avatarRoot)) continue;
                ApplyMaterialToAllRenderers(go.transform, mat);
            }
        }

        if (hammerAlsoSearchByName)
        {
            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (r.gameObject.name.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0)
                    r.material = mat;
            }
        }
    }

    private void ApplyMaterialToAllRenderers(Transform root, Material mat)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = mat;
    }

    // --------------------------
    // UI button handlers
    // --------------------------
    public void ReplayTutorial()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToLobby()
    {
        SceneManager.LoadScene(lobbySceneName);
    }

    private void Log(string msg)
    {
        if (!verboseLogs) return;
        Debug.Log($"[WhackTutorialController] cid={(_realtime != null ? _realtime.clientID : -1)} {msg}");
    }
}
