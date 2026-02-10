using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MoleVisual : MonoBehaviour
{
    [Header("Motion")]
    public float popDistance = 0.03f;
    public float moveSpeed = 0.08f;

    [Header("Hit FX (Prefab + AudioClip)")]
    public GameObject hitVfxPrefab;
    public AudioClip hitSfx;
    public Transform vfxSpawnPoint;
    public float vfxAutoDestroySeconds = 2.0f;

    private Vector3 _visibleLocalPos;
    private Vector3 _hiddenLocalPos;
    private Collider _col;
    private int _holeIndex = -1;

    private int _predictedHideSeq = -999;
    private float _predictedHideUntilLocalTime = 0f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _col = GetComponent<Collider>();

        _holeIndex = ParseHoleIndexFromParent();
        _visibleLocalPos = transform.localPosition;
        _hiddenLocalPos = _visibleLocalPos + new Vector3(0f, 0f, -popDistance);

        transform.localPosition = _hiddenLocalPos;
        _col.enabled = false;

        if (vfxSpawnPoint == null) vfxSpawnPoint = transform;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;

        Debug.Log($"[MoleVisual] '{name}' holeIndex={_holeIndex} parent='{transform.parent?.name}' root='{transform.root.name}'");
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
                if (int.TryParse(inside, out int v))
                    return v;
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

    public void PlayHitFx(bool isLocalHitter = false)
    {
        // VFX
        if (hitVfxPrefab != null && vfxSpawnPoint != null)
        {
            GameObject spawned = Instantiate(hitVfxPrefab, vfxSpawnPoint.position, vfxSpawnPoint.rotation);
            if (vfxAutoDestroySeconds > 0f)
                Destroy(spawned, vfxAutoDestroySeconds);
        }

        // SFX
        if (hitSfx != null && _audioSource != null)
            _audioSource.PlayOneShot(hitSfx);
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
            shouldBeUp = (gs.CurrentHoleIndex == _holeIndex);
            hittable = shouldBeUp;
        }
        else if (state == 2)
        {
            bool isThis = (gs.CurrentHoleIndex == _holeIndex);
            bool inWindow = hostNow >= gs.MoleStartMs && hostNow <= gs.MoleEndMs;
            shouldBeUp = isThis && inWindow;
            hittable = shouldBeUp;
        }
        else
        {
            shouldBeUp = false;
            hittable = false;
        }

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
