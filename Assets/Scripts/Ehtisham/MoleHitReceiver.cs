using UnityEngine;
using Normal.Realtime;

[RequireComponent(typeof(MoleVisual))]
public class MoleHitReceiver : MonoBehaviour
{
    private MoleVisual _mole;

    // local debounce to avoid double-trigger spam on the same mole+seq
    private int _lastSeqSent = int.MinValue;

    private void Awake()
    {
        _mole = GetComponent<MoleVisual>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hammer")) return;

        var view = other.GetComponentInParent<RealtimeView>();
        if (view == null || !view.isOwnedLocallySelf) return;

        var input = view.GetComponent<WhackPlayerInput>();
        if (input == null) return;

        var gs = WhackGameStateSync.Instance;
        if (gs == null) return;

        int seq = gs.CurrentSeq;
        if (_mole.HoleIndex != gs.CurrentHoleIndex) return;

        Debug.Log($"[LOCAL HIT] moleIndex={_mole.HoleIndex} seq={seq} byCollider='{other.name}' pos={other.transform.position}");

        _mole.PredictHideForSeq(seq, 0.25f);
        _mole.PlayHitFx(isLocalHitter: true);
        input.TrySendHit(_mole.HoleIndex, seq);

        var hand = other.GetComponent<HammerHand>();
        if (hand != null) WhackHaptics.Pulse(hand.node, 0.75f, 0.08f);
    }

}
