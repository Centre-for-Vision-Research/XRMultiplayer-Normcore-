using UnityEngine;

[RequireComponent(typeof(MoleVisual))]
public class MoleHitReceiver : MonoBehaviour
{
    private MoleVisual _mole;

    private int _lastSeqSent = int.MinValue;
    private float _lastLocalHitTime = -999f;

    [Tooltip("Seconds. Prevent trigger chatter (multiple colliders, rapid OnTriggerEnter spam).")]
    public float localCooldownSeconds = 0.06f;

    private void Awake()
    {
        _mole = GetComponent<MoleVisual>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hammer")) return;

        var gs = WhackGameStateSync.Instance;
        if (gs == null || !gs.IsModelReady()) return;

        // Only allow hits when an active mole exists
        if (gs.GameState != 1 && gs.GameState != 2) return;

        int seq = gs.CurrentSeq;

        // Cooldown
        if (Time.realtimeSinceStartup - _lastLocalHitTime < localCooldownSeconds) return;

        // Seq debounce
        if (_lastSeqSent == seq) return;

        // Must be current hole
        if (_mole.HoleIndex != gs.CurrentHoleIndex) return;

        // IMPORTANT: find input in parents (MR pivots have their own RealtimeView)
        var input = other.GetComponentInParent<WhackPlayerInput>();
        if (input == null) return;

        if (!input.IsOwnedLocally) return;

        _lastSeqSent = seq;
        _lastLocalHitTime = Time.realtimeSinceStartup;

        _mole.PredictHideForSeq(seq, 0.25f);
        _mole.PlayHitFx(isLocalHitter: true);

        input.TrySendHit(_mole.HoleIndex, seq);

        var hand = other.GetComponent<HammerHand>();
        if (hand != null) WhackHaptics.Pulse(hand.node, 0.75f, 0.08f);
    }
}
