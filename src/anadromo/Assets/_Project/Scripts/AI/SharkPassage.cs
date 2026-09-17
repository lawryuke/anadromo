using UnityEngine;

namespace Anadromo.AI
{
    public class SharkPassage : MonoBehaviour
    {
        public Vector3 startPoint = new Vector3(119.07f, 7.93f, 102.76f);
        public Vector3 endPoint = new Vector3(119.07f, 86.8f, 102.76f);
        [Min(0.01f)] public float speed = 12f;
        [Min(0f)] public float secondGroupDelay = 2f;
        [Min(0f)] public float spacing = 3f;
        public bool waitForFlock;
        public FlockManager flock;
        public Transform[] sharks = new Transform[5];

        private float elapsed;
        private bool started;

        private void Start()
        {
            foreach (var shark in sharks)
                if (shark != null) shark.gameObject.SetActive(false);
            if (!waitForFlock) BeginPassage();
        }

        public void BeginPassage()
        {
            if (started) return;
            started = true;
            elapsed = 0f;
            ApplyTime(elapsed);
        }

        private void Update()
        {
            if (!started)
            {
                if (waitForFlock && flock != null && flock.isGameStarted) BeginPassage();
                return;
            }
            elapsed += Time.deltaTime;
            ApplyTime(elapsed);
        }

        // Absolute time keeps both waves synchronized even when a frame crosses the delay.
        private void ApplyTime(float time)
        {
            Vector3 direction = endPoint - startPoint;
            float distance = direction.magnitude;
            for (int i = 0; i < sharks.Length; i++)
            {
                var shark = sharks[i];
                if (shark == null) continue;
                float age = time - (i < 2 ? 0f : secondGroupDelay);
                shark.gameObject.SetActive(age >= 0f);
                float progress = distance > 0f ? Mathf.Clamp01(Mathf.Max(0f, age) * speed / distance) : 1f;
                float lane = i < 2 ? i - 0.5f : i - 3f;
                // Fan out in transit; every capsule starts and ends at the requested coordinates.
                Vector3 separation = Vector3.right * (lane * spacing * Mathf.Sin(progress * Mathf.PI));
                shark.position = Vector3.Lerp(startPoint, endPoint, progress) + separation;
                if (distance > 0f) shark.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            }
        }
    }
}
