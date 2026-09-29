using Anadromo.AI;
using Anadromo.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Anadromo.Locomotion
{
    /// <summary>Connects the tracked swimmer's body, combat and hands-only recovery.</summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CapsuleCollider), typeof(PiranhaPlayerTarget))]
    public sealed class VRPlayerGameplay : MonoBehaviour
    {
        public Transform head;
        public FlapDetector hands;
        public float restartHoldDuration = 1.5f;
        PiranhaPlayerTarget target;
        CapsuleCollider hull;
        Text message;
        float restartHold, shakeCooldown;
        readonly Vector3[] previousHands = new Vector3[2];
        readonly bool[] handValid = new bool[2];

        bool TryReadHand(bool left, out Pose pose)
        {
            if (hands && hands.TryGetTrackedWrist(left, out pose)) return true;
            pose = default;
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(left
                ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand);
            if (!device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) || !tracked ||
                !device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 point)) return false;
            Transform space = head.parent ? head.parent : transform;
            pose.position = space.TransformPoint(point);
            return true;
        }

        void Awake()
        {
            target = GetComponent<PiranhaPlayerTarget>();
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

        void Update()
        {
            if (!head || !target) return;
            bool bothNearHead = true;
            bool shaking = false;
            float dt = Time.deltaTime;
            for (int i = 0; i < 2; i++)
            {
                Pose pose = default;
                bool valid = TryReadHand(i == 0, out pose);
                bothNearHead &= valid && Vector3.Distance(pose.position, head.position) < .45f;
                if (valid)
                {
                    // Remove virtual swimming/turning from the gesture measurement.
                    Vector3 point = transform.InverseTransformPoint(pose.position);
                    float speed = handValid[i] && dt > 0f && dt < .15f
                        ? Vector3.Distance(point, previousHands[i]) / dt : 0f;
                    shaking |= speed > 1.2f && speed < 5f;
                    previousHands[i] = point;
                }
                handValid[i] = valid;
            }
            shakeCooldown = Mathf.Max(0f, shakeCooldown - dt);
            if (target.Alive && shaking && shakeCooldown <= 0f)
            {
                target.ShakeLampreys(25f);
                shakeCooldown = .2f;
            }
            if (target.Alive) restartHold = 0f;
            else restartHold = bothNearHead ? restartHold + dt : 0f;
            if (!target.Alive || target.AttachedLampreyCount > 0)
            {
                if (!message) CreateMessage();
                message.gameObject.SetActive(true);
                message.text = target.Alive
                    ? "Sacude las manos para soltar las lampreas"
                    : "Energía agotada\nAcerca ambas manos al visor durante 1,5 s para reiniciar"
                        + (restartHold > 0f ? "\n" + Mathf.RoundToInt(100f * restartHold / restartHoldDuration) + "%" : "");
            }
            else if (message) message.gameObject.SetActive(false);
            if (!target.Alive && restartHold >= restartHoldDuration) RestartLevel();
        }

        public void RestartLevel()
        {
            // Reload also resets one-shot traps, prey and all enemy state.
            SceneManager.LoadScene(gameObject.scene.path);
        }

        void CreateMessage()
        {
            var canvasObject = new GameObject("VR gameplay messages", typeof(Canvas));
            canvasObject.transform.SetParent(head, false);
            canvasObject.transform.localPosition = new Vector3(0f, -.12f, .8f);
            canvasObject.transform.localScale = Vector3.one * .001f;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = head.GetComponent<Camera>();
            canvas.sortingOrder = 100;
            var textObject = new GameObject("Instructions", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);
            message = textObject.GetComponent<Text>();
            message.rectTransform.sizeDelta = new Vector2(700f, 170f);
            message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            message.fontSize = 30;
            message.alignment = TextAnchor.MiddleCenter;
            message.color = Color.white;
            message.raycastTarget = false;
        }
    }
}
