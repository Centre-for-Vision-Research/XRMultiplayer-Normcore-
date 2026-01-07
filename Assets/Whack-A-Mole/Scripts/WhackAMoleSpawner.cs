using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WhackAMoleSpawner : MonoBehaviour {

    public int maxMolesUp = 1;

    public float spawnMin = 0.25f;
    public float spawnMax = 0.45f;

    private List<WhackHole> holes = new List<WhackHole>();
    private bool generating = false;
    private WhackAMoleTaskManager manager;

    void Awake() {
        manager = FindObjectOfType<WhackAMoleTaskManager>();
        holes.AddRange(FindObjectsOfType<WhackHole>());
    }

    public void StartGenerating() {
        if (!manager.IsHost()) return;

        generating = true;

        int seed = RoleManager.Instance.GetCommonSeed();
        Random.InitState(seed);

        StartCoroutine(GenerateRoutine());
    }

    public void StopGenerating() {
        generating = false;
    }

    private IEnumerator GenerateRoutine() {

        while (generating) {

            float wait = Random.Range(spawnMin, spawnMax);
            yield return new WaitForSeconds(wait);

            int up = 0;
            foreach (var h in holes)
                if (h.mole != null && h.mole.IsUp)
                    up++;

            if (up >= maxMolesUp)
                continue;

            int idx = Random.Range(0, holes.Count);
            WhackHole hole = holes[idx];

            if (!hole.mole.IsUp) {
                hole.mole.Pop();
                WhackAMoleTaskManager.Instance.RegisterSpawn(hole);
            }
        }
    }
}
