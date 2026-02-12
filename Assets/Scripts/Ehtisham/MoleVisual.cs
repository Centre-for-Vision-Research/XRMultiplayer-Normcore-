using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MoleVisual : MonoBehaviour
{
    [Header("Motion")]
    public float popDistance = 0.03f;
    public float moveSpeed = 0.08f;

    [Header("Big Mole (SharedBig)")]
    [Tooltip("Multiplier applied to THIS mole object's localScale when the mole is SharedBig.")]
    public float bigVisualScale = 1.35f;

    [Tooltip("How far down (meters) the big mole goes after the first hit. Should be < popDistance.")]
    public float bigPartialDownDistance = 0.012f;

    [Header("Materials (assign in Inspector)")]
    public Material teacherMat;
    public Material studentMat;
    public Material sharedMat; // brown

    [Header("Hit FX")]
    public GameObject hitVfxPrefab;
    public Transform vfxSpawnPoint;
    public float vfxAutoDestroySeconds = 2.0f;

    [Header("Audio FX")]
    public AudioClip correctHitSfx;
    public AudioClip wrongHitSfx;
    public AudioClip bigProgressSfx; // optional, falls back to correct
    public float sfxVolume = 1f;

    [Header("FX Robustness")]
    public float fxCooldownSeconds = 0.05f;

    private Vector3 _visibleLocalPos;
    private Vector3 _hiddenLocalPos;
    private Vector3 _bigPartialLocalPos;

    private Collider _col;
    private int _holeIndex = -1;

    private AudioSource _audioSource;
    private float _lastFxTime = -999f;

    // local prediction for instant feel
    private int _predictedHideSeq = -999;
    private float _predictedHideUntilLocalTime = 0f;

    private int _predictedBigSeq = -999;
    private float _predictedBigUntilLocalTime = 0f;

    // cached refs for materials
    private Transform _body, _lhand, _rhand;

    // base scale for scaling the entire mole object (covers hair, whiskers, etc.)
    private Vector3 _rootBaseScale;

    private MoleKind _lastKindApplied = (MoleKind)(-99);

    private void Awake()
    {
        _col = GetComponent<Collider>();
        _holeIndex = ParseHoleIndexFromParent();

        _visibleLocalPos = transform.localPosition;
        _hiddenLocalPos = _visibleLocalPos + new Vector3(0f, 0f, -popDistance);

        float partial = Mathf.Clamp(bigPartialDownDistance, 0f, popDistance);
        _bigPartialLocalPos = _visibleLocalPos + new Vector3(0f, 0f, -partial);

        // cache base scale of the whole mole object
        _rootBaseScale = transform.localScale;

        transform.localPosition = _hiddenLocalPos;
        _col.enabled = false;

        if (vfxSpawnPoint == null) vfxSpawnPoint = transform;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;

        CacheVisualChildren();
    }

    private void CacheVisualChildren()
    {
        // body is usually "body"
        _body = FindChildByAnyName(transform, "body", "Body");

        // hands: support both old names and your camelCase names
        _lhand = FindChildByAnyName(transform, "leftHand", "lefthand", "LeftHand", "Lefthand");
        _rhand = FindChildByAnyName(transform, "rightHand", "righthand", "RightHand", "Righthand");
    }

    private static Transform FindChildByAnyName(Transform root, params string[] names)
    {
        foreach (var n in names)
        {
            var direct = root.Find(n);
            if (direct != null) return direct;
        }

        // fallback: recursive search (case-insensitive)
        return FindChildRecursive(root, names);
    }

    private static Transform FindChildRecursive(Transform root, string[] names)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var c = root.GetChild(i);
            foreach (var n in names)
            {
                if (string.Equals(c.name, n, System.StringComparison.OrdinalIgnoreCase))
                    return c;
            }

            var found = FindChildRecursive(c, names);
            if (found != null) return found;
        }
        return null;
    }

    private int ParseHoleIndexFromParent()
    {
        Transform t = transform;
        while (t != null)
        {
            string n = t.name;
            int open = n.IndexOf('(');
            int close = n.IndexOf(')');

            if (open >= 0 && close > open)
            {
                string inside = n.Substring(open + 1, close - open - 1);
                if (int.TryParse(inside, out int v)) return v;
            }

            t = t.parent;
        }
        return -1;
    }

    public int HoleIndex => _holeIndex;

    public void PredictHideForSeq(int seq, float localHoldSeconds = 0.25f)
    {
        _predictedHideSeq = seq;
        _predictedHideUntilLocalTime = Time.realtimeSinceStartup + localHoldSeconds;
    }

    public void PredictBigProgressForSeq(int seq, float localHoldSeconds = 0.30f)
    {
        _predictedBigSeq = seq;
        _predictedBigUntilLocalTime = Time.realtimeSinceStartup + localHoldSeconds;
    }

    public void PlayHitFx(ResolveKind kind)
    {
        if (Time.realtimeSinceStartup - _lastFxTime < fxCooldownSeconds) return;
        _lastFxTime = Time.realtimeSinceStartup;

        if (hitVfxPrefab != null && vfxSpawnPoint != null)
        {
            GameObject spawned = Instantiate(hitVfxPrefab, vfxSpawnPoint.position, vfxSpawnPoint.rotation);
            if (vfxAutoDestroySeconds > 0f) Destroy(spawned, vfxAutoDestroySeconds);
        }

        if (_audioSource == null) return;

        if (kind == ResolveKind.HitWrong)
        {
            if (wrongHitSfx != null) _audioSource.PlayOneShot(wrongHitSfx, sfxVolume);
            return;
        }

        if (kind == ResolveKind.BigFirst)
        {
            var clip = bigProgressSfx != null ? bigProgressSfx : correctHitSfx;
            if (clip != null) _audioSource.PlayOneShot(clip, sfxVolume);
            return;
        }

        if (kind == ResolveKind.HitCorrect || kind == ResolveKind.BigComplete)
        {
            if (correctHitSfx != null) _audioSource.PlayOneShot(correctHitSfx, sfxVolume);
        }
    }

    private void ApplyKindVisual(MoleKind kind)
    {
        bool isBig = (kind == MoleKind.SharedBig);

        // IMPORTANT: Always enforce the correct scale, even if kind didn't change.
        ApplyBigScale(isBig);

        // Materials can be skipped if kind hasn't changed.
        if (kind == _lastKindApplied) return;
        _lastKindApplied = kind;

        Material mat = sharedMat;

        if (kind == MoleKind.TeacherSmall) mat = teacherMat != null ? teacherMat : sharedMat;
        else if (kind == MoleKind.StudentSmall) mat = studentMat != null ? studentMat : sharedMat;

        ApplyMaterialTo(_body, mat);
        ApplyMaterialTo(_lhand, mat);
        ApplyMaterialTo(_rhand, mat);
    }


    private void ApplyMaterialTo(Transform t, Material m)
    {
        if (t == null || m == null) return;

        // apply to all renderers under that part (covers skinned mesh, etc.)
        var renderers = t.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material = m;
    }

    private void ApplyBigScale(bool isBig)
    {
        // scale the whole mole object so extra parts like hair, whiskers, etc. scale too
        transform.localScale = isBig ? (_rootBaseScale * bigVisualScale) : _rootBaseScale;
    }

    private void Update()
    {
        var gs = WhackGameStateSync.Instance;
        if (gs == null || !gs.IsModelReady()) return;

        int hostNow = gs.EstimateHostNowMs();
        int state = gs.GameState;

        bool shouldBeUp = false;
        bool hittable = false;

        MoleKind activeKind = MoleKind.SharedSmall;
        int activeSeq = -1;
        int bigStage = 0;

        if (state == 1 || state == 2)
        {
            const int graceMs = 60;

            if (gs.TryFindActiveSlotForHole(_holeIndex, hostNow, graceMs,
                out int slotIndex, out int seq, out MoleKind kind, out int slotBigStage, out int firstRole))
            {
                shouldBeUp = true;
                hittable = true;

                activeKind = kind;
                activeSeq = seq;
                bigStage = slotBigStage;

                // local prediction for big stage transition (helps feel instant)
                if (_predictedBigSeq == seq && Time.realtimeSinceStartup <= _predictedBigUntilLocalTime)
                    bigStage = 1;
                else if (_predictedBigSeq == seq)
                    _predictedBigSeq = -999;

                // local prediction: hide immediately for hitter feel (small moles, and big completion)
                if (_predictedHideSeq == seq)
                {
                    if (Time.realtimeSinceStartup <= _predictedHideUntilLocalTime)
                    {
                        shouldBeUp = false;
                        hittable = false;
                    }
                    else
                    {
                        _predictedHideSeq = -999;
                    }
                }
            }
        }

        if (shouldBeUp)
            ApplyKindVisual(activeKind);
        else
            ApplyBigScale(false); // ensure it returns to base scale when hidden

        Vector3 target;

        if (!shouldBeUp) target = _hiddenLocalPos;
        else
        {
            bool isBig = (activeKind == MoleKind.SharedBig);
            if (isBig && bigStage == 1) target = _bigPartialLocalPos;
            else target = _visibleLocalPos;
        }

        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, moveSpeed * Time.deltaTime);
        _col.enabled = hittable;
    }
}
