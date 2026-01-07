using UnityEngine;

public class WhackHole : MonoBehaviour {
    [Header("Assigned Automatically")]
    public MoleController mole;

    private void Awake() {
        mole = GetComponentInChildren<MoleController>();
        if (mole == null) {
            Debug.LogError($"[WhackHole] Hole '{name}' has NO Mole child with MoleController!");
        }
    }
}
