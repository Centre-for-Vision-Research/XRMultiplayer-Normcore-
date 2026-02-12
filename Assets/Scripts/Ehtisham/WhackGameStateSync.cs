using System;
using UnityEngine;
using Normal.Realtime;

public class WhackGameStateSync : RealtimeComponent<WhackGameStateModel>
{
    public static WhackGameStateSync Instance { get; private set; }

    public event Action<int> GameStateChanged;
    public event Action<ResolveInfo> ResolveEvent;

    private RealtimeView _view;

    private int _lastHostNowMs;
    private float _lastHostNowReceivedLocalTime;

    private int _lastResolveEventId = -1;

    [Serializable]
    public struct ResolveInfo
    {
        public int eventId;
        public int seq;
        public int holeIndex;
        public int type;        // 1 hit, 2 miss
        public int byRole;      // 0 A, 1 B, -1
        public int atHostMs;
        public int byClientId;  // echo suppression + dedupe
    }

    private void Awake()
    {
        Instance = this;
        _view = GetComponent<RealtimeView>();
    }

    public bool IsOwnedLocally => _view != null && _view.isOwnedLocallySelf;

    protected override void OnRealtimeModelReplaced(WhackGameStateModel previousModel, WhackGameStateModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.gameStateDidChange -= OnGameStateChanged;
            previousModel.hostNowMsDidChange -= OnHostNowChanged;
            previousModel.resolveEventIdDidChange -= OnResolveEventIdChanged;
        }

        if (currentModel != null)
        {
            currentModel.gameStateDidChange += OnGameStateChanged;
            currentModel.hostNowMsDidChange += OnHostNowChanged;
            currentModel.resolveEventIdDidChange += OnResolveEventIdChanged;

            _lastHostNowMs = currentModel.hostNowMs;
            _lastHostNowReceivedLocalTime = Time.realtimeSinceStartup;

            // IMPORTANT: do not replay old resolve events after model replace
            _lastResolveEventId = currentModel.resolveEventId;

            GameStateChanged?.Invoke(currentModel.gameState);
        }
    }

    private void OnGameStateChanged(WhackGameStateModel m, int newState)
    {
        GameStateChanged?.Invoke(newState);
    }

    private void OnHostNowChanged(WhackGameStateModel m, int newHostNowMs)
    {
        _lastHostNowMs = newHostNowMs;
        _lastHostNowReceivedLocalTime = Time.realtimeSinceStartup;
    }

    private void OnResolveEventIdChanged(WhackGameStateModel m, int value)
    {
        if (m == null) return;
        if (value <= _lastResolveEventId) return;

        _lastResolveEventId = value;

        ResolveEvent?.Invoke(new ResolveInfo
        {
            eventId = m.resolveEventId,
            type = m.resolveType,
            seq = m.resolveSeq,
            holeIndex = m.resolveHoleIndex,
            byRole = m.resolveByRole,
            atHostMs = m.resolveAtHostMs,
            byClientId = m.resolveByClientId
        });

        Debug.Log($"[WhackGameStateSync] cid={realtime.clientID} resolveEventId changed -> {value} type={m.resolveType} hole={m.resolveHoleIndex} byClient={m.resolveByClientId}");

    }

    public int EstimateHostNowMs()
    {
        if (model == null) return Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

        float dt = Time.realtimeSinceStartup - _lastHostNowReceivedLocalTime;
        return _lastHostNowMs + Mathf.RoundToInt(dt * 1000f);
    }

    public int GameState => model != null ? model.gameState : 0;
    public int CurrentSeq => model != null ? model.currentSeq : -1;
    public int CurrentHoleIndex => model != null ? model.currentHoleIndex : -1;
    public int MoleStartMs => model != null ? model.moleStartMs : 0;
    public int MoleEndMs => model != null ? model.moleEndMs : 0;
    public int GameStartMs => model != null ? model.gameStartMs : 0;
    public int GameEndMs => model != null ? model.gameEndMs : 0;
    public int ReturnToLobbyAtMs => model != null ? model.returnToLobbyAtMs : 0;
    public int Seed => model != null ? model.seed : 0;

    public bool IsModelReady() => model != null;

    // --------------------------
    // Authority-only setters
    // --------------------------
    public void AuthoritySetHostNowMs(int v)
    {
        if (!IsOwnedLocally || model == null) return;
        model.hostNowMs = v;

        // IMPORTANT: keep local clock cache correct even if hostNowMsDidChange does not fire locally
        _lastHostNowMs = v;
        _lastHostNowReceivedLocalTime = Time.realtimeSinceStartup;
    }

    public void AuthoritySetSeed(int v) { if (!IsOwnedLocally || model == null) return; model.seed = v; }
    public void AuthoritySetGameState(int v) { if (!IsOwnedLocally || model == null) return; model.gameState = v; }

    public void AuthoritySetGameTimes(int startMs, int endMs, int returnMs)
    {
        if (!IsOwnedLocally || model == null) return;
        model.gameStartMs = startMs;
        model.gameEndMs = endMs;
        model.returnToLobbyAtMs = returnMs;
    }

    public void AuthorityScheduleMole(int seq, int holeIndex, int startMs, int endMs)
    {
        if (!IsOwnedLocally || model == null) return;
        model.currentSeq = seq;
        model.currentHoleIndex = holeIndex;
        model.moleStartMs = startMs;
        model.moleEndMs = endMs;
    }

    public void AuthorityEmitResolve(int seq, int holeIndex, int type, int byRole, int atHostMs, int byClientId)
    {
        if (!IsOwnedLocally || model == null) return;

        // Write payload first
        model.resolveSeq      = seq;
        model.resolveHoleIndex= holeIndex;
        model.resolveType     = type;
        model.resolveByRole   = byRole;
        model.resolveAtHostMs = atHostMs;
        model.resolveByClientId = byClientId;

        // IMPORTANT: increment last so remote reads the updated payload when event fires
        model.resolveEventId = model.resolveEventId + 1;
    }


    // Hard reset for new run / new room
    public void AuthorityResetAll()
    {
        if (!IsOwnedLocally || model == null) return;

        model.gameState = 0;
        model.hostNowMs = 0;

        model.currentSeq = 0;
        model.currentHoleIndex = -1;
        model.moleStartMs = 0;
        model.moleEndMs = 0;

        model.gameStartMs = 0;
        model.gameEndMs = 0;
        model.returnToLobbyAtMs = 0;

        model.resolveSeq = 0;
        model.resolveHoleIndex = 0;
        model.resolveType = 0;
        model.resolveByRole = -1;
        model.resolveAtHostMs = 0;
        model.resolveByClientId = -1;

        _lastResolveEventId = model.resolveEventId;
        _lastHostNowMs = model.hostNowMs;
        _lastHostNowReceivedLocalTime = Time.realtimeSinceStartup;
    }
}
