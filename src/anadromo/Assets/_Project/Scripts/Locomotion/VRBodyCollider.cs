using UnityEngine;
using Unity.XR.CoreUtils;

namespace Anadromo.Locomotion
{
    // A separate solid body: PlayerFeeding owns the sphere trigger used as the mouth.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class VRBodyCollider : MonoBehaviour
    {
        public Transform head;
        CapsuleCollider hull;

        void Awake()
        {
            hull = GetComponent<CapsuleCollider>();
            hull.isTrigger = false;
            var origin = GetComponent<XROrigin>();
            if (!head && origin && origin.Camera) head = origin.Camera.transform;
            SyncBody();
        }

        void FixedUpdate() => SyncBody();

        public void SyncBody()
        {
            if (!hull) hull = GetComponent<CapsuleCollider>();
            if (head && hull) hull.center = transform.InverseTransformPoint(head.position);
        }
    }
}
