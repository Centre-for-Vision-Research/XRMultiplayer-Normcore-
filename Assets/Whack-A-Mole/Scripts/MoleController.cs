using UnityEngine;
using Normal.Realtime;

[RequireComponent(typeof(Collider))]
public class MoleController : RealtimeComponent<MoleModel>
{
    [Header("Movement")]
    public float popDistance = 0.03f;
    public float moveSpeed = 0.05f;
    public float holdMin = 0.25f;
    public float holdMax = 0.50f;

    [Header("Debug")]
    public bool verboseLogs = true;
    public float logEverySeconds = 1.0f;

    // cached poses
    private Vector3 visibleLocalPos;
    private Vector3 hiddenLocalPos;

    private float currentHoldDuration;
    private float timer;

    private Collider col;
    private WhackAMoleTaskManager manager;

    private enum State { Hidden = 0, PoppingUp = 1, Holding = 2, GoingDown = 3, InitialUp = 4 }
    private State state = State.Hidden;

    public bool IsUp => state == State.PoppingUp || state == State.Holding || state == State.InitialUp;

    private float _nextLogTime;

    private void Awake()
    {
        col = GetComponent<Collider>();
        manager = FindObjectOfType<WhackAMoleTaskManager>();

        // Scene starts UP in your case. Cache current as visible.
        visibleLocalPos = transform.localPosition;
        hiddenLocalPos = visibleLocalPos + new Vector3(0f, 0f, -popDistance);
    }

    private void Start()
    {
        // IMPORTANT:
        // Do NOT force-hide here. It races with TaskManager boot.
        // Instead, if model exists, snap to model; otherwise do a local-only safe hide.
        if (model != null)
        {
            ApplyFromModel(immediate: true);
        }
        else
        {
            // model not bound yet, keep it locally hidden so visuals are not “all up”
            ForceHiddenLocal_NoPublish();
        }

        ThrottledStatus("Start()");
    }

    protected override void OnRealtimeModelReplaced(MoleModel prev, MoleModel curr)
    {
        if (prev != null)
        {
            prev.stateDidChange -= OnStateChanged;
            prev.isUpDidChange -= OnIsUpChanged;
        }

        if (curr != null)
        {
            curr.stateDidChange += OnStateChanged;
            curr.isUpDidChange += OnIsUpChanged;

            // Late joiners or late binding: snap immediately
            ApplyFromModel(immediate: true);
            ThrottledStatus("ModelReplaced()");
        }
    }

    private void OnDestroy()
    {
        if (model != null)
        {
            model.stateDidChange -= OnStateChanged;
            model.isUpDidChange -= OnIsUpChanged;
        }
    }

    private void OnStateChanged(MoleModel m, int value)
    {
        ApplyFromModel(immediate: false);
    }

    private void OnIsUpChanged(MoleModel m, bool value)
    {
        col.enabled = value;
    }

    // ------------------------------------------------------------
    // Authority: in your setup, clientID==0 is authority
    // ------------------------------------------------------------
    private bool IsAuthority()
    {
        var r = FindObjectOfType<Realtime>();
        return r != null && r.clientID == 0;
    }

    private bool IsOwnedLocally()
    {
        var view = GetComponent<RealtimeView>();
        return view != null && view.isOwnedLocallySelf;
    }

    private void Publish()
    {
        if (!IsAuthority()) return;
        if (!IsOwnedLocally()) return;
        if (model == null) return;

        model.state = (int)state;
        model.isUp = IsUp;

        ThrottledStatus($"Publish -> state={(int)state} isUp={IsUp}");
    }

    private void ApplyFromModel(bool immediate)
    {
        if (model == null) return;

        state = (State)model.state;
        col.enabled = model.isUp;

        if (immediate)
        {
            transform.localPosition = (state == State.Hidden) ? hiddenLocalPos : visibleLocalPos;
        }
    }

    // ------------------------------------------------------------
    // Local-only (no publish) helpers for startup safety
    // ------------------------------------------------------------
    private void ForceHiddenLocal_NoPublish()
    {
        state = State.Hidden;
        timer = 0f;
        transform.localPosition = hiddenLocalPos;
        col.enabled = false;
    }

    // ------------------------------------------------------------
    // API called by TaskManager/Spawner (authority only)
    // Names match what your TaskManager expects.
    // ------------------------------------------------------------
    public void HideImmediate_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return;

        state = State.Hidden;
        timer = 0f;
        transform.localPosition = hiddenLocalPos;
        col.enabled = false;

        Publish();
    }

    public void ShowImmediate_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return;

        state = State.InitialUp;
        timer = 0f;
        transform.localPosition = visibleLocalPos;
        col.enabled = true;

        Publish();
    }

    public void Pop_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return;
        if (state != State.Hidden) return;

        currentHoldDuration = Random.Range(holdMin, holdMax);
        timer = 0f;
        state = State.PoppingUp;

        Publish();
    }

    public bool Hit_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return false;
        if (!IsUp) return false;

        HideImmediate_Authority();
        return true;
    }

    // ------------------------------------------------------------
    // Simulation
    // Everyone animates locally from model.state.
    // Authority advances state machine + publishes.
    // ------------------------------------------------------------
    private void Update()
    {
        if (model == null) return;

        // Non-authority or not-owned: just animate toward implied pose
        if (!IsAuthority() || !IsOwnedLocally())
        {
            AnimateTowardModel((State)model.state);
            return;
        }

        // Authority state machine
        switch (state)
        {
            case State.Hidden:
                break;

            case State.InitialUp:
                // keep model fresh
                Publish();
                break;

            case State.PoppingUp:
                MoveTowards(visibleLocalPos, () =>
                {
                    col.enabled = true;
                    timer = 0f;
                    state = State.Holding;
                    Publish();
                });
                break;

            case State.Holding:
                timer += Time.deltaTime;
                if (timer >= currentHoldDuration)
                {
                    state = State.GoingDown;
                    Publish();
                }
                break;

            case State.GoingDown:
                MoveTowards(hiddenLocalPos, () =>
                {
                    col.enabled = false;
                    timer = 0f;
                    state = State.Hidden;
                    Publish();
                    manager?.OnMoleMissed(this);
                });
                break;
        }

        ThrottledStatus("Update()");
    }

    private void AnimateTowardModel(State targetState)
    {
        Vector3 targetPos = (targetState == State.Hidden) ? hiddenLocalPos : visibleLocalPos;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    private void MoveTowards(Vector3 target, System.Action onArrive)
    {
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            moveSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.localPosition, target) < 0.0001f)
        {
            transform.localPosition = target;
            onArrive?.Invoke();
        }
    }

    public bool HasModel() => model != null;

    private void ThrottledStatus(string tag)
    {
        if (!verboseLogs) return;
        if (Time.time < _nextLogTime) return;
        _nextLogTime = Time.time + logEverySeconds;

        var r = FindObjectOfType<Realtime>();
        int cid = r != null ? r.clientID : -99;
        bool auth = IsAuthority();
        bool owned = IsOwnedLocally();

        int mState = model != null ? model.state : -1;
        bool mUp = model != null && model.isUp;

        Debug.Log($"[MoleController:{name}] {tag} cid={cid} auth={auth} owned={owned} localState={(int)state} modelState={mState} modelUp={mUp} col={col.enabled}");
    }
}
