using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class WhackPlayerInputModel
{
    [RealtimeProperty(1, true, true)]
    private int _hitEventId;

    [RealtimeProperty(2, true, true)]
    private int _hitHoleIndex;

    [RealtimeProperty(3, true, true)]
    private int _hitSeq;

    [RealtimeProperty(4, true, true)]
    private int _hitSentAtHostMs;
}
