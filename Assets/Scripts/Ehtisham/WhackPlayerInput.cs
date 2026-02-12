using System;
using UnityEngine;
using Normal.Realtime;

public class WhackPlayerInput : RealtimeComponent<WhackPlayerInputModel>
{
    public event Action<WhackPlayerInput, HitEvent> HitEventReceived;

    public struct HitEvent
    {
        public int eventId;
        public int holeIndex;
        public int seq;
        public int sentAtHostMs;
        public int slotIndex;
    }

    private RealtimeView _view;
    private int _lastSeenHitEventId = 0;
    private int _localAntiSpamSeq = -999;

    public int OwnerClientIdInHierarchy => _view != null ? _view.ownerIDInHierarchy : -1;
    public bool IsOwnedLocally => _view != null && _view.isOwnedLocallySelf;

    private void Awake()
    {
        _view = GetComponent<RealtimeView>();
    }

    protected override void OnRealtimeModelReplaced(WhackPlayerInputModel previousModel, WhackPlayerInputModel currentModel)
    {
        if (previousModel != null)
            previousModel.hitEventIdDidChange -= OnHitEventIdChanged;

        if (currentModel != null)
        {
            currentModel.hitEventIdDidChange += OnHitEventIdChanged;
            _lastSeenHitEventId = currentModel.hitEventId;
        }
    }

    private void OnHitEventIdChanged(WhackPlayerInputModel m, int newId)
    {
        if (m == null) return;
        if (newId <= _lastSeenHitEventId) return;

        _lastSeenHitEventId = newId;

        HitEvent e = new HitEvent
        {
            eventId = newId,
            holeIndex = m.hitHoleIndex,
            seq = m.hitSeq,
            sentAtHostMs = m.hitSentAtHostMs,
            slotIndex = m.hitSlotIndex
        };

        HitEventReceived?.Invoke(this, e);
    }

    public void TrySendHit(int holeIndex, int slotIndex, int seq)
    {
        if (!IsOwnedLocally) return;
        if (model == null) return;

        if (_localAntiSpamSeq == seq) return;
        _localAntiSpamSeq = seq;

        int hostNow = WhackGameStateSync.Instance != null
            ? WhackGameStateSync.Instance.EstimateHostNowMs()
            : Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

        model.hitHoleIndex = holeIndex;
        model.hitSlotIndex = slotIndex;
        model.hitSeq = seq;
        model.hitSentAtHostMs = hostNow;
        model.hitEventId = model.hitEventId + 1;
    }
}
