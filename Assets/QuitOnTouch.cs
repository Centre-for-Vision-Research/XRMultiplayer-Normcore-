using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class QuitOnTouch : MonoBehaviour
{
    public void QuitApp() {
        SceneManager.LoadScene("LobbyAvtrs");
    }
}
