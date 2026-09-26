using System.Collections;
using Anadromo.Config;
using Anadromo.Logic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VFX;

namespace Anadromo.Mechanics
{
    [DefaultExecutionOrder(-150)]
    public sealed class DiegeticSwimIntro : MonoBehaviour
    {
        public Transform groupRoot;
        public Transform initialTarget;
        public Rigidbody playerBody;
        public VisualEffect debris;
        public Camera viewer;
        [Min(0.1f)] public float revealDuration = 3;
        [Min(0)] public float warmupSeconds = 2;
        [Min(0.1f)] public float coastDuration = 3;
        public bool Revealed { get; private set; }
        public bool Launched { get; private set; }
        public bool Released { get; private set; }
        public float ForwardSpeed { get; private set; }
        LevelManager manager;
        SwimGroupController[] groups;
        CanvasGroup curtain;
        GameObject overlay;
        float coastElapsed, launchSpeed;
        bool prepared;
        static readonly int IntroVelocity = Shader.PropertyToID("IntroVelocity");
        float SprintSpeed => GameSettings.I ? GameSettings.I.playerSprintSpeed : 1.2f;

        public void BindPlayer(Transform root, Camera camera)
        {
            playerBody = root.GetComponent<Rigidbody>();
            viewer = camera;
            if (overlay != null)
            {
                var canvas = overlay.GetComponent<Canvas>();
                canvas.worldCamera = viewer;
                canvas.planeDistance = viewer.nearClipPlane + .02f;
            }
        }

        void Awake()
        {
            overlay = new GameObject("Fundido de entrada", typeof(Canvas), typeof(CanvasGroup));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewer;
            canvas.planeDistance = viewer != null ? viewer.nearClipPlane + .02f : .1f;
            canvas.sortingOrder = 32000;
            curtain = overlay.GetComponent<CanvasGroup>();
            curtain.alpha = 1;
            curtain.blocksRaycasts = false;
            var panel = new GameObject("Negro", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            panel.GetComponent<Image>().color = Color.black;
            panel.GetComponent<Image>().raycastTarget = false;
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void Prepare(LevelManager owner, SwimGroupController[] salmon)
        {
            if (prepared) return;
            manager = owner;
            groups = salmon;
            if (!groupRoot || !initialTarget || !playerBody || !debris || !viewer ||
                !debris.HasVector3(IntroVelocity))
            {
                Debug.LogError("Inicio diegético: faltan referencias o el parámetro VFX IntroVelocity.", this);
                return; // Remain black/locked rather than silently starting an inconsistent intro.
            }
            prepared = true;
            foreach (var group in groups) group.HoldFacing(initialTarget);
            // The authored group position is never reset to a hard-coded spawn.
            debris.SetVector3(IntroVelocity, Vector3.back * SprintSpeed);
            debris.Reinit();
            debris.Simulate(1f / 30f, (uint)Mathf.CeilToInt(warmupSeconds * 30));
            StartCoroutine(Reveal());
        }

        IEnumerator Reveal()
        {
            // Allow the warmed particle buffer to be rendered while the curtain is opaque.
            yield return new WaitForEndOfFrame();
            float elapsed = 0;
            while (elapsed < revealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                curtain.alpha = 1 - Mathf.SmoothStep(0, 1, elapsed / revealDuration);
                yield return null;
            }
            curtain.alpha = 0;
            Revealed = true;
            if (manager.autoStart || manager.UsesVRPlayer) manager.StartGame();
        }

        public void Launch()
        {
            if (!prepared || Launched) return;
            Launched = true;
            launchSpeed = Mathf.Max(0, SprintSpeed);
            ForwardSpeed = launchSpeed;
            // Same event as the physical launch: existing particles immediately lose velocity.
            debris.SetVector3(IntroVelocity, Vector3.zero);
        }

        public void ReleasePlayer()
        {
            if (!Launched || Released) return;
            Released = true;
            coastElapsed = 0;
            playerBody.AddForce(Vector3.forward * ForwardSpeed, ForceMode.VelocityChange);
        }

        void FixedUpdate()
        {
            if (!prepared) return;
            if (!Launched)
            {
                debris.SetVector3(IntroVelocity, Vector3.back * SprintSpeed);
                return;
            }
            if (!Released)
            {
                Vector3 delta = Vector3.forward * ForwardSpeed * Time.fixedDeltaTime;
                groupRoot.position += delta;
                foreach (var group in groups) group.TranslateIntroFrame(delta);
                // A desktop player can already be carried by groupRoot; XR usually is a separate root.
                if (!playerBody.transform.IsChildOf(groupRoot))
                    playerBody.MovePosition(playerBody.position + delta);
            }
            else
            {
                coastElapsed += Time.fixedDeltaTime;
                ForwardSpeed = launchSpeed * (1 - Mathf.SmoothStep(0, 1, coastElapsed / coastDuration));
                Vector3 drift = Vector3.forward * ForwardSpeed;
                float forwardSpeed = Vector3.Dot(playerBody.linearVelocity, Vector3.forward);
                if (forwardSpeed < ForwardSpeed)
                    playerBody.AddForce(Vector3.forward * (ForwardSpeed - forwardSpeed), ForceMode.VelocityChange);
                foreach (var group in groups) group.externalVelocity = drift;
            }
        }

        void OnDisable()
        {
            if (groups != null) foreach (var group in groups) if (group != null) group.externalVelocity = Vector3.zero;
            if (debris != null && debris.HasVector3(IntroVelocity)) debris.SetVector3(IntroVelocity, Vector3.zero);
            if (overlay != null) Destroy(overlay);
        }
    }
}
