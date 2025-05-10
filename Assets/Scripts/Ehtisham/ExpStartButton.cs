using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ExpStartButton : MonoBehaviour {
    public string sceneName; // Set this in the Unity Inspector

    void Start() {
        GetComponent<Button>().onClick.AddListener(() => LoadScene());
    }

    private void LoadScene() {
        Debug.Log($"Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }
}
