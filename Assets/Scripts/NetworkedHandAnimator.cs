using UnityEngine;
using Normal.Realtime;

public class NetworkedHandAnimator : RealtimeComponent<RealtimeHandsAnimationsModel>
{
    private Animator _animator; // Animator reference

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    protected override void OnRealtimeModelReplaced(RealtimeHandsAnimationsModel previousModel, RealtimeHandsAnimationsModel currentModel)
    {
        if (previousModel != null)
        {
            // Unsubscribe from previous model events
            previousModel.leftPinchDidChange -= LeftPinchDidChange;
            previousModel.leftGrabDidChange -= LeftGrabDidChange;
            previousModel.rightPinchDidChange -= RightPinchDidChange;
            previousModel.rightGrabDidChange -= RightGrabDidChange;
        }

        if (currentModel != null)
        {
            // Subscribe to current model events
            currentModel.leftPinchDidChange += LeftPinchDidChange;
            currentModel.leftGrabDidChange += LeftGrabDidChange;
            currentModel.rightPinchDidChange += RightPinchDidChange;
            currentModel.rightGrabDidChange += RightGrabDidChange;

            // Initialize animator with current model values
            _animator.SetFloat("Left Pinch", currentModel.leftPinch);
            _animator.SetFloat("Left Grab", currentModel.leftGrab);
            _animator.SetFloat("Right Pinch", currentModel.rightPinch);
            _animator.SetFloat("Right Grab", currentModel.rightGrab);
        }
    }

    private void LeftPinchDidChange(RealtimeHandsAnimationsModel model, float value)
    {
        _animator.SetFloat("Left Pinch", value);
    }

    private void LeftGrabDidChange(RealtimeHandsAnimationsModel model, float value)
    {
        _animator.SetFloat("Left Grab", value);
    }

    private void RightPinchDidChange(RealtimeHandsAnimationsModel model, float value)
    {
        _animator.SetFloat("Right Pinch", value);
    }

    private void RightGrabDidChange(RealtimeHandsAnimationsModel model, float value)
    {
        _animator.SetFloat("Right Grab", value);
    }

    public void SetHandAnimationValues(float leftPinch, float leftGrab, float rightPinch, float rightGrab)
    {
        if (model != null && realtimeView.isOwnedLocally)
        {
            model.leftPinch = leftPinch;
            model.leftGrab = leftGrab;
            model.rightPinch = rightPinch;
            model.rightGrab = rightGrab;
        }
        else
        {
            Debug.LogWarning("Cannot set hand animation values. RealtimeView is not owned locally.");
        }
    }
}



// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using Normal.Realtime;

// public class NetworkedHandAnimator : RealtimeComponent<RealtimeHandsAnimationsModel>
// {
//     private Animator _animator;

//     private void Awake()
//     {
//         _animator = GetComponent<Animator>();
//     }

//     protected override void OnRealtimeModelReplaced(RealtimeHandsAnimationsModel previousModel, RealtimeHandsAnimationsModel currentModel)
//     {
//         if (previousModel != null)
//         {
//             // Unsubscribe from previous model events
//             previousModel.leftPinchDidChange -= LeftPinchDidChange;
//             previousModel.leftGrabDidChange -= LeftGrabDidChange;
//             previousModel.rightPinchDidChange -= RightPinchDidChange;
//             previousModel.rightGrabDidChange -= RightGrabDidChange;

//             Debug.Log("Unsubscribed from previous model events.");
//         }

//         if (currentModel != null)
//         {
//             // Subscribe to current model events
//             currentModel.leftPinchDidChange += LeftPinchDidChange;
//             currentModel.leftGrabDidChange += LeftGrabDidChange;
//             currentModel.rightPinchDidChange += RightPinchDidChange;
//             currentModel.rightGrabDidChange += RightGrabDidChange;

//             Debug.Log("Subscribed to new model events.");

//             // Immediately apply the current model values to the animator (in case they're already set)
//             _animator.SetFloat("LeftPinch", currentModel.leftPinch);
//             _animator.SetFloat("LeftGrab", currentModel.leftGrab);
//             _animator.SetFloat("RightPinch", currentModel.rightPinch);
//             _animator.SetFloat("RightGrab", currentModel.rightGrab);
//         }
//     }


//     private void LeftPinchDidChange(RealtimeHandsAnimationsModel model, float value)
//     {
//         Debug.Log("LeftPinchDidChange triggered on client with value: " + value);
//         _animator.SetFloat("LeftPinch", value);
//     }


//     private void LeftGrabDidChange(RealtimeHandsAnimationsModel model, float value)
//     {
//         Debug.Log("LeftGrabDidChange triggered: " + value);
//         _animator.SetFloat("LeftGrab", value);
//     }

//     private void RightPinchDidChange(RealtimeHandsAnimationsModel model, float value)
//     {
//         Debug.Log("RightPinchDidChange triggered: " + value);
//         _animator.SetFloat("RightPinch", value);
//     }

//     private void RightGrabDidChange(RealtimeHandsAnimationsModel model, float value)
//     {
//         Debug.Log("RightGrabDidChange triggered: " + value);
//         _animator.SetFloat("RightGrab", value);
//     }


//     public void SetHandAnimationValues(float leftPinch, float leftGrab, float rightPinch, float rightGrab)
//     {
//         // Ensure this client owns the model before setting values
//         if (model != null && realtimeView.isOwnedLocally)
//         {
//             //Debug.Log("Setting hand animation values as the owner.");
//             model.leftPinch = leftPinch;
//             model.leftGrab = leftGrab;
//             model.rightPinch = rightPinch;
//             model.rightGrab = rightGrab;
//         }
//     }

// }
