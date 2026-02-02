using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class RoleManagerModel
{
    [RealtimeProperty(1, true, true)]
    private int _teacherID;

    [RealtimeProperty(2, true, true)]
    private int _studentID;

    [RealtimeProperty(3, true, true)]
    private int _commonSeed;

    [RealtimeProperty(4, true, true)]
    private int _currentHoleIndex;
}
