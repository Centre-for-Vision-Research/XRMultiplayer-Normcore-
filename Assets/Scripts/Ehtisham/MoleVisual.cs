using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MoleVisual : MonoBehaviour
{
    [Header("Motion")]
    public float popDistance = 0.03f;
    public float moveSpeed = 0.08f;

    [Header("Hit FX (Prefab + AudioClip)")]
    [Tooltip("Prefab to spawn on hit (e.g., particle prefab). Can be a ParticleSystem prefab or any GameObject.")]
    public GameObject hitVfxPrefab;

    [Tooltip("Audio clip to play on hit (mp3/wav/ogg imported as AudioClip).")]
    public AudioClip hitSfx;

    [Tooltip("Optional: where to spawn VFX (defaults to this transform).")]
    public Transform vfxSpawnPoint;

    [Tooltip("Optional: auto-destroy spawned VFX after seconds (0 = don't auto destroy).")]
    public float vfxAutoDestroySeconds = 2.0f;

    [Tooltip("If true, PlayHitFx only plays for local hitter. If false, can be played for everyone when resolve event arrives.")]
    public bool suppressNonLocalFx = false;

    private Vector3 _visibleLocalPos;
    private Vector3 _hiddenLocalPos;
    private Collider _col;

    private int _holeIndex = -1;

    // local prediction (for hitter authority feel)
    private int _predictedHideSeq = -999;
    private float _predictedHideUntilLocalTime = 0f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _col = GetComponent<Collider>();

        _holeIndex = ParseHoleIndexFromParent();
        _visibleLocalPos = transform.localPosition;
        _hiddenLocalPos = _visibleLocalPos + new Vector3(0f, 0f, -popDistance);

        // start hidden until game state says otherwise
        transform.localPosition = _hiddenLocalPos;
        _col.enabled = false;

        if (vfxSpawnPoint == null) vfxSpawnPoint = transform;

        // Ensure there is an AudioSource to play clips
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f; // 3D sound
    }

    private int ParseHoleIndexFromParent()
    {
        if (transform.parent == null) return -1;

        // expects parent name like: "hole (12)"
        string n = transform.parent.name;
        int open = n.IndexOf('(');
        int close = n.IndexOf(')');
        if (open >= 0 && close > open)
        {
            string inside = n.Substring(open + 1, close - open - 1);
            if (int.TryParse(inside, out int v)) return v;
        }
        return -1;
    }

    public int HoleIndex => _holeIndex;

    public void PredictHideForSeq(int seq, float localHoldSeconds = 0.25f)
    {
        _predictedHideSeq = seq;
        _predictedHideUntilLocalTime = Time.realtimeSinceStartup + localHoldSeconds;
    }

    /// <summary>
    /// Plays hit VFX + SFX. Call this on local hit for instant feedback,
    /// and also from authority "resolve" event to sync feedback for both players.
    /// </summary>
    public void PlayHitFx(bool isLocalHitter = false)
    {
        if (suppressNonLocalFx && !isLocalHitter) return;

        // VFX
        if (hitVfxPrefab != null && vfxSpawnPoint != null)
        {
            GameObject spawned = Instantiate(hitVfxPrefab, vfxSpawnPoint.position, vfxSpawnPoint.rotation);

            // If it has a ParticleSystem, optionally destroy after it finishes
            if (vfxAutoDestroySeconds > 0f)
            {
                Destroy(spawned, vfxAutoDestroySeconds);
            }
        }

        // SFX
        if (hitSfx != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(hitSfx);
        }
    }

    private void Update()
    {
        var gs = WhackGameStateSync.Instance;
        if (gs == null) return;

        int state = gs.GameState;
        int hostNow = gs.EstimateHostNowMs();

        bool shouldBeUp = false;
        bool hittable = false;

        if (state == 1)
        {
            // WaitingForStartHit: current hole is up and hittable
            shouldBeUp = (gs.CurrentHoleIndex == _holeIndex);
            hittable = shouldBeUp;
        }
        else if (state == 2)
        {
            // Running: mole is up only during its scheduled window
            bool isThis = (gs.CurrentHoleIndex == _holeIndex);
            bool inWindow = hostNow >= gs.MoleStartMs && hostNow <= gs.MoleEndMs;
            shouldBeUp = isThis && inWindow;
            hittable = shouldBeUp;
        }
        else
        {
            // Ended or WaitingForPlayers
            shouldBeUp = false;
            hittable = false;
        }

        // local prediction: hide immediately for hitter feel
        if (_predictedHideSeq == gs.CurrentSeq)
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

        Vector3 target = shouldBeUp ? _visibleLocalPos : _hiddenLocalPos;
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, moveSpeed * Time.deltaTime);

        _col.enabled = hittable;
    }
}
