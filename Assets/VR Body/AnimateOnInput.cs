using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Normal.Realtime;

[System.Serializable]
public class AnimationInput
{
    public string animationPropertyName; // Parameter name in Animator
    public InputActionProperty action;   // Input action for this parameter
}

public class AnimateOnInput : MonoBehaviour
{
    public List<AnimationInput> animationInputs; // List of input-to-parameter mappings
    public Animator animator;                   // Animator reference

    private NetworkedHandAnimator _networkedHandAnimator; // Synchronization component
    private RealtimeView _realtimeView;                  // Realtime view for ownership check

    private void Start()
    {
        _networkedHandAnimator = GetComponent<NetworkedHandAnimator>();
        _realtimeView = GetComponent<RealtimeView>();

        if (_realtimeView == null)
        {
            Debug.LogError("AnimateOnInput requires a RealtimeView component on the same GameObject.");
        }
    }

    void Update()
    {
        if (_realtimeView != null && !_realtimeView.isOwnedLocally)
        {
            return; // Skip if not owned locally
        }

        float leftPinch = 0f, leftGrab = 0f, rightPinch = 0f, rightGrab = 0f;

        foreach (var item in animationInputs)
        {
            float actionValue = item.action.action.ReadValue<float>();

            // Assign values based on the parameter names
            switch (item.animationPropertyName)
            {
                case "Left Pinch":
                    leftPinch = actionValue;
                    break;
                case "Left Grab":
                    leftGrab = actionValue;
                    break;
                case "Right Pinch":
                    rightPinch = actionValue;
                    break;
                case "Right Grab":
                    rightGrab = actionValue;
                    break;
                default:
                    Debug.LogWarning($"Unknown animation property name: {item.animationPropertyName}");
                    break;
            }

            // Directly update the local animator
            animator.SetFloat(item.animationPropertyName, actionValue);
        }

        // Send these values to the NetworkedHandAnimator for syncing across the network
        _networkedHandAnimator.SetHandAnimationValues(leftPinch, leftGrab, rightPinch, rightGrab);
    }
}




// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;
// using Normal.Realtime;

// [System.Serializable]
// public class AnimationInput
// {
//     public string animationPropertyName;
//     public InputActionProperty action;
// }

// public class AnimateOnInput : MonoBehaviour
// {
//     public List<AnimationInput> animationInputs;
//     public Animator animator;

//     private NetworkedHandAnimator _networkedHandAnimator;
//     private RealtimeView _realtimeView;

//     private void Start()
//     {
//         _networkedHandAnimator = GetComponent<NetworkedHandAnimator>();
//         _realtimeView = GetComponent<RealtimeView>();

//         if (_realtimeView == null)
//         {
//             Debug.LogError("AnimateOnInput requires a RealtimeView component on the same GameObject.");
//         }
//     }

//     void Update()
//     {
//         if (_realtimeView != null && !_realtimeView.isOwnedLocally)
//         {
//             return;
//         }
//         float leftPinch = 0f, leftGrab = 0f, rightPinch = 0f, rightGrab = 0f;

//         foreach (var item in animationInputs)
//         {
//             float actionValue = item.action.action.ReadValue<float>();
//             //animator.SetFloat(item.animationPropertyName, actionValue);

//             //Assign the value to the appropriate variable based on the parameter name
//             if (item.animationPropertyName == "LeftPinch")
//                 leftPinch = actionValue;
//             else if (item.animationPropertyName == "LeftGrab")
//                 leftGrab = actionValue;
//             else if (item.animationPropertyName == "RightPinch")
//                 rightPinch = actionValue;
//             else if (item.animationPropertyName == "RightGrab")
//                 rightGrab = actionValue;
//         }

//         // Send these values to the NetworkedHandAnimator for syncing
//         _networkedHandAnimator.SetHandAnimationValues(leftPinch, leftGrab, rightPinch, rightGrab);
//     }
// }



// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;

// [System.Serializable]
// public class AnimationInput
// {
//     public string animationPropertyName;
//     public InputActionProperty action;
// }

// public class AnimateOnInput : MonoBehaviour
// {
//     public List<AnimationInput> animationInputs;
//     public Animator animator;

//     // Update is called once per frame
//     void Update()
//     {
//         foreach (var item in animationInputs)
//         {
//             float actionValue = item.action.action.ReadValue<float>();
//             animator.SetFloat(item.animationPropertyName, actionValue);
//         }
//     }
// }
