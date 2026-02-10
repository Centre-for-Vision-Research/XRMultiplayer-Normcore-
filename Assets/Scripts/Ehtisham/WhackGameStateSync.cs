using System;
using UnityEngine;
using Normal.Realtime;

public class WhackGameStateSync : RealtimeComponent<WhackGameStateModel>
{
    public static WhackGameStateSync Instance { get; private set; }

    public event Action<int> GameStateChanged;
    public event Action<ResolveInfo> ResolveEvent;

    private int _lastHostNowMs;
    private float _lastHostNowReceivedLocalTime;

    [Serializable]
    public struct ResolveInfo
    {
        public int eventId;
        public int seq;
        public int holeIndex;
        public int type;      // 1 hit, 2 miss
        public int byRole;    // 0 A, 1 B, -1
        public int atHostMs;
    }

    private void Awake()
    {
        Instance = this;
    }

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

    private void OnResolveEventIdChanged(WhackGameStateModel m, int newEventId)
    {
        if (m == null) return;

        ResolveInfo info = new ResolveInfo
        {
            eventId = newEventId,
            seq = m.resolveSeq,
            holeIndex = m.resolveHoleIndex,
            type = m.resolveType,
            byRole = m.resolveByRole,
            atHostMs = m.resolveAtHostMs
        };

        ResolveEvent?.Invoke(info);
    }

    // Clients estimate host time using last received hostNowMs + local elapsed
    public int EstimateHostNowMs()
    {
        if (model == null) return Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

        float dt = Time.realtimeSinceStartup - _lastHostNowReceivedLocalTime;
        return model.hostNowMs + Mathf.RoundToInt(dt * 1000f);
    }

    // Convenience getters
    public int GameState => model != null ? model.gameState : 0;
    public int CurrentSeq => model != null ? model.currentSeq : -1;
    public int CurrentHoleIndex => model != null ? model.currentHoleIndex : -1;
    public int MoleStartMs => model != null ? model.moleStartMs : 0;
    public int MoleEndMs => model != null ? model.moleEndMs : 0;

    public int GameStartMs => model != null ? model.gameStartMs : 0;
    public int GameEndMs => model != null ? model.gameEndMs : 0;
    public int ReturnToLobbyAtMs => model != null ? model.returnToLobbyAtMs : 0;
    public int Seed => model != null ? model.seed : 0;
    


    // Authority-only setters (call from WhackGameController only)
    public void AuthoritySetHostNowMs(int v) { if (model != null) model.hostNowMs = v; }
    public void AuthoritySetSeed(int v) { if (model != null) model.seed = v; }
    public void AuthoritySetGameState(int v) { if (model != null) model.gameState = v; }
    public void AuthoritySetGameTimes(int startMs, int endMs, int returnMs)
    {
        if (model == null) return;
        model.gameStartMs = startMs;
        model.gameEndMs = endMs;
        model.returnToLobbyAtMs = returnMs;
    }
    public void AuthorityScheduleMole(int seq, int holeIndex, int startMs, int endMs)
    {
        if (model == null) return;
        model.currentSeq = seq;
        model.currentHoleIndex = holeIndex;
        model.moleStartMs = startMs;
        model.moleEndMs = endMs;
    }
    public void AuthorityEmitResolve(int eventId, int seq, int holeIndex, int type, int byRole, int atHostMs)
    {
        if (model == null) return;
        model.resolveEventId = eventId;
        model.resolveSeq = seq;
        model.resolveHoleIndex = holeIndex;
        model.resolveType = type;
        model.resolveByRole = byRole;
        model.resolveAtHostMs = atHostMs;
    }
}
