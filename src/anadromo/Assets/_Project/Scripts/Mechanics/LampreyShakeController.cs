using System.Collections.Generic;
using Anadromo.AI;
using Anadromo.Locomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;
using XRUsages = UnityEngine.XR.CommonUsages;

namespace Anadromo.Mechanics
{
    [DisallowMultipleComponent]
    public sealed class LampreyShakeController : MonoBehaviour
    {
        [Header("Sacudida de manos")]
        [Min(1f)] public float escapeSeconds = 6f;
        [Min(1f)] public float gripPerShake = 25f;
        [Min(.02f)] public float minimumTravel = .08f;
        [Min(.1f)] public float minimumSpeed = .35f;
        [Min(.15f)] public float reversalWindow = .65f;
        [Header("Balanceo visual")]
        [Range(0f, 5f)] public float rollDegrees = 2.5f;
        [Range(.1f, 1f)] public float rollFrequency = .45f;
        public bool IsStruggling => target && target.Alive && target.AttachedLampreyCount > 0;
        public float RemainingSeconds => Mathf.Max(0, escapeSeconds - elapsed);
        public bool MovementLocked => locked;

        PiranhaPlayerTarget target;
        Rigidbody body;
        Camera viewer;
        FlapDetector flap;
        readonly List<Behaviour> movement = new List<Behaviour>();
        readonly List<bool> movementEnabled = new List<bool>();
        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        readonly ShakeStrokeDetector left = new ShakeStrokeDetector(), right = new ShakeStrokeDetector();
        XRHandSubsystem hands;
        bool locked, wasKinematic, flapWasSuppressed, buttonWasDown, initialized, timedOut;
        float elapsed, nextShake, deadAt, restartHold, lastSideAt;
        int lastSide;
        Transform rollRoot;
        Vector3 rollPosition;
        Quaternion rollRotation;
        bool ownsRollRoot;
        GameObject overlay;
        Text message;
        Image background;

        void Awake() { target = GetComponent<PiranhaPlayerTarget>(); body = GetComponent<Rigidbody>(); }
        void Start()
        {
            viewer = GetComponentInChildren<Camera>();
            var swim = GetComponent<FlapSwimController>();
            flap = swim ? swim.Detector : null;
            AddMovement(swim);
            AddMovement(GetComponent<SimpleFlyCamera>());
            AddMovement(GetComponent<LocomotionModeManager>());
            if (viewer)
            {
                var origin = GetComponent<XROrigin>();
                if (origin && origin.CameraFloorOffsetObject)
                    rollRoot = origin.CameraFloorOffsetObject.transform;
                else
                {
                    rollRoot = new GameObject("Lamprey view roll").transform;
                    rollRoot.SetParent(viewer.transform.parent, false);
                    viewer.transform.SetParent(rollRoot, false);
                    ownsRollRoot = true;
                }
                CreateOverlay();
            }
            initialized = true;
            if (target && !target.Alive) HandleDeath();
            else if (target) AttachChanged();
        }

        void AddMovement(Behaviour component) { if (component) movement.Add(component); }

        public void AttachChanged()
        {
            if (!target) target = GetComponent<PiranhaPlayerTarget>();
            if (!initialized) return;
            if (target.AttachedLampreyCount > 0)
            {
                if (!locked) { elapsed = 0; timedOut = false; ResetInput(); LockMovement(); }
            }
            else if (target.Alive) UnlockMovement();
        }

        void LockMovement()
        {
            if (locked || !initialized) return;
            locked = true;
            movementEnabled.Clear();
            foreach (var component in movement)
            { movementEnabled.Add(component.enabled); component.enabled = false; }
            if (body)
            {
                wasKinematic = body.isKinematic;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                body.isKinematic = true;
            }
            if (flap) { flapWasSuppressed = flap.SuppressFlaps; flap.SuppressFlaps = true; }
            if (rollRoot) { rollPosition = rollRoot.localPosition; rollRotation = rollRoot.localRotation; }
            if (target) target.Vital.Energy.SetSprinting(false);
        }

        void RestoreRoll()
        {
            if (rollRoot && locked) { rollRoot.localPosition = rollPosition; rollRoot.localRotation = rollRotation; }
        }

        void UnlockMovement()
        {
            if (!locked) return;
            RestoreRoll();
            if (body) body.isKinematic = wasKinematic;
            if (flap) { flap.SuppressFlaps = flapWasSuppressed; flap.ResetState(); }
            for (int i = 0; i < movementEnabled.Count; i++)
                if (movement[i]) movement[i].enabled = movementEnabled[i];
            var desktop = GetComponent<SimpleFlyCamera>();
            if (desktop) desktop.SyncLookRotation();
            locked = false; elapsed = 0; ResetInput();
            if (overlay) overlay.SetActive(false);
        }

        public void HandleDeath()
        {
            LockMovement(); RestoreRoll(); ResetInput();
            deadAt = Time.unscaledTime; restartHold = 0;
            RefreshOverlay();
        }

        public void ResetEncounter() { UnlockMovement(); restartHold = 0; timedOut = false; }

        void ResetInput() { left.Reset(); right.Reset(); lastSide = 0; lastSideAt = -10; nextShake = 0; }

