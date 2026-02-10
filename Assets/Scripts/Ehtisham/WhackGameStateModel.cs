using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class WhackGameStateModel
{
    // 0 WaitingForPlayers, 1 WaitingForStartHit, 2 Running, 3 Ended
    [RealtimeProperty(1, true, true)]
    private int _gameState;

    // Host time in ms, broadcast by authority at fixed rate
    [RealtimeProperty(2, true, true)]
    private int _hostNowMs;

    // Current mole "sequence" (increments every mole event)
    [RealtimeProperty(3, true, true)]
    private int _currentSeq;

    // Which hole index is scheduled/active for currentSeq
    [RealtimeProperty(4, true, true)]
    private int _currentHoleIndex;

    // Scheduled window for current mole in host time (ms)
    [RealtimeProperty(5, true, true)]
    private int _moleStartMs;

    [RealtimeProperty(6, true, true)]
    private int _moleEndMs;

    // Block timing
    [RealtimeProperty(7, true, true)]
    private int _gameStartMs;

    [RealtimeProperty(8, true, true)]
    private int _gameEndMs;

    // When clients should load lobby
    [RealtimeProperty(9, true, true)]
    private int _returnToLobbyAtMs;

    // Seed used by authority (debug + late-join consistency)
    [RealtimeProperty(10, true, true)]
    private int _seed;

    // Resolve event channel (for hit/miss effects)
    [RealtimeProperty(11, true, true)]
    private int _resolveEventId;

    [RealtimeProperty(12, true, true)]
    private int _resolveSeq;

    [RealtimeProperty(13, true, true)]
    private int _resolveHoleIndex;

    // 1 Hit, 2 Miss
    [RealtimeProperty(14, true, true)]
    private int _resolveType;

    // 0 A, 1 B, -1 none
    [RealtimeProperty(15, true, true)]
    private int _resolveByRole;

    [RealtimeProperty(16, true, true)]
    private int _resolveAtHostMs;
}
