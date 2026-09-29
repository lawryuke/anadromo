using Anadromo.AI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anadromo.Locomotion
{
    /// <summary>Connects the tracked swimmer's body, combat and hands-only recovery.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CapsuleCollider), typeof(PiranhaPlayerTarget))]
    public sealed class VRPlayerGameplay : MonoBehaviour
    {
        public Transform head;
        public FlapDetector hands;
        CapsuleCollider hull;
        void Awake()
        {
            hull = GetComponent<CapsuleCollider>();
            if (!head)
            {
                var camera = GetComponentInChildren<Camera>();
                if (camera) head = camera.transform;
            }
            SyncBody();
        }

        void FixedUpdate() => SyncBody();

        public void SyncBody()
        {
            if (head && hull) hull.center = transform.InverseTransformPoint(head.position);
        }

        public void RestartLevel()
        {
            // Reload also resets one-shot traps, prey and all enemy state.
            SceneManager.LoadScene(gameObject.scene.path);
        }

    }
}
