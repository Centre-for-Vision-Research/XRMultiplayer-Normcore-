using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR;

public class AvatarsOptimization : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        XRSettings.eyeTextureResolutionScale = 0.7f;
    }
}
