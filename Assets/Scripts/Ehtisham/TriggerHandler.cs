//using UnityEngine;

//public class TriggerHandler : MonoBehaviour {
//    private SortingTaskManager sortingTaskManager;

//    void Start() {
//        sortingTaskManager = FindObjectOfType<SortingTaskManager>(); // Find the manager
//    }

//    void OnTriggerEnter(Collider other) {
//        // Find the parent GameObject that has the "Shape" tag
//        Transform shapeParent = other.transform;
//        while (shapeParent.parent != null) {
//            if (shapeParent.parent.CompareTag("Shape")) {
//                shapeParent = shapeParent.parent; // Found the correct parent
//                break;
//            }
//            shapeParent = shapeParent.parent;
//        }

//        // If we found the correct shape parent, pass it to SortingTaskManager
//        if (shapeParent.CompareTag("Shape")) {
//            sortingTaskManager.OnShapeTriggerEnter(shapeParent.gameObject, gameObject);
//        }
//    }
//}
