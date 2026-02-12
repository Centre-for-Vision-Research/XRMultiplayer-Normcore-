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
        public int type;            // 1 hit, 2 miss
        public int byRole;          // 0 teacher, 1 student, -1
        public int atHostMs;
        public int byClientId;
        public ResolveKind kind;    // correct, wrong, big first, big complete
        public int slotIndex;       // 0..2
        public MoleKind moleKind;   // teacher small, student small, shared small, shared big
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
            byClientId = m.resolveByClientId,
            kind = (ResolveKind)m.resolveKind,
            slotIndex = m.resolveSlotIndex,
            moleKind = (MoleKind)m.resolveMoleKind
        });
    }

    public int EstimateHostNowMs()
    {
        if (model == null) return Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);
        float dt = Time.realtimeSinceStartup - _lastHostNowReceivedLocalTime;
        return _lastHostNowMs + Mathf.RoundToInt(dt * 1000f);
    }

    public int GameState => model != null ? model.gameState : 0;
    public int GameStartMs => model != null ? model.gameStartMs : 0;
    public int GameEndMs => model != null ? model.gameEndMs : 0;
    public int ReturnToLobbyAtMs => model != null ? model.returnToLobbyAtMs : 0;
    public int Seed => model != null ? model.seed : 0;

    public int ActiveSlotCount => model != null ? Mathf.Clamp(model.activeSlotCount, 1, 3) : 1;

    public bool IsModelReady() => model != null;

    // ---------- Slot getters ----------
    public bool TryGetSlot(int slot, out int seq, out int hole, out int startMs, out int endMs, out MoleKind kind, out int bigStage, out int bigFirstRole)
    {
        seq = -1; hole = -1; startMs = 0; endMs = 0; kind = MoleKind.SharedSmall; bigStage = 0; bigFirstRole = -1;
        if (model == null) return false;
        if (slot < 0 || slot > 2) return false;

        if (slot == 0)
        {
            seq = model.currentSeq;
            hole = model.currentHoleIndex;
            startMs = model.moleStartMs;
            endMs = model.moleEndMs;
            kind = (MoleKind)model.slot0MoleKind;
            bigStage = model.slot0BigStage;
            bigFirstRole = model.slot0BigFirstRole;
            return true;
        }

        if (slot == 1)
        {
            seq = model.slot1Seq;
            hole = model.slot1HoleIndex;
            startMs = model.slot1StartMs;
            endMs = model.slot1EndMs;
            kind = (MoleKind)model.slot1MoleKind;
            bigStage = model.slot1BigStage;
            bigFirstRole = model.slot1BigFirstRole;
            return true;
        }

        seq = model.slot2Seq;
        hole = model.slot2HoleIndex;
        startMs = model.slot2StartMs;
        endMs = model.slot2EndMs;
        kind = (MoleKind)model.slot2MoleKind;
        bigStage = model.slot2BigStage;
        bigFirstRole = model.slot2BigFirstRole;
        return true;
    }

    public bool TryFindActiveSlotForHole(int holeIndex, int hostNowMs, int graceMs, out int slotIndex, out int seq, out MoleKind kind, out int bigStage, out int bigFirstRole)
    {
        slotIndex = -1; seq = -1; kind = MoleKind.SharedSmall; bigStage = 0; bigFirstRole = -1;
        if (model == null) return false;

        int slots = ActiveSlotCount;
        for (int s = 0; s < slots; s++)
        {
            if (!TryGetSlot(s, out int sSeq, out int sHole, out int sStart, out int sEnd, out MoleKind sKind, out int sBigStage, out int sFirstRole))
                continue;

            if (sHole != holeIndex) continue;

            bool inWindow = hostNowMs >= (sStart - graceMs) && hostNowMs <= (sEnd + graceMs);
            if (!inWindow) continue;

            slotIndex = s;
            seq = sSeq;
            kind = sKind;
            bigStage = sBigStage;
            bigFirstRole = sFirstRole;
            return true;
        }

        return false;
    }

    // ---------- Authority setters ----------
    public void AuthoritySetHostNowMs(int v)
    {
        if (!IsOwnedLocally || model == null) return;
        model.hostNowMs = v;
        _lastHostNowMs = v;
        _lastHostNowReceivedLocalTime = Time.realtimeSinceStartup;
    }

    public void AuthoritySetSeed(int v) { if (!IsOwnedLocally || model == null) return; model.seed = v; }
    public void AuthoritySetGameState(int v) { if (!IsOwnedLocally || model == null) return; model.gameState = v; }
    public void AuthoritySetActiveSlotCount(int v) { if (!IsOwnedLocally || model == null) return; model.activeSlotCount = Mathf.Clamp(v, 1, 3); }

    public void AuthoritySetGameTimes(int startMs, int endMs, int returnMs)
    {
        if (!IsOwnedLocally || model == null) return;
        model.gameStartMs = startMs;
        model.gameEndMs = endMs;
        model.returnToLobbyAtMs = returnMs;
    }

    public void AuthorityScheduleSlot(int slot, int seq, int holeIndex, int startMs, int endMs, MoleKind kind, int bigStage, int bigFirstRole)
    {
        if (!IsOwnedLocally || model == null) return;

        if (slot == 0)
        {
            model.currentSeq = seq;
            model.currentHoleIndex = holeIndex;
            model.moleStartMs = startMs;
            model.moleEndMs = endMs;
            model.slot0MoleKind = (int)kind;
            model.slot0BigStage = bigStage;
            model.slot0BigFirstRole = bigFirstRole;
            return;
        }

        if (slot == 1)
        {
            model.slot1Seq = seq;
            model.slot1HoleIndex = holeIndex;
            model.slot1StartMs = startMs;
            model.slot1EndMs = endMs;
            model.slot1MoleKind = (int)kind;
            model.slot1BigStage = bigStage;
            model.slot1BigFirstRole = bigFirstRole;
            return;
        }

        model.slot2Seq = seq;
        model.slot2HoleIndex = holeIndex;
        model.slot2StartMs = startMs;
        model.slot2EndMs = endMs;
        model.slot2MoleKind = (int)kind;
        model.slot2BigStage = bigStage;
        model.slot2BigFirstRole = bigFirstRole;
    }

    public void AuthorityEmitResolve(int seq, int holeIndex, int slotIndex, int type, int byRole, int atHostMs, int byClientId, ResolveKind kind, MoleKind moleKind)
    {
        if (!IsOwnedLocally || model == null) return;

        model.resolveSeq = seq;
        model.resolveHoleIndex = holeIndex;
        model.resolveSlotIndex = slotIndex;
        model.resolveType = type;
        model.resolveByRole = byRole;
        model.resolveAtHostMs = atHostMs;
        model.resolveByClientId = byClientId;
        model.resolveKind = (int)kind;
        model.resolveMoleKind = (int)moleKind;

        model.resolveEventId = model.resolveEventId + 1;
    }

    public void AuthorityResetAll()
    {
        if (!IsOwnedLocally || model == null) return;

        model.gameState = 0;
        model.hostNowMs = 0;

        model.currentSeq = 0;
        model.currentHoleIndex = -1;
        model.moleStartMs = 0;
        model.moleEndMs = 0;

        model.slot0MoleKind = (int)MoleKind.SharedSmall;
        model.slot0BigStage = 0;
        model.slot0BigFirstRole = -1;

        model.activeSlotCount = 1;

        model.slot1Seq = 0; model.slot1HoleIndex = -1; model.slot1StartMs = 0; model.slot1EndMs = 0;
        model.slot1MoleKind = (int)MoleKind.SharedSmall; model.slot1BigStage = 0; model.slot1BigFirstRole = -1;

        model.slot2Seq = 0; model.slot2HoleIndex = -1; model.slot2StartMs = 0; model.slot2EndMs = 0;
        model.slot2MoleKind = (int)MoleKind.SharedSmall; model.slot2BigStage = 0; model.slot2BigFirstRole = -1;

        model.gameStartMs = 0;
        model.gameEndMs = 0;
        model.returnToLobbyAtMs = 0;

        model.resolveSeq = 0;
        model.resolveHoleIndex = 0;
        model.resolveType = 0;
        model.resolveByRole = -1;
        model.resolveAtHostMs = 0;
        model.resolveByClientId = -1;
        model.resolveKind = 0;
        model.resolveSlotIndex = 0;
        model.resolveMoleKind = 0;

        _lastResolveEventId = model.resolveEventId;
        _lastHostNowMs = model.hostNowMs;
        _lastHostNowReceivedLocalTime = Time.realtimeSinceStartup;
    }
}
