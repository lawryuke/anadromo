using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace Anadromo.UI
{
    [DefaultExecutionOrder(-250)]
    [AddComponentMenu("Anadromo/UI/Opening Slideshow")]
    public sealed class OpeningSlideshow : MonoBehaviour
    {
        [SerializeField] Sprite[] slides = new Sprite[0];
        [SerializeField] AudioClip music;
        [SerializeField] AudioSource[] backgroundMusic = new AudioSource[0];
        [SerializeField, Min(1f)] float secondsPerSlide = 6f;
        [SerializeField, Min(0f)] float fadeSeconds = .35f;
        [SerializeField, Min(1f)] float idleSeconds = 45f;

        [Header("Pantalla de introduccion")]
        [SerializeField, Min(1f)] float screenDistance = 3f;
        [SerializeField, Min(.5f)] float screenWidth = 3.2f;

        public bool IsPlaying { get; private set; }
        public int CompletedFrame { get; private set; } = -1;
        public bool ClickedThisFrame { get; private set; }

        Canvas canvas;
        Image slideImage;
        AudioSource audioSource;
        Camera viewer;
        XRHandSubsystem hands;
        readonly List<XRHandSubsystem> subsystems = new List<XRHandSubsystem>();
        float slideTime;
        float idleTime;
        int slideIndex;
        bool leftPinched, rightPinched;
        Vector3 leftWrist, rightWrist;
        bool leftWristValid, rightWristValid;
        bool presentationEnabled;
        bool placementPending;
        bool waitingForHeadTracking;

        public void Initialize(Camera viewer, bool enableIntro = true)
        {
            this.viewer = viewer;
            presentationEnabled = enableIntro;
            // Keep sampling the start gesture in VR without creating or playing the story.
            if (!presentationEnabled) return;
            if (viewer == null || slides == null || slides.Length == 0 || slides[0] == null)
            {
                Debug.LogWarning("Opening Slideshow necesita una cámara y las imágenes de introducción.", this);
                enabled = false;
                return;
            }

            var root = new GameObject("Opening slideshow", typeof(RectTransform), typeof(Canvas));
            // A single physical screen shared by both eyes, independent of the moving rig.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewer;
            canvas.sortingOrder = 32760;
            var screenRect = (RectTransform)root.transform;
            screenRect.sizeDelta = new Vector2(1600, 900);
            screenRect.localScale = Vector3.one * (Mathf.Max(.5f, screenWidth) / 1600f);
            // Use a layer that the selected player camera actually renders.
            for (int layer = 0; layer < 32; layer++)
                if ((viewer.cullingMask & (1 << layer)) != 0) { root.layer = layer; break; }

            var background = new GameObject("Black background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            background.layer = root.layer;
            Stretch(background.GetComponent<RectTransform>());
            var black = background.GetComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;

            var picture = new GameObject("Story image", typeof(RectTransform), typeof(Image));
            picture.transform.SetParent(root.transform, false);
            picture.layer = root.layer;
            Stretch(picture.GetComponent<RectTransform>());
            slideImage = picture.GetComponent<Image>();
            slideImage.preserveAspect = true;
            slideImage.raycastTarget = false;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0;
            audioSource.clip = music;
            Play();
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void Play()
        {
            foreach (var source in backgroundMusic) if (source != null) source.Pause();
            slideIndex = 0;
            slideTime = 0;
            IsPlaying = true;
            // Defer placement until after the camera's first tracked pose, also on idle replay.
            placementPending = true;
            waitingForHeadTracking = true;
            canvas.enabled = false;
            canvas.gameObject.SetActive(true);
            slideImage.sprite = slides[0];
            slideImage.color = Color.white;
            if (music != null) audioSource.Play();
        }

        void LateUpdate()
        {
            if (!IsPlaying || !canvas || !viewer) return;
            bool tracked = false;
            if (waitingForHeadTracking)
            {
                var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
                tracked = head.isValid && head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool isTracked) && isTracked;
            }
            if (!placementPending && !tracked) return;

            // Keep the screen upright: head pitch and roll must not tilt the presentation.
            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < .001f)
                forward = Vector3.ProjectOnPlane(viewer.transform.up, Vector3.up);
            forward.Normalize();
            float distance = Mathf.Max(1f, screenDistance, viewer.nearClipPlane + .5f);
            canvas.transform.SetPositionAndRotation(viewer.transform.position + forward * distance,
                Quaternion.LookRotation(forward, Vector3.up));
            canvas.enabled = true;
            placementPending = false;
            if (tracked) waitingForHeadTracking = false;
            // No following or billboarding after placement: users can look away naturally.
        }

        void OnDestroy()
        {
            if (canvas) Destroy(canvas.gameObject);
        }

        void Finish()
        {
            IsPlaying = false;
            CompletedFrame = Time.frameCount;
            canvas.gameObject.SetActive(false);
            audioSource.Stop();
            foreach (var source in backgroundMusic) if (source != null) source.UnPause();
            idleTime = 0;
        }

        void Update()
        {
            bool click = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool handActivity = false;
            click |= SampleHand(true, ref leftPinched, ref leftWrist, ref leftWristValid, ref handActivity);
            click |= SampleHand(false, ref rightPinched, ref rightWrist, ref rightWristValid, ref handActivity);
            ClickedThisFrame = click;
            bool activity = click || handActivity || OtherActivity();
            if (IsPlaying)
            {
                if (click) { Finish(); return; }
                slideTime += Time.unscaledDeltaTime;
                if (slideTime >= secondsPerSlide)
                {
                    slideTime = 0;
                    slideIndex++;
                    if (slideIndex >= slides.Length) { Finish(); return; }
                    slideImage.sprite = slides[slideIndex];
                }
                float fade = Mathf.Min(slideTime, secondsPerSlide - slideTime);
                slideImage.color = new Color(1, 1, 1, fadeSeconds <= 0 ? 1 : Mathf.Clamp01(fade / fadeSeconds));
                return;
            }

            var manager = Anadromo.Logic.LevelManager.Instance;
            if (!presentationEnabled || manager == null || !manager.IsReady || manager.currentPhase != Anadromo.Logic.GamePhase.WaitingForStart)
                return;
            idleTime = activity ? 0 : idleTime + Time.unscaledDeltaTime;
            if (idleTime >= idleSeconds) Play();
        }

        bool SampleHand(bool isLeft, ref bool pinched, ref Vector3 wristPosition,
            ref bool wristValid, ref bool activity)
        {
            if (hands == null || !hands.running)
            {
                hands = null;
                SubsystemManager.GetSubsystems(subsystems);
                foreach (var subsystem in subsystems)
                    if (subsystem.running) { hands = subsystem; break; }
            }
            if (hands == null) { pinched = false; wristValid = false; return false; }
            var hand = isLeft ? hands.leftHand : hands.rightHand;
            if (!hand.isTracked ||
                !hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose wrist) ||
                !hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumb) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTip) ||
                !hand.GetJoint(XRHandJointID.MiddleProximal).TryGetPose(out Pose middle) ||
                !hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out Pose index) ||
                !hand.GetJoint(XRHandJointID.LittleProximal).TryGetPose(out Pose little))
            { pinched = false; wristValid = false; return false; }

            if (wristValid && Vector3.Distance(wristPosition, wrist.position) > .015f) activity = true;
            wristPosition = wrist.position;
            wristValid = true;
            Vector3 palm = Vector3.Cross(middle.position - wrist.position,
                index.position - little.position) * (isLeft ? -1f : 1f);
            Transform trackingSpace = viewer.transform.parent;
            Vector3 forward = trackingSpace != null
                ? trackingSpace.InverseTransformDirection(viewer.transform.forward)
                : viewer.transform.forward;
            bool facingForward = palm.sqrMagnitude > .000001f &&
                Vector3.Dot(palm.normalized, forward) > .5f;
            float distance = Vector3.Distance(thumb.position, indexTip.position);
            bool nowPinched = facingForward && distance < (pinched ? .04f : .025f);
            bool clicked = nowPinched && !pinched;
            pinched = nowPinched;
            activity |= nowPinched;
            return clicked;
        }

        static bool OtherActivity()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) return true;
            if (Mouse.current != null && (Mouse.current.delta.ReadValue().sqrMagnitude > 1 ||
                Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame)) return true;
            return false;
        }
    }
}
