using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime.Serialization;

[RealtimeModel]
public partial class RealtimeHandsAnimationsModel
{
    [RealtimeProperty(1, true, true)] // ID, reliable, notifyChange
    private float _leftPinch;
    
    [RealtimeProperty(2, true, true)]
    private float _leftGrab;
    
    [RealtimeProperty(3, true, true)]
    private float _rightPinch;
    
    [RealtimeProperty(4, true, true)]
    private float _rightGrab;
}