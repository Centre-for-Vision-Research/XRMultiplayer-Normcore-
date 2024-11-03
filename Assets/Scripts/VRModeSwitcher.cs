using UnityEngine;
using UnityEngine.XR;

public class VRModeSwitcher : MonoBehaviour {
    public GameObject vrCamera;      // Assign your VR camera GameObject
    public GameObject nonVrCamera;    // Assign your non-VR camera

    void Start() {
        if (XRSettings.isDeviceActive) {
            // VR is active
            vrCamera.SetActive(true);
            nonVrCamera.SetActive(false);
        } else {
            // VR is not active
            vrCamera.SetActive(false);
            nonVrCamera.SetActive(true);
        }
    }
}
