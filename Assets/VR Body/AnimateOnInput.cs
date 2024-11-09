// AnimateOnInput.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Normal.Realtime; // Ensure you have the correct namespace

public class AnimateOnInput : RealtimeComponent<AnimateOnInputModel>
{
    [System.Serializable]
    public class AnimationInput
    {
        public string animationPropertyName;
        public InputActionProperty action;
    }

    public List<AnimationInput> animationInputs;
    public Animator animator;

    // Local cache to track changes and minimize network traffic
    private Dictionary<string, float> lastValues = new Dictionary<string, float>();

    protected override void OnRealtimeModelReplaced(AnimateOnInputModel previousModel, AnimateOnInputModel currentModel)
    {
        if (previousModel != null)
        {
            previousModel.handAnimDidChange -= HandleHandAnimChanged;
        }

        if (currentModel != null)
        {
            currentModel.handAnimDidChange += HandleHandAnimChanged;

            // Initialize the animator with the current value
            animator.SetFloat("HandAnim", currentModel.handAnim);
        }
    }

    void Start()
    {
        // Initialize lastValues
        foreach (var item in animationInputs)
        {
            lastValues[item.animationPropertyName] = 0f;
        }
    }

    void Update()
    {
        // Only update if this client owns the object
        if (realtimeView.isOwnedLocallyInHierarchy)
        {
            foreach (var item in animationInputs)
            {
                float actionValue = item.action.action.ReadValue<float>();
                animator.SetFloat(item.animationPropertyName, actionValue);

                // Check if the value has changed significantly to update the network
                if (Mathf.Abs(actionValue - lastValues[item.animationPropertyName]) > 0.01f)
                {
                    lastValues[item.animationPropertyName] = actionValue;

                    // Update the model to synchronize with other clients
                    switch (item.animationPropertyName)
                    {
                        case "HandAnim":
                            model.handAnim = actionValue;
                            break;
                        // Add cases for other properties if needed
                    }
                }
            }
        }
    }

    private void HandleHandAnimChanged(AnimateOnInputModel model, float value)
    {
        // Avoid updating the local animator if this client owns the object
        if (!realtimeView.isOwnedLocallyInHierarchy)
        {
            animator.SetFloat("HandAnim", value);
        }
    }
}


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
