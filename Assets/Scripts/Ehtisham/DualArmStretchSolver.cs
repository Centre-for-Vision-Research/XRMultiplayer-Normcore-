using UnityEngine;

public class DualArmStretchSolverHighPrecision : MonoBehaviour {
    [System.Serializable]
    public class Arm {
        public string name = "Arm";
        public Transform shoulder;
        public Transform upperArm;
        public Transform lowerArm;
        public Transform hand;
        public string targetNameInScene;
        [HideInInspector] public Transform target;
        [HideInInspector] public float defaultTotalLength;
        [HideInInspector] public float currentStretchRatio = 1f;
        [HideInInspector] public float stretchVelocity = 0f;
    }

    [Header("Left Arm")]
    public Arm leftArm;

    [Header("Right Arm")]
    public Arm rightArm;

    [Header("Stretch Settings")]
    public float maxStretchFactor = 1.25f;
    public float smoothTime = 0.08f;
    public float wristToPalmOffset = 0.1f;       // matches tracking offset
    public float minDistanceToStretch = 0.02f;   // start stretching only after this
    public float lowerArmBias = 0.7f;            // 70% of stretch goes to forearm

    public bool debugDraw = false;
    private bool targetsAssigned = false;

    void Start() {
        InitArm(ref leftArm);
        InitArm(ref rightArm);
    }

    void LateUpdate() {
        if (!targetsAssigned) {
            TryAssignTargets();
            if (!targetsAssigned) return;
        }

        StretchArm(ref leftArm);
        StretchArm(ref rightArm);
    }

    void InitArm(ref Arm arm) {
        float upperLen = Vector3.Distance(arm.upperArm.position, arm.lowerArm.position);
        float lowerLen = Vector3.Distance(arm.lowerArm.position, arm.hand.position);
        arm.defaultTotalLength = upperLen + lowerLen;
    }

    void TryAssignTargets() {
        leftArm.target = GameObject.Find(leftArm.targetNameInScene)?.transform;
        rightArm.target = GameObject.Find(rightArm.targetNameInScene)?.transform;

        if (leftArm.target != null && rightArm.target != null) {
            targetsAssigned = true;
            Debug.Log("✅ High-Precision ArmStretchSolver: Targets found.");
        }
    }

    void StretchArm(ref Arm arm) {
        if (arm.shoulder == null || arm.target == null) return;

        // Corrected real-world reach
        float actualReach = Vector3.Distance(arm.shoulder.position, arm.target.position) - wristToPalmOffset;

        // If not past threshold, don't stretch
        if (actualReach < arm.defaultTotalLength + minDistanceToStretch) {
            arm.currentStretchRatio = Mathf.SmoothDamp(arm.currentStretchRatio, 1f, ref arm.stretchVelocity, smoothTime);
        } else {
            float targetRatio = Mathf.Clamp(actualReach / arm.defaultTotalLength, 1f, maxStretchFactor);
            arm.currentStretchRatio = Mathf.SmoothDamp(arm.currentStretchRatio, targetRatio, ref arm.stretchVelocity, smoothTime);
        }

        // Apply with upper/lower bias
        float upperRatio = Mathf.Lerp(1f, arm.currentStretchRatio, 1f - lowerArmBias);
        float lowerRatio = Mathf.Lerp(1f, arm.currentStretchRatio, lowerArmBias);

        arm.upperArm.localScale = GetAxisScale(arm.upperArm, upperRatio);
        arm.lowerArm.localScale = GetAxisScale(arm.lowerArm, lowerRatio);


        if (debugDraw) {
            Debug.DrawLine(arm.shoulder.position, arm.target.position, Color.magenta);
        }
    }
    
