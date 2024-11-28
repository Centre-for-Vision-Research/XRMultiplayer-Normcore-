// using UnityEngine;
// using UnityEngine.XR.Interaction.Toolkit;

// public class DynamicHandAttachPoints : MonoBehaviour
// {
//     public Transform leftHandAttach;                // Set this in the Inspector
//     public Transform rightHandAttach;               // Set this in the Inspector
//     private XRDirectInteractor leftDirectInteractor;  // Dynamically found
//     private XRDirectInteractor rightDirectInteractor; // Dynamically found

//     void Update()
//     {
//         // Continuously check and assign the left interactor
//         if (leftDirectInteractor == null)
//         {
//             leftDirectInteractor = FindInteractorGameObjectByName("Left Direct Interactor")?.GetComponent<XRDirectInteractor>();
//             if (leftDirectInteractor != null && leftHandAttach != null)
//             {
//                 leftDirectInteractor.attachTransform = leftHandAttach;
//             }
//         }

//         // Continuously check and assign the right interactor
//         if (rightDirectInteractor == null)
//         {
//             rightDirectInteractor = FindInteractorGameObjectByName("Right Direct Interactor")?.GetComponent<XRDirectInteractor>();
//             if (rightDirectInteractor != null && rightHandAttach != null)
//             {
//                 rightDirectInteractor.attachTransform = rightHandAttach;
//             }
//         }
//     }

//     // Utility method to find GameObject by name
//     private GameObject FindInteractorGameObjectByName(string name)
//     {
//         GameObject interactor = GameObject.Find(name);
//         if (interactor == null)
//         {
//             Debug.LogWarning($"GameObject with name '{name}' not found.");
//         }
//         return interactor;
//     }
// }


using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Normal.Realtime;

public class DynamicHandAttachPoints : MonoBehaviour
{
    public Transform leftHandAttach;  // Set this in the Inspector
    public Transform rightHandAttach; // Set this in the Inspector

    private XRDirectInteractor leftDirectInteractor;  // Dynamically found
    private XRDirectInteractor rightDirectInteractor; // Dynamically found

    private RealtimeView realtimeView;

    void Start()
    {
        // Get the RealtimeView component to check ownership
        realtimeView = GetComponent<RealtimeView>();
    }

    void Update()
    {
        // Only process for the local player's avatar
        if (realtimeView == null || !realtimeView.isOwnedLocally)
            return;

        // Continuously check and assign the left interactor
        if (leftDirectInteractor == null)
        {
            leftDirectInteractor = FindInteractorGameObjectByName("Left Direct Interactor")?.GetComponent<XRDirectInteractor>();
            if (leftDirectInteractor != null && leftHandAttach != null)
            {
                leftDirectInteractor.attachTransform = leftHandAttach;
            }
        }

        // Continuously check and assign the right interactor
        if (rightDirectInteractor == null)
        {
            rightDirectInteractor = FindInteractorGameObjectByName("Right Direct Interactor")?.GetComponent<XRDirectInteractor>();
            if (rightDirectInteractor != null && rightHandAttach != null)
            {
                rightDirectInteractor.attachTransform = rightHandAttach;
            }
        }
    }

    // Utility method to find GameObject by name
    private GameObject FindInteractorGameObjectByName(string name)
    {
        GameObject interactor = GameObject.Find(name);
        // if (interactor == null)
        // {
        //     Debug.LogWarning($"GameObject with name '{name}' not found.");
        // }
        return interactor;
    }
}
