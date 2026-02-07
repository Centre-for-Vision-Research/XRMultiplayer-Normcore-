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
    public bool verboseLogs = false;

    // cached poses
    private Vector3 visibleLocalPos;
    private Vector3 hiddenLocalPos;

    private float currentHoldDuration;
    private float timer;

    private Collider col;
    private WhackAMoleTaskManager manager;

    private enum State
    {
        Hidden = 0,
        PoppingUp = 1,
        Holding = 2,
        GoingDown = 3,
        InitialUp = 4
    }

    private State state = State.Hidden;

    public bool IsUp =>
        state == State.PoppingUp ||
        state == State.Holding ||
        state == State.InitialUp;

    private void Awake()
    {
        col = GetComponent<Collider>();
        manager = FindObjectOfType<WhackAMoleTaskManager>();

        visibleLocalPos = transform.localPosition;
        hiddenLocalPos = visibleLocalPos + new Vector3(0f, 0f, -popDistance);

        // Safe default
        state = State.Hidden;
        ApplyVisualImmediate();
    }

    private void Start()
    {
        // DO NOT publish or force anything here
        // Just stay hidden until authority tells us otherwise
        ApplyVisualImmediate();
    }

    protected override void OnRealtimeModelReplaced(MoleModel prev, MoleModel curr)
    {
        if (prev != null)
        {
            prev.stateDidChange -= OnModelStateChanged;
        }

        if (curr != null)
        {
            curr.stateDidChange += OnModelStateChanged;

            // Snap to model immediately (late join safe)
            state = (State)curr.state;
            ApplyVisualImmediate();
        }
    }

    private void OnDestroy()
    {
        if (model != null)
            model.stateDidChange -= OnModelStateChanged;
    }

    private void OnModelStateChanged(MoleModel m, int newState)
    {
        state = (State)newState;
        // visuals will interpolate in Update
    }

    // =========================================================
    // AUTHORITY CHECKS
    // =========================================================

    private bool IsAuthority()
    {
        return WhackAMoleTaskManager.Instance != null &&
               WhackAMoleTaskManager.Instance.IsHost();
    }

    private bool IsOwnedLocally()
    {
        var view = GetComponent<RealtimeView>();
        return view != null && view.isOwnedLocallySelf;
    }

    private void PublishState()
    {
        if (!IsAuthority()) return;
        if (!IsOwnedLocally()) return;
        if (model == null) return;

        model.state = (int)state;
    }

    // =========================================================
    // COLLIDER IS DERIVED FROM STATE ONLY (IMPORTANT)
    // =========================================================

    private void RefreshCollider()
    {
        col.enabled = IsUp;
    }

    // =========================================================
    // VISUALS
    // =========================================================

    private void ApplyVisualImmediate()
    {
        transform.localPosition =
            (state == State.Hidden) ? hiddenLocalPos : visibleLocalPos;

        RefreshCollider();
    }

    private void AnimateToward(State target)
    {
        Vector3 targetPos =
            (target == State.Hidden) ? hiddenLocalPos : visibleLocalPos;

        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetPos,
            moveSpeed * Time.deltaTime
        );

        RefreshCollider();
    }

    // =========================================================
    // PUBLIC API CALLED BY TASK MANAGER / SPAWNER
    // =========================================================

    public void HideImmediate_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return;

        state = State.Hidden;
        timer = 0f;
        ApplyVisualImmediate();
        PublishState();
    }

    public void ShowImmediate_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return;

        state = State.InitialUp;
        timer = 0f;
        ApplyVisualImmediate();
        PublishState();
    }

    public void Pop_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return;
        if (state != State.Hidden) return;

        currentHoldDuration = Random.Range(holdMin, holdMax);
        timer = 0f;
        state = State.PoppingUp;

        PublishState();
    }

    /// <summary>
    /// Called by ANY client hit.
    /// Authority validates and resolves.
    /// </summary>
    public bool Hit_Authority()
    {
        if (!IsAuthority() || !IsOwnedLocally() || model == null) return false;
        if (!IsUp) return false;

        HideImmediate_Authority();
        return true;
    }

    // =========================================================
    // UPDATE LOOP
    // =========================================================

    private void Update()
    {
        if (model == null) return;

        // Non-authority just animates toward model state
        if (!IsAuthority() || !IsOwnedLocally())
        {
            AnimateToward((State)model.state);
            return;
        }

        // Authority state machine
        switch (state)
        {
            case State.Hidden:
                break;

            case State.InitialUp:
                // stays until first hit
                break;

            case State.PoppingUp:
                AnimateToward(State.PoppingUp);
                if (Vector3.Distance(transform.localPosition, visibleLocalPos) < 0.0001f)
                {
                    state = State.Holding;
                    timer = 0f;
                    PublishState();
                }
                break;

            case State.Holding:
                timer += Time.deltaTime;
                if (timer >= currentHoldDuration)
                {
                    state = State.GoingDown;
                    PublishState();
                }
                break;

            case State.GoingDown:
                AnimateToward(State.Hidden);
                if (Vector3.Distance(transform.localPosition, hiddenLocalPos) < 0.0001f)
                {
                    state = State.Hidden;
                    PublishState();
                    manager?.OnMoleMissed(this);
                }
                break;
        }
    }

    // =========================================================
    public bool HasModel() => model != null;
}
