using UnityEngine;
using Normal.Realtime;
using UnityEngine.XR;

[RequireComponent(typeof(Collider))]
public class HammerHit : MonoBehaviour {

    public bool isLeftHand = false; 
    public GameObject particle;
    public AudioClip hitSE;

    private AudioSource audioSource;
    private RealtimeView avatarView;
    private WhackAMoleTaskManager manager;

    private int playerId = -1;

    private void Start() {
        GetComponent<Collider>().isTrigger = true;

        manager = FindObjectOfType<WhackAMoleTaskManager>();
        avatarView = GetComponentInParent<RealtimeView>();

        audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void ResolvePlayer() {
        if (playerId >= 0) return;
        if (avatarView == null) return;

        int owner = avatarView.ownerIDInHierarchy;

        if (RoleManager.Instance.IsTeacher(owner)) playerId = 0;
        else if (RoleManager.Instance.IsStudent(owner)) playerId = 1;
    }

    private void OnTriggerEnter(Collider other) {

        MoleController mole =
            other.GetComponent<MoleController>() ??
            other.GetComponentInParent<MoleController>();

        if (mole == null) return;

        ResolvePlayer();
        if (playerId < 0) return;

        // LOCAL VFX + SOUND
        if (particle != null)
            Instantiate(particle, mole.transform.position, Quaternion.identity);

        if (hitSE != null)
            audioSource.PlayOneShot(hitSE);

        // LOCAL HAPTIC ON CORRECT HAND
        if (avatarView.isOwnedLocallySelf)
            SendHaptics(isLeftHand);

        // HOST processes game logic
        if (manager.IsHost())
            manager.OnMoleHit(mole, playerId);
    }

    private void SendHaptics(bool left) {

        XRNode node = left ? XRNode.LeftHand : XRNode.RightHand;

        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
            device.SendHapticImpulse(0, 0.75f, 0.08f);
    }
}
