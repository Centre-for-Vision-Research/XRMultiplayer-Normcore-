// using Normal.Realtime;
// using Normal.Realtime.Serialization;

// [RealtimeModel]
// public partial class RoleManagerModel {
//     [RealtimeProperty(1, true, true)]
//     private int _teacherID;

//     [RealtimeProperty(2, true, true)]
//     private int _studentID;
// }

using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class RoleManagerModel {
    [RealtimeProperty(1, true, true)]
    private int _teacherID;

    [RealtimeProperty(2, true, true)]
    private int _studentID;
    
    // Add the common seed property. Initial value should be non-valid (0)
    [RealtimeProperty(3, true, true)]
    private int _commonSeed;
}
