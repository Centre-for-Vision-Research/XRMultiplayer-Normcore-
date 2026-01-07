using Normal.Realtime;
using UnityEngine;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class RealtimeAvatarAppearanceModel
{
    [RealtimeProperty(1, true, true)]
    private string _skinTone;

    [RealtimeProperty(2, true, true)]
    private string _physique;
}
