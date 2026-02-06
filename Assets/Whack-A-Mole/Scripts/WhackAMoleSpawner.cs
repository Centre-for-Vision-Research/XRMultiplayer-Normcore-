using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WhackAMoleSpawner : MonoBehaviour {
    public int maxMolesUp = 1;
    public float spawnMin = 0.25f;
    public float spawnMax = 0.45f;

    [Header("Debug")]
    public bool verboseLogs = true;

    private List<WhackHole> holes = new List<WhackHole>();
    private bool generating = false;
    private WhackAMoleTaskManager manager;
    private Coroutine routine;

    void Awake() {
        manager = FindObjectOfType<WhackAMoleTaskManager>();
        RefreshHoles();
    }

    public void StartGenerating() {
        if (manager == null || !manager.IsHost()) {
            Log("StartGenerating ignored (not host).");
            return;
        }
        if (generating) return;

        generating = true;

        int seed = (RoleManager.Instance != null && RoleManager.Instance.GetCommonSeed() != 0)
            ? RoleManager.Instance.GetCommonSeed()
            : 12345;

        Random.InitState(seed);

        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(GenerateRoutine());

        Log($"Generating started. seed={seed}");
    }

    public void StopGenerating() {
        generating = false;
        if (routine != null) {
            StopCoroutine(routine);
            routine = null;
        }
        Log("Generating stopped.");
    }

    private IEnumerator GenerateRoutine() {
        yield return WaitForMolesReady();

        while (generating) {
            yield return new WaitForSeconds(Random.Range(spawnMin, spawnMax));

            int up = 0;
            foreach (var h in holes)
                if (h != null && h.mole != null && h.mole.IsUp)
                    up++;

            if (up >= maxMolesUp) continue;

            for (int tries = 0; tries < 12; tries++) {
                var hole = holes[Random.Range(0, holes.Count)];
                if (hole == null || hole.mole == null) continue;

                if (!hole.mole.IsUp) {
                    hole.mole.Pop_Authority();
                    WhackAMoleTaskManager.Instance.RegisterSpawn(hole);
                    Log($"POP -> {hole.name}");
                    break;
                }
            }
        }
    }

    private IEnumerator WaitForMolesReady() {
        float start = Time.time;
        while (Time.time - start < 10f) {
            RefreshHoles();

            bool ok = holes.Count > 0;
            if (ok) {
                foreach (var h in holes) {
                    if (h == null || h.mole == null || !h.mole.HasModel()) { ok = false; break; }
                }
            }

            if (ok) {
                Log($"Ready. holes={holes.Count}");
                yield break;
            }

            yield return null;
        }

        Debug.LogError("[WhackAMoleSpawner] Timed out waiting for holes/mole models.");
    }

    private void RefreshHoles() {
        holes.Clear();
        holes.AddRange(FindObjectsOfType<WhackHole>(true));
        holes.RemoveAll(h => h == null || h.mole == null);
    }

    private void Log(string msg) {
        if (!verboseLogs) return;
        Debug.Log($"[WhackAMoleSpawner] {msg}");
    }
}
