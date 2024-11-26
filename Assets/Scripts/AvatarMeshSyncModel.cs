using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;

[RealtimeModel]
public partial class MeshSyncModel {
    [RealtimeProperty(1, true, true)]
    private int _meshIndex;
}
