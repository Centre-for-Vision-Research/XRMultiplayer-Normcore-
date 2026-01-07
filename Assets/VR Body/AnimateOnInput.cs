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
        if (_realtimeView != null && !_realtimeView.isOwnedLocallySelf)
        {
            return; // Skip if not owned locally
        }

        // remove this if need animation on input

        float leftPinch = 1f;  
        float leftGrab  = 1f; 
        float rightPinch = 0f;  
        float rightGrab  = 0f;

        // COMMENTING OUT THE WHOLE INPUT LOOP
        /*
        foreach (var item in animationInputs)
        {
            float actionValue = item.action.action.ReadValue<float>();

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

            animator.SetFloat(item.animationPropertyName, actionValue);
        }
        */

        // FORCE ALL ANIMATOR PARAMETERS TO 1
        foreach (var item in animationInputs)
        {
            animator.SetFloat(item.animationPropertyName, 1f);
        }

        // SEND VALUES ACROSS NETWORK
        _networkedHandAnimator.SetHandAnimationValues(leftPinch, leftGrab, rightPinch, rightGrab);
    }
}