        void Update()
        {
            if (!target) return;
            bool button = PrimaryButton(XRNode.LeftHand) || PrimaryButton(XRNode.RightHand);
            if (!target.Alive)
            {
                bool wrists = ReadWrist(true, out Vector3 l) && ReadWrist(false, out Vector3 r) &&
                    Vector3.Distance(l, r) < .15f;
                restartHold = wrists ? restartHold + Time.unscaledDeltaTime : 0;
                if (Time.unscaledTime - deadAt > .75f && (restartHold >= 2f ||
                    (button && !buttonWasDown) || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)))
                    target.ResetEncounter();
            }
            else if (IsStruggling)
            {
                if (!locked) AttachChanged();
                elapsed += Time.deltaTime;
                if (elapsed >= escapeSeconds) { timedOut = true; target.TakeDamage(target.maxHealth); }
                else
                {
                    bool stroke = SampleHand(true, left) | SampleHand(false, right);
                    if (!XRSettings.isDeviceActive) stroke |= DesktopStroke();
                    if (stroke && Time.time >= nextShake)
                    { nextShake = Time.time + .12f; target.ShakeLampreys(gripPerShake); }
                }
            }
            buttonWasDown = button;
            RefreshOverlay();
        }

        bool SampleHand(bool isLeft, ShakeStrokeDetector detector)
        {
            if (!ReadWrist(isLeft, out Vector3 point)) { detector.Reset(); return false; }
            return detector.Sample(point, Time.deltaTime, minimumTravel, minimumSpeed, reversalWindow);
        }

        bool ReadWrist(bool isLeft, out Vector3 point)
        {
            point = Vector3.zero;
            if (hands == null || !hands.running)
            {
                hands = null; SubsystemManager.GetSubsystems(subsystems);
                foreach (var subsystem in subsystems) if (subsystem.running) { hands = subsystem; break; }
            }
            if (hands != null)
            {
                var hand = isLeft ? hands.leftHand : hands.rightHand;
                if (hand.isTracked && hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose pose))
                { point = pose.position; return true; }
                return false;
            }
            var device = InputDevices.GetDeviceAtXRNode(isLeft ? XRNode.LeftHand : XRNode.RightHand);
            return device.TryGetFeatureValue(XRUsages.isTracked, out bool tracked) && tracked &&
                device.TryGetFeatureValue(XRUsages.devicePosition, out point);
        }

        static bool PrimaryButton(XRNode node) => InputDevices.GetDeviceAtXRNode(node)
            .TryGetFeatureValue(XRUsages.primaryButton, out bool pressed) && pressed;

        bool DesktopStroke()
        {
            var keyboard = Keyboard.current;
            int side = keyboard != null && keyboard.aKey.wasPressedThisFrame ? -1 :
                keyboard != null && keyboard.dKey.wasPressedThisFrame ? 1 : 0;
            if (side == 0 && Mouse.current != null)
            {
                float x = Mouse.current.delta.ReadValue().x;
                if (Mathf.Abs(x) > 25) side = x < 0 ? -1 : 1;
            }
            if (side == 0) return false;
            bool reversed = lastSide != 0 && side != lastSide && Time.time - lastSideAt <= reversalWindow;
            lastSide = side; lastSideAt = Time.time;
            return reversed;
        }

        void LateUpdate()
        {
            if (!locked) return;
            foreach (var component in movement) if (component) component.enabled = false;
            RestoreRoll();
            if (!IsStruggling || !rollRoot || !viewer) return;
            // Roll the tracking space about the eyes; leave the tracked head pose intact.
            Vector3 pivot = viewer.transform.position;
            float fade = Mathf.Clamp01(elapsed / .5f);
            rollRoot.RotateAround(pivot, viewer.transform.forward,
                Mathf.Sin(elapsed * rollFrequency * Mathf.PI * 2) * rollDegrees * fade);
        }

        void CreateOverlay()
        {
            overlay = new GameObject("Lamprey encounter HUD", typeof(RectTransform), typeof(Canvas));
            overlay.transform.SetParent(viewer.transform, false);
            overlay.transform.localPosition = new Vector3(0, 0, .7f);
            overlay.transform.localScale = Vector3.one * .001f;
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = viewer; canvas.sortingOrder = 32000;
            background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            background.transform.SetParent(overlay.transform, false);
            background.rectTransform.sizeDelta = new Vector2(3000, 3000); background.raycastTarget = false;
            message = new GameObject("Message", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            message.transform.SetParent(overlay.transform, false);
            message.rectTransform.sizeDelta = new Vector2(620, 320);
            message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            message.fontSize = 28; message.alignment = TextAnchor.MiddleCenter; message.raycastTarget = false;
            overlay.SetActive(false);
        }

        void RefreshOverlay()
        {
            if (!overlay || !target) return;
            overlay.SetActive(IsStruggling || !target.Alive);
            if (!overlay.activeSelf) return;
            background.color = new Color(.025f, .015f, .04f, target.Alive ? 0 : .96f);
            message.rectTransform.localPosition = target.Alive ? new Vector3(0, -170, 0) : Vector3.zero;
            if (target.Alive)
                message.text = "¡SACÚDETE!\n" + (XRSettings.isDeviceActive ?
                    "Sacude las manos de un lado a otro" : "Alterna A / D o sacude el ratón") +
                    "\n" + RemainingSeconds.ToString("0.0") + " s  ·  " +
                    Mathf.RoundToInt(target.ShakeProgress * 100) + "% liberado";
            else message.text = "HAS PERDIDO\n" + (timedOut ? "No te liberaste a tiempo" : "La energía se agotó") + "\n\n" +
                (XRSettings.isDeviceActive ? "Junta las manos 2 s para reintentar\nTambién puedes pulsar A / X" : "Pulsa R para reintentar");
        }

        void OnDisable()
        {
            if (!target || target.Alive) UnlockMovement(); else RestoreRoll();
            if (overlay) overlay.SetActive(false);
        }
        void OnDestroy()
        {
            if (overlay) Destroy(overlay);
            if (ownsRollRoot && rollRoot)
            {
                if (viewer) viewer.transform.SetParent(rollRoot.parent, true);
                Destroy(rollRoot.gameObject);
            }
        }
    }
}
