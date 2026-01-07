using com.perceptlab.armultiplayer;
using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable), typeof(RealtimeView))]
public class SharedInteractable : MonoBehaviour
{
    void Awake()
    {
        RealtimeView _rv = GetComponent<RealtimeView>();
        gameObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().selectEntered.AddListener((args) => _rv.RequestOwnershipOfSelfAndChildren());
        gameObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().selectEntered.AddListener((args) => RLogger.Log(gameObject.name+" :selectEntered"));
    }

}
