using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class MoleModel
{
    // True = mole is up/hittable
    [RealtimeProperty(1, true, true)]
    private bool _isUp;

    // Host state sync so clients follow animations correctly
    [RealtimeProperty(2, true, true)]
    private int _state;

    // 0 Hidden
    // 1 PoppingUp
    // 2 Holding
    // 3 GoingDown
    // 4 InitialUp (special case)
}
