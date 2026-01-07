using UnityEngine;
using Normal.Realtime;

public class DualArmStretchSolverHighPrecision : MonoBehaviour
{
    [System.Serializable]
    public class Arm {
        public string name = "Arm";
        public Transform shoulder;
        public Transform upperArm;
        public Transform lowerArm;
        public Transform hand;

        public Transform target; //  Assigned directly in inspector

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
    public float wristToPalmOffset = 0.1f;
    public float minDistanceToStretch = 0.02f;
    public float lowerArmBias = 0f;

    public bool debugDraw = false;

    private bool isLocalPlayer = false;

    void Start()
    {

        InitArm(ref leftArm);
        InitArm(ref rightArm);
    }

    void LateUpdate()
    {
        if (leftArm.target == null || rightArm.target == null) return;

        StretchArm(ref leftArm);
        StretchArm(ref rightArm);
    }

    void InitArm(ref Arm arm)
    {
        float upperLen = Vector3.Distance(arm.upperArm.position, arm.lowerArm.position);
        float lowerLen = Vector3.Distance(arm.lowerArm.position, arm.hand.position);
        arm.defaultTotalLength = upperLen + lowerLen;
    }

    void StretchArm(ref Arm arm)
    {
        if (arm.shoulder == null || arm.target == null) return;

        float actualReach = Vector3.Distance(arm.shoulder.position, arm.target.position) - wristToPalmOffset;

        if (actualReach < arm.defaultTotalLength + minDistanceToStretch)
        {
            arm.currentStretchRatio = Mathf.SmoothDamp(arm.currentStretchRatio, 1f, ref arm.stretchVelocity, smoothTime);
        }
        else
        {
            float targetRatio = Mathf.Clamp(actualReach / arm.defaultTotalLength, 1f, maxStretchFactor);
            arm.currentStretchRatio = Mathf.SmoothDamp(arm.currentStretchRatio, targetRatio, ref arm.stretchVelocity, smoothTime);
        }

        float upperRatio = Mathf.Lerp(1f, arm.currentStretchRatio, 1f - lowerArmBias);
        float lowerRatio = Mathf.Lerp(1f, arm.currentStretchRatio, lowerArmBias);

        arm.upperArm.localScale = GetAxisScale(arm.upperArm, upperRatio);
        arm.lowerArm.localScale = GetAxisScale(arm.lowerArm, lowerRatio);

        if (debugDraw)
        {
            Debug.DrawLine(arm.shoulder.position, arm.target.position, Color.magenta);
        }
    }

    Vector3 GetAxisScale(Transform bone, float stretch)
    {
        Vector3 localDir = (bone.InverseTransformDirection(bone.GetChild(0).position - bone.position)).normalized;

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

// public class DualArmStretchSolverHighPrecision : MonoBehaviour {
//     [System.Serializable]
//     public class Arm {
//         public string name = "Arm";
//         public Transform shoulder;
//         public Transform upperArm;
//         public Transform lowerArm;
//         public Transform hand;
//         public string targetNameInScene;
//         [HideInInspector] public Transform target;
//         [HideInInspector] public float defaultTotalLength;
//         [HideInInspector] public float currentStretchRatio = 1f;
//         [HideInInspector] public float stretchVelocity = 0f;
//     }

//     [Header("Left Arm")]
//     public Arm leftArm;

//     [Header("Right Arm")]
//     public Arm rightArm;

//     [Header("Stretch Settings")]
//     public float maxStretchFactor = 1.25f;
//     public float smoothTime = 0.08f;
//     public float wristToPalmOffset = 0.1f;       // matches tracking offset
//     public float minDistanceToStretch = 0.02f;   // start stretching only after this
//     public float lowerArmBias = 0f;

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

//         StretchArm(ref leftArm);
//         StretchArm(ref rightArm);
//     }

//     void InitArm(ref Arm arm) {
//         float upperLen = Vector3.Distance(arm.upperArm.position, arm.lowerArm.position);
//         float lowerLen = Vector3.Distance(arm.lowerArm.position, arm.hand.position);
//         arm.defaultTotalLength = upperLen + lowerLen;
//     }

//     void TryAssignTargets() {
//         leftArm.target = GameObject.Find(leftArm.targetNameInScene)?.transform;
//         rightArm.target = GameObject.Find(rightArm.targetNameInScene)?.transform;

//         if (leftArm.target != null && rightArm.target != null) {
//             targetsAssigned = true;
//             Debug.Log("✅ High-Precision ArmStretchSolver: Targets found.");
//         }
//     }

//     void StretchArm(ref Arm arm) {
//         if (arm.shoulder == null || arm.target == null) return;

//         // Corrected real-world reach
//         float actualReach = Vector3.Distance(arm.shoulder.position, arm.target.position) - wristToPalmOffset;

//         // If not past threshold, don't stretch
//         if (actualReach < arm.defaultTotalLength + minDistanceToStretch) {
//             arm.currentStretchRatio = Mathf.SmoothDamp(arm.currentStretchRatio, 1f, ref arm.stretchVelocity, smoothTime);
//         } else {
//             float targetRatio = Mathf.Clamp(actualReach / arm.defaultTotalLength, 1f, maxStretchFactor);
//             arm.currentStretchRatio = Mathf.SmoothDamp(arm.currentStretchRatio, targetRatio, ref arm.stretchVelocity, smoothTime);
//         }

//         // Apply with upper/lower bias
//         float upperRatio = Mathf.Lerp(1f, arm.currentStretchRatio, 1f - lowerArmBias);
//         float lowerRatio = Mathf.Lerp(1f, arm.currentStretchRatio, lowerArmBias);

//         arm.upperArm.localScale = GetAxisScale(arm.upperArm, upperRatio);
//         arm.lowerArm.localScale = GetAxisScale(arm.lowerArm, lowerRatio);


//         if (debugDraw) {
//             Debug.DrawLine(arm.shoulder.position, arm.target.position, Color.magenta);
//         }
//     }
    
//     Vector3 GetAxisScale(Transform bone, float stretch)
//     {
//         Vector3 localDir = (bone.InverseTransformDirection(bone.GetChild(0).position - bone.position)).normalized;

//         // Choose the dominant axis (X, Y, or Z)
//         int axis = 0;
//         float maxDot = Mathf.Abs(localDir.x);
//         if (Mathf.Abs(localDir.y) > maxDot) { axis = 1; maxDot = Mathf.Abs(localDir.y); }
//         if (Mathf.Abs(localDir.z) > maxDot) { axis = 2; }

//         Vector3 scale = Vector3.one;
//         scale[axis] = stretch;
//         return scale;
//     }

// }


