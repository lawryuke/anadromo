using UnityEngine;
using UnityEngine.XR;
using Anadromo.Config;

namespace Anadromo.Locomotion
{
    public static class QuestSwimInput
    {
        public static Vector3 ReadVelocity(Transform head)
        {
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return Vector3.zero;
            float magnitude = Mathf.Min(stick.magnitude, 1f);
            if (magnitude <= 0.15f) return Vector3.zero;
            stick = stick.normalized * ((magnitude - 0.15f) / 0.85f);
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            bool sprint = left.TryGetFeatureValue(CommonUsages.primaryButton, out bool pressed) && pressed;
            var settings = GameSettings.I;
            float speed = sprint ? (settings ? settings.playerSprintSpeed : 1.2f)
                                 : (settings ? settings.playerSpeed : 0.7f);
            return (head.forward * stick.y + head.right * stick.x) * speed;
        }

        public static float ReadTurn()
        {
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (!left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 stick)) return 0f;
            float magnitude = Mathf.Min(Mathf.Abs(stick.x), 1f);
            return magnitude <= 0.2f ? 0f : Mathf.Sign(stick.x) * (magnitude - 0.2f) / 0.8f;
        }

        public static void TurnAroundHead(Transform root, Transform head, Rigidbody body, float angle)
        {
            if (Mathf.Approximately(angle, 0f)) return;
            Quaternion turn = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 offset = head.position - root.position;
            body.MovePosition(body.position + offset - turn * offset);
            body.MoveRotation(turn * body.rotation);
        }
    }
}
