// AnimateOnInputModel.realtime
using Normal.Realtime;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class AnimateOnInputModel
{
    [RealtimeProperty(1, true, true)]
    private float _handAnim;
    
    // Add more properties if needed, following the underscore naming convention
}
