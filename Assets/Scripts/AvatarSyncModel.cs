using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AvatarSyncModel: Models the Realtime information being sent across using Normcore
/// </summary>
[RealtimeModel]
public partial class AvatarSyncModel
{
    [RealtimeProperty(1, false, true)]
    private string _avatarData;
}
