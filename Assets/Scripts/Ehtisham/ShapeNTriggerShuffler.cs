using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Normal.Realtime;


public class ShapeNTriggerShuffler : MonoBehaviour {
    IEnumerator Start() {
        // Wait until RoleManager is available and the common seed is set (non-zero)
        while (RoleManager.Instance == null || RoleManager.Instance.GetCommonSeed() == 0) {
            yield return null; // Wait for the next frame
        }
        int seed = RoleManager.Instance.GetCommonSeed();
        Random.InitState(seed);
        Debug.Log("ShapeNTriggerShuffler using common seed: " + seed);

        // Collect all ShapeCutouts and Triggers
        List<GameObject> cutouts = new List<GameObject>(GameObject.FindGameObjectsWithTag("ShapeCutout"));
        List<GameObject> triggers = new List<GameObject>(GameObject.FindGameObjectsWithTag("Trigger"));

        // Map: shape name -> cutout & trigger
        Dictionary<string, GameObject> cutoutMap = new Dictionary<string, GameObject>();
        Dictionary<string, GameObject> triggerMap = new Dictionary<string, GameObject>();

        foreach (GameObject cutout in cutouts) {
            string shapeName = cutout.name.Replace("Cut", "");
            cutoutMap[shapeName] = cutout;
        }

        foreach (GameObject trigger in triggers) {
            triggerMap[trigger.name] = trigger;
        }

        // Build list of shape names that exist in both maps
        List<string> shapeNames = new List<string>();
        foreach (var name in cutoutMap.Keys) {
            if (triggerMap.ContainsKey(name)) {
                shapeNames.Add(name);
            }
        }

        // Shuffle the shapeNames list using the shared Random state
        List<string> shuffledNames = new List<string>(shapeNames);
        for (int i = 0; i < shuffledNames.Count; i++) {
            int j = Random.Range(i, shuffledNames.Count);
            (shuffledNames[i], shuffledNames[j]) = (shuffledNames[j], shuffledNames[i]);
        }

        // Swap the transforms of matching cutouts and triggers based on the shuffled order
        for (int i = 0; i < shapeNames.Count; i++) {
            string nameA = shapeNames[i];
            string nameB = shuffledNames[i];

            if (nameA == nameB)
                continue; // If not shuffled, skip

            // Swap cutout transforms
            Transform tA = cutoutMap[nameA].transform;
            Transform tB = cutoutMap[nameB].transform;
            SwapTransforms(tA, tB);

            // Swap trigger transforms
            Transform trA = triggerMap[nameA].transform;
            Transform trB = triggerMap[nameB].transform;
            SwapTransforms(trA, trB);
        }


        // Apply role-based settings on LOCALLY OWNED AVATR AFTER shuffling

        RoleBasedSettings[] allRBS = FindObjectsOfType<RoleBasedSettings>();
        RoleBasedSettings localRBS = null;

        foreach (var rbs in allRBS) {
            RealtimeView view = rbs.GetComponent<RealtimeView>();
            if (view != null && view.isOwnedLocallySelf) {
                localRBS = rbs;
                break;
            }
        }

        if (localRBS != null) {
            localRBS.ApplyRoleSettings();
        } else {
            Debug.LogWarning("Local RoleBasedSettings not found.");
        }
    }

    void SwapTransforms(Transform a, Transform b) {
        Vector3 tempPos = a.position;
        Quaternion tempRot = a.rotation;

        a.position = b.position;
        a.rotation = b.rotation;

        b.position = tempPos;
        b.rotation = tempRot;
    }
}