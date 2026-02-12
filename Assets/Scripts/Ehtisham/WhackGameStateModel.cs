using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class WhackGameStateModel
{
    // 0 WaitingForPlayers, 1 WaitingForStartHit, 2 Running, 3 Ended
    [RealtimeProperty(1, true, true)]
    private int _gameState;

    // Host time in ms, broadcast by authority
    [RealtimeProperty(2, true, true)]
    private int _hostNowMs;

    // -------------------------
    // SLOT 0 (backward compatible fields)
    // -------------------------
    [RealtimeProperty(3, true, true)]
    private int _currentSeq;           // Slot0 seq

    [RealtimeProperty(4, true, true)]
    private int _currentHoleIndex;     // Slot0 hole

    [RealtimeProperty(5, true, true)]
    private int _moleStartMs;          // Slot0 start

    [RealtimeProperty(6, true, true)]
    private int _moleEndMs;            // Slot0 end

    // Block timing
    [RealtimeProperty(7, true, true)]
    private int _gameStartMs;

    [RealtimeProperty(8, true, true)]
    private int _gameEndMs;

    // When clients should load lobby
    [RealtimeProperty(9, true, true)]
    private int _returnToLobbyAtMs;

    // Seed used by authority
    [RealtimeProperty(10, true, true)]
    private int _seed;

    // -------------------------
    // Resolve event channel
    // -------------------------
    [RealtimeProperty(11, true, true)]
    private int _resolveEventId;

    [RealtimeProperty(12, true, true)]
    private int _resolveSeq;

    [RealtimeProperty(13, true, true)]
    private int _resolveHoleIndex;

    // 1 Hit, 2 Miss (kept)
    [RealtimeProperty(14, true, true)]
    private int _resolveType;

    // 0 teacher(A), 1 student(B), -1 none
    [RealtimeProperty(15, true, true)]
    private int _resolveByRole;

    [RealtimeProperty(16, true, true)]
    private int _resolveAtHostMs;

    [RealtimeProperty(17, true, true)]
    private int _resolveByClientId;

    // NEW: richer resolve meaning
    [RealtimeProperty(18, true, true)]
    private int _resolveKind;          // ResolveKind

    [RealtimeProperty(19, true, true)]
    private int _resolveSlotIndex;     // 0..2

    [RealtimeProperty(20, true, true)]
    private int _resolveMoleKind;      // MoleKind

    // -------------------------
    // Multi-slot support
    // -------------------------
    [RealtimeProperty(21, true, true)]
    private int _activeSlotCount;      // 1..3

    // Slot0 extra
    [RealtimeProperty(22, true, true)]
    private int _slot0MoleKind;        // MoleKind
    [RealtimeProperty(23, true, true)]
    private int _slot0BigStage;        // 0 normal, 1 waiting second
    [RealtimeProperty(24, true, true)]
    private int _slot0BigFirstRole;    // 0 teacher, 1 student, -1 none

    // Slot1
    [RealtimeProperty(25, true, true)]
    private int _slot1Seq;
    [RealtimeProperty(26, true, true)]
    private int _slot1HoleIndex;
    [RealtimeProperty(27, true, true)]
    private int _slot1StartMs;
    [RealtimeProperty(28, true, true)]
    private int _slot1EndMs;
    [RealtimeProperty(29, true, true)]
    private int _slot1MoleKind;
    [RealtimeProperty(30, true, true)]
    private int _slot1BigStage;
    [RealtimeProperty(31, true, true)]
    private int _slot1BigFirstRole;

    // Slot2
    [RealtimeProperty(32, true, true)]
    private int _slot2Seq;
    [RealtimeProperty(33, true, true)]
    private int _slot2HoleIndex;
    [RealtimeProperty(34, true, true)]
    private int _slot2StartMs;
    [RealtimeProperty(35, true, true)]
    private int _slot2EndMs;
    [RealtimeProperty(36, true, true)]
    private int _slot2MoleKind;
    [RealtimeProperty(37, true, true)]
    private int _slot2BigStage;
    [RealtimeProperty(38, true, true)]
    private int _slot2BigFirstRole;
}
