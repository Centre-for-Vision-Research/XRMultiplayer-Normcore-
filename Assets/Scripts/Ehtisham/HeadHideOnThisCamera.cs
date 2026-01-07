using UnityEngine;

[RequireComponent(typeof(Camera))]
public class HeadHideOnThisCamera : MonoBehaviour
{
    void OnPreRender() {
        Shader.SetGlobalFloat("_HIDE_HEAD", 1f);
    }
    void OnPostRender() {
        Shader.SetGlobalFloat("_HIDE_HEAD", 0f);
    }
}
