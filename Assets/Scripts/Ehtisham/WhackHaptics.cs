using UnityEngine;
using UnityEngine.XR;

public static class WhackHaptics
{
    public static void PulseBothHands(float amplitude = 0.6f, float duration = 0.06f)
    {
        Pulse(XRNode.LeftHand, amplitude, duration);
        Pulse(XRNode.RightHand, amplitude, duration);
    }

    private static void Pulse(XRNode node, float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
        {
            device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
        }
    }
}
