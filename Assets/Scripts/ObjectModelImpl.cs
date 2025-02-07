using Normal.Realtime;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class ObjectModelImpl : RealtimeComponent<ObjectModel>
{
    [SerializeField]
    Renderer[] renderers = new Renderer[0];
    [SerializeField]
    Color originalColor = new Color(0, 0, 102);
    [SerializeField]
    Color holdingColor = new Color(0, 204, 102);
    [SerializeField]
    DataManager dM;

    private void Start()
    {
        if(dM == null) dM = FindAnyObjectByType<DataManager>();
    }

    // When we connect to a room server, we'll be given an instance of our model to work with.
    protected override void OnRealtimeModelReplaced(ObjectModel previousModel, ObjectModel currentModel)
    {
        if (previousModel != null)
        {
            // Unsubscribe from events on the old model.
            previousModel.eventComplete -= DoneUpdateTrackState;
        }
        if (currentModel != null)
        {
            // Subscribe to events on the new model
            currentModel.eventComplete += DoneUpdateTrackState;
        }
    }

    /// <summary>
    /// To update the tracking state of a GameObject for all the clients in the scene.
    /// 0 - idle,
    /// 1 - tracking,
    /// 2 - stopped tracking.
    /// </summary>
    /// <param name="ts"></param>
    public void UpdateTS(int ts)
    {
        model.UpdateTrackingState(ts);
    }

    private void DoneUpdateTrackState(int ts)
    {
        if(this.IsDestroyed())
        { return; }
        switch (ts)
        {
            case 0:
                this.gameObject.SetActive(true);
                //setColor(originalColor);
                break;
            case 1:
                this.gameObject.SetActive(true);
                //setColor(holdingColor);
                dM.AddObjectTrack(this.gameObject);
                break;
             case 2:
                
                dM.RemoveObjectTrack(this.gameObject);
                this.gameObject.SetActive(false);
                //Debug.Log("222222222222222222222");
                break;
            default:
                Debug.Log("tracking state not properly set");
                break;
        }
    }

    private void setColor(Color c)
    {
        foreach(Renderer r in renderers)
        {
            r.material.color = c;
        }
    }
}
