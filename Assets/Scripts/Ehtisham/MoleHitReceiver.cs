using UnityEngine;
using Normal.Realtime;

[RequireComponent(typeof(MoleVisual))]
public class MoleHitReceiver : MonoBehaviour
{
    private MoleVisual _mole;

    private void Awake()
    {
        _mole = GetComponent<MoleVisual>();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Only accept hits from local player's hammers
        // We identify "local player" by finding a RealtimeView in the other collider's parent hierarchy
        var view = other.GetComponentInParent<RealtimeView>();
        if (view == null) return;
        if (!view.isOwnedLocallySelf) return;

        // Only accept if other collider is a hammer
        if (!other.CompareTag("Hammer")) return;

        var input = view.GetComponent<WhackPlayerInput>();
        if (input == null) return;

        var gs = WhackGameStateSync.Instance;
        if (gs == null) return;

        // Must be the scheduled hole and correct seq
        int seq = gs.CurrentSeq;
        if (_mole.HoleIndex != gs.CurrentHoleIndex) return;

        // Local immediate feel
        _mole.PredictHideForSeq(seq, 0.25f);

        // Send hit to authority through owned input model
        input.TrySendHit(_mole.HoleIndex, seq);

        // Local FX feel for hitter
        _mole.PlayHitFx(isLocalHitter: true);

        WhackHaptics.PulseBothHands();
    }
}
