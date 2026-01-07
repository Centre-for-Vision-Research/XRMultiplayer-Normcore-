using UnityEngine;
using Normal.Realtime;

[RequireComponent(typeof(Collider))]
public class MoleController : RealtimeComponent<MoleModel> {

    [Header("Movement Settings")]
    public float popDistance = 0.03f;
    public float moveSpeed   = 0.05f;
    public float holdMin = 0.25f;
    public float holdMax = 0.50f;

    private float currentHoldDuration;

    private Collider col;
    private WhackAMoleTaskManager manager;
    private Vector3 visibleLocalPos;
    private Vector3 hiddenLocalPos;
    private bool isHost;

    private enum State { Hidden = 0, PoppingUp = 1, Holding = 2, GoingDown = 3, InitialUp = 4 }
    private State state = State.Hidden;

    private float timer = 0f;
    public bool IsUp => state == State.PoppingUp || state == State.Holding || state == State.InitialUp;

    private void Awake() {
        col = GetComponent<Collider>();
        manager = FindObjectOfType<WhackAMoleTaskManager>();
        isHost = manager != null && manager.IsHost();

        visibleLocalPos = transform.localPosition;
        hiddenLocalPos = visibleLocalPos + new Vector3(0f, 0f, -popDistance);
    }

    private void Start() {
        col.enabled = false;
        transform.localPosition = hiddenLocalPos;
        state = State.Hidden;
        UpdateModel();
    }

    protected override void OnRealtimeModelReplaced(MoleModel prev, MoleModel curr) {
        if (prev != null)
            prev.isUpDidChange -= OnIsUpChanged;

        if (curr != null) {
            curr.isUpDidChange += OnIsUpChanged;
            col.enabled = curr.isUp;

            if (!isHost)
                ApplyClientState();
        }
    }

    private void OnIsUpChanged(MoleModel model, bool value) {
        col.enabled = value;
    }

    private void UpdateModel() {
        if (!isHost || model == null) return;

        model.isUp = IsUp;
        model.state = (int)state;
    }

    private void ApplyClientState() {
        if (model == null) return;

        State mState = (State)model.state;

        if (mState == State.Hidden)
            transform.localPosition = hiddenLocalPos;
    }

    // ---- Host control ----
    public void ShowImmediate() {
        if (!isHost) return;

        state = State.InitialUp;
        transform.localPosition = visibleLocalPos;
        col.enabled = true;
        timer = 0f;

        UpdateModel();
    }

    public void HideImmediate() {
        if (!isHost) return;

        transform.localPosition = hiddenLocalPos;
        col.enabled = false;
        state = State.Hidden;
        timer = 0f;

        UpdateModel();
    }

    public void Pop() {
        if (!isHost) return;
        if (state != State.Hidden) return;

        currentHoldDuration = Random.Range(holdMin, holdMax);
        timer = 0f;
        state = State.PoppingUp;

        UpdateModel();
    }

    public bool Hit() {
        if (!isHost) return false;
        if (!IsUp) return false;

        HideImmediate();
        return true;
    }

    private void Update() {
        if (!isHost) return;

        switch (state) {
            case State.InitialUp:
                UpdateModel();
                break;

            case State.PoppingUp:
                MoveTowards(visibleLocalPos, () => {
                    col.enabled = true;
                    timer = 0f;
                    state = State.Holding;
                    UpdateModel();
                });
                break;

            case State.Holding:
                timer += Time.deltaTime;
                if (timer >= currentHoldDuration) {
                    state = State.GoingDown;
                    UpdateModel();
                }
                break;

            case State.GoingDown:
                MoveTowards(hiddenLocalPos, () => {
                    col.enabled = false;
                    timer = 0f;
                    state = State.Hidden;
                    UpdateModel();
                    manager?.OnMoleMissed(this);
                });
                break;
        }
    }

    private void MoveTowards(Vector3 target, System.Action onArrive) {
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            target,
            moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.localPosition, target) < 0.0001f) {
            transform.localPosition = target;
            onArrive?.Invoke();
        }
    }
}
