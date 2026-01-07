using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class RoleManagerModel {
    [RealtimeProperty(1, true, true)]
    private int _teacherID;

    [RealtimeProperty(2, true, true)]
    private int _studentID;

    [RealtimeProperty(3, true, true)]
    private int _commonSeed;

    [RealtimeProperty(4, true, true)]
    private int _currentHoleIndex;

    [RealtimeProperty(5, true, true)] private bool _gameRootPoseSet;
    [RealtimeProperty(6, true, true)] private float _grPosX;
    [RealtimeProperty(7, true, true)] private float _grPosY;
    [RealtimeProperty(8, true, true)] private float _grPosZ;
    [RealtimeProperty(9, true, true)] private float _grRotX;
    [RealtimeProperty(10, true, true)] private float _grRotY;
    [RealtimeProperty(11, true, true)] private float _grRotZ;
    [RealtimeProperty(12, true, true)] private float _grRotW;
}
