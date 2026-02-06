using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class MRSharedAnchorModel {
    // Host generates a fresh group UUID per session.
    [RealtimeProperty(1, true, true)]
    private string _groupUuid;

    // Optional: the anchor UUID (for debugging).
    [RealtimeProperty(2, true, true)]
    private string _anchorUuid;

    // 0 = none, 1 = group created, 2 = anchor shared
    [RealtimeProperty(3, true, true)]
    private int _stage;
}
