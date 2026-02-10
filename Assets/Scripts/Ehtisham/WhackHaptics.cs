using UnityEngine;
using UnityEngine.XR;

public static class WhackHaptics
{
    public static void PulseBothHands(float amplitude = 0.6f, float duration = 0.06f)
    {
        Pulse(XRNode.LeftHand, amplitude, duration);
        Pulse(XRNode.RightHand, amplitude, duration);
    }

    // MUST be public because MoleHitReceiver calls it
    public static void Pulse(XRNode node, float amplitude = 0.6f, float duration = 0.06f)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
        {
            device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
        }
    }
}
