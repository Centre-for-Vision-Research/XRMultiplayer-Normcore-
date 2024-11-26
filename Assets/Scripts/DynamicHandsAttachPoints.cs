using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class DynamicHandAttachPoints : MonoBehaviour
{
    public Transform leftHandAttach;                // Set this in the Inspector
    public Transform rightHandAttach;               // Set this in the Inspector
    private XRDirectInteractor leftDirectInteractor;  // Dynamically found
    private XRDirectInteractor rightDirectInteractor; // Dynamically found

    void Update()
    {
        // Continuously check and assign the left interactor
        if (leftDirectInteractor == null)
        {
            leftDirectInteractor = FindInteractorGameObjectByName("Left Direct Interactor")?.GetComponent<XRDirectInteractor>();
            if (leftDirectInteractor != null && leftHandAttach != null)
            {
                leftDirectInteractor.attachTransform = leftHandAttach;
                Debug.Log("Assigned Left Direct Interactor attachTransform.");
            }
        }

        // Continuously check and assign the right interactor
        if (rightDirectInteractor == null)
        {
            rightDirectInteractor = FindInteractorGameObjectByName("Right Direct Interactor")?.GetComponent<XRDirectInteractor>();
            if (rightDirectInteractor != null && rightHandAttach != null)
            {
                rightDirectInteractor.attachTransform = rightHandAttach;
                Debug.Log("Assigned Right Direct Interactor attachTransform.");
            }
        }
    }

    // Utility method to find GameObject by name
    private GameObject FindInteractorGameObjectByName(string name)
    {
        GameObject interactor = GameObject.Find(name);
        if (interactor == null)
        {
            Debug.LogWarning($"GameObject with name '{name}' not found.");
        }
        return interactor;
    }
}

