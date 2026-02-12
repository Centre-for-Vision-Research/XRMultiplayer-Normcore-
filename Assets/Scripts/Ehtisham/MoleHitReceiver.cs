using UnityEngine;

[RequireComponent(typeof(MoleVisual))]
public class MoleHitReceiver : MonoBehaviour
{
    private MoleVisual _mole;

    private int _lastSeqSent = int.MinValue;
    private float _lastLocalHitTime = -999f;

    [Tooltip("Seconds. Prevent trigger chatter.")]
    public float localCooldownSeconds = 0.06f;

    private void Awake()
    {
        _mole = GetComponent<MoleVisual>();
    }

    private int GetLocalRole()
    {
        if (RoleManager.Instance == null) return 0;
        int cid = WhackGameStateSync.Instance != null && WhackGameStateSync.Instance.realtime != null
            ? WhackGameStateSync.Instance.realtime.clientID
            : -1;

        if (RoleManager.Instance.IsStudent(cid)) return 1;
        return 0; // teacher default
    }

    private bool IsCorrectHit(MoleKind kind, int byRole)
    {
        if (kind == MoleKind.SharedSmall || kind == MoleKind.SharedBig) return true;
        if (kind == MoleKind.TeacherSmall) return byRole == 0;
        if (kind == MoleKind.StudentSmall) return byRole == 1;
        return true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hammer")) return;

        var gs = WhackGameStateSync.Instance;
        if (gs == null || !gs.IsModelReady()) return;

        if (gs.GameState != 1 && gs.GameState != 2) return;

        if (Time.realtimeSinceStartup - _lastLocalHitTime < localCooldownSeconds) return;

        int hostNow = gs.EstimateHostNowMs();
        const int graceMs = 60;

        if (!gs.TryFindActiveSlotForHole(_mole.HoleIndex, hostNow, graceMs,
            out int slotIndex, out int seq, out MoleKind kind, out int bigStage, out int bigFirstRole))
            return;

        if (_lastSeqSent == seq) return;

        var input = other.GetComponentInParent<WhackPlayerInput>();
        if (input == null) return;
        if (!input.IsOwnedLocally) return;

        int myRole = GetLocalRole();

        // Big mole stage1: only the OTHER role can complete
        bool isBig = (kind == MoleKind.SharedBig);
        if (isBig && bigStage == 1 && bigFirstRole == myRole)
            return;

        _lastSeqSent = seq;
        _lastLocalHitTime = Time.realtimeSinceStartup;

        // local feedback + prediction
        if (isBig)
        {
            if (bigStage == 0)
            {
                _mole.PredictBigProgressForSeq(seq, 0.35f);
                _mole.PlayHitFx(ResolveKind.BigFirst);
            }
            else
            {
                _mole.PredictHideForSeq(seq, 0.25f);
                _mole.PlayHitFx(ResolveKind.BigComplete);
            }
        }
        else
        {
            bool correct = IsCorrectHit(kind, myRole);
            _mole.PredictHideForSeq(seq, 0.25f);
            _mole.PlayHitFx(correct ? ResolveKind.HitCorrect : ResolveKind.HitWrong);
        }

        input.TrySendHit(_mole.HoleIndex, slotIndex, seq);

        var hand = other.GetComponent<HammerHand>();
        if (hand != null) WhackHaptics.Pulse(hand.node, 0.75f, 0.08f);
    }
}