    Vector3 GetAxisScale(Transform bone, float stretch)
    {
        Vector3 localDir = (bone.InverseTransformDirection(bone.GetChild(0).position - bone.position)).normalized;

        // Choose the dominant axis (X, Y, or Z)
        int axis = 0;
        float maxDot = Mathf.Abs(localDir.x);
        if (Mathf.Abs(localDir.y) > maxDot) { axis = 1; maxDot = Mathf.Abs(localDir.y); }
        if (Mathf.Abs(localDir.z) > maxDot) { axis = 2; }

        Vector3 scale = Vector3.one;
        scale[axis] = stretch;
        return scale;
    }

}




// using UnityEngine;

// public class DualArmStretchSolverHighFidelity : MonoBehaviour {
//     [System.Serializable]
//     public class Arm {
//         public string name = "Arm";
//         public Transform shoulder;
//         public Transform upperArm;
//         public Transform lowerArm;
//         public Transform hand;
//         public string targetNameInScene;  // e.g., "Right Cont Target"
//         [HideInInspector] public Transform target;
//         [HideInInspector] public float defaultTotalLength;
//         [HideInInspector] public float currentStretchRatio = 1f;
//     }

//     [Header("Left Arm")]
//     public Arm leftArm;

//     [Header("Right Arm")]
//     public Arm rightArm;

//     [Header("Stretch Settings")]
//     public float maxStretchFactor = 1.3f;
//     public float stretchLerpSpeed = 5f;
//     public float ignoreStretchBelow = 0.9f;   // Avoid compressing
//     public float maxAllowedDistance = 2.0f;   // Ignore unrealistic outliers
//     public bool debugDraw = false;

//     private bool targetsAssigned = false;

//     void Start() {
//         InitArm(ref leftArm);
//         InitArm(ref rightArm);
//     }

//     void LateUpdate() {
//         if (!targetsAssigned) {
//             TryAssignTargets();
//             if (!targetsAssigned) return;
//         }

//         ProcessArm(ref leftArm);
//         ProcessArm(ref rightArm);
//     }

//     void InitArm(ref Arm arm) {
//         if (arm.upperArm == null || arm.lowerArm == null || arm.hand == null) return;

//         float upperLen = Vector3.Distance(arm.upperArm.position, arm.lowerArm.position);
//         float lowerLen = Vector3.Distance(arm.lowerArm.position, arm.hand.position);
//         arm.defaultTotalLength = upperLen + lowerLen;
//     }

//     void TryAssignTargets() {
//         leftArm.target = GameObject.Find(leftArm.targetNameInScene)?.transform;
//         rightArm.target = GameObject.Find(rightArm.targetNameInScene)?.transform;

//         if (leftArm.target != null && rightArm.target != null) {
//             targetsAssigned = true;
//             Debug.Log("✅ VR targets assigned to ArmStretchSolver.");
//         }
//     }

//     void ProcessArm(ref Arm arm) {
//         if (arm.shoulder == null || arm.target == null) return;

//         float distance = Vector3.Distance(arm.shoulder.position, arm.target.position);
//         if (distance > maxAllowedDistance) return; // skip weird spikes

//         float targetStretchRatio = distance / arm.defaultTotalLength;

//         // Clamp only above 1 (no compression), and within stretch limit
//         if (targetStretchRatio < ignoreStretchBelow) {
//             targetStretchRatio = 1f;
//         } else {
//             targetStretchRatio = Mathf.Clamp(targetStretchRatio, 1f, maxStretchFactor);
//         }

//         // Smooth Lerp
//         arm.currentStretchRatio = Mathf.Lerp(arm.currentStretchRatio, targetStretchRatio, Time.deltaTime * stretchLerpSpeed);

//         Vector3 upper = arm.upperArm.localScale;
//         Vector3 lower = arm.lowerArm.localScale;

//         arm.upperArm.localScale = new Vector3(upper.x, arm.currentStretchRatio, upper.z);
//         arm.lowerArm.localScale = new Vector3(lower.x, arm.currentStretchRatio, lower.z);

//         if (debugDraw) {
//             Debug.DrawLine(arm.shoulder.position, arm.target.position, Color.cyan);
//         }
//     }
// }




