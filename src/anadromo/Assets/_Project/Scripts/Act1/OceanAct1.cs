using System.Collections;
using Anadromo.Mechanics;
using Anadromo.Systems;
using OceanViz3;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace Anadromo.Act1
{
    // OceanViz owns habitats, rendering, species and ECS; this component owns the player/tutorial.
    public sealed class OceanAct1 : MonoBehaviour
    {
        public enum TutorialPhase { Loading, Arrival, Swimming, Feeding, Complete }
        public TutorialPhase Phase { get; private set; }
        public int Meals { get; private set; }
        [SerializeField] private string schoolSpecies = "Sea Bass";
        [SerializeField] private int schoolPopulation = 40;
        [SerializeField] private float strokeGain = 1.8f;
        [SerializeField] private float maximumSpeed = 4f;
        [SerializeField] private float arrivalSeconds = 4f;
        private MainScene ocean;
        private Transform player, head, leftFin, rightFin, guide;
        private CharacterController controller;
        private EnergySystem energy;
        private ColorAdjustments hunger;
        private Volume hungerVolume;
        private AudioSource heartbeat;
        private AudioClip heartbeatClip;
        private Vector3 velocity, lastLeft, lastRight, feedCenter;
        private bool trackingWasValid, diagnostic;
        private float phaseTime, yaw, pitch, nextBeat;
        private Material foodMaterial;
        private EntityQuery sceneData;
        private bool hasSceneQuery;

        private IEnumerator Start()
        {
            Phase = TutorialPhase.Loading;
            float deadline = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < deadline)
            {
                ocean = FindFirstObjectByType<MainScene>();
                if (ocean != null && MainScene.IsReady && LocationScript.IsReady &&
                    ocean.currentLocationScript != null && ocean.simulationModeManager.views.Count > 0) break;
                yield return null;
            }
            if (ocean == null || !MainScene.IsReady || !LocationScript.IsReady)
            {
                Debug.LogError("[Anadromo] OceanViz did not initialize within 120 seconds.");
                enabled = false;
                yield break;
            }
            ocean.simulationModeManager.SetHudless(true);
            ocean.enabled = false; // Keep sandbox keyboard/menu commands out of the narrative session.
            var network = FindFirstObjectByType<NetworkClient>();
            if (network != null) network.enabled = false;
            CreatePlayer();
            ocean.simulationAPI.SpawnEntityGroup(schoolSpecies, "Act1 School");
            ocean.simulationAPI.SetDynamicEntityGroupPopulation("Act1 School", schoolPopulation);
            ocean.simulationAPI.SpawnEntityGroup("European Anchovy", "Act1 Food Models");
            ocean.simulationAPI.SetDynamicEntityGroupPopulation("Act1 Food Models", 0);
            ocean.simulationAPI.SpawnEntityGroup("Posidonia Oceanica", "Act1 Seagrass");
            ocean.simulationAPI.SetEntityGroupPopulation("Act1 Seagrass", 0.05f);
            Phase = TutorialPhase.Arrival;
            phaseTime = 0f;
            Debug.Log("[Anadromo] Act I ready. Desktop: RMB look, WASD swim, Q/E vertical, Shift sprint. F8 diagnostics.");
            yield return CreateFoodAndGuide();
        }

        private void CreatePlayer()
        {
            var cam = ocean.mainCamera.GetComponent<Camera>();
            var oldRig = ocean.simulationModeManager.cameraRig;
            var oldMotor = oldRig.GetComponent<SimulationModeCameraRig>();
            if (oldMotor != null) oldMotor.enabled = false;
            var oldCollider = oldRig.GetComponent<CharacterController>();
            if (oldCollider != null) oldCollider.enabled = false;
            player = new GameObject("Anadromo Player").transform;
            player.position = cam.transform.position;
            head = cam.transform;
            var offset = new GameObject("Tracking Space");
            offset.transform.SetParent(player, false);
            head.SetParent(offset.transform, true);
            head.localPosition = Vector3.zero;
            var origin = player.gameObject.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offset;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = 0f;
            yaw = head.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, head.eulerAngles.x);
            cam.stereoTargetEye = StereoTargetEyeMask.Both;
            controller = player.gameObject.AddComponent<CharacterController>();
            controller.radius = 0.25f;
            controller.height = 0.6f;
            controller.stepOffset = 0f;
            energy = player.gameObject.AddComponent<EnergySystem>();
            var mouth = new GameObject("Mouth");
            mouth.transform.SetParent(head, false);
            mouth.transform.localPosition = new Vector3(0, -0.1f, 0.35f);
            var body = mouth.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var feeding = mouth.AddComponent<PlayerFeeding>();
            feeding.OnPreyConsumed.AddListener(OnMeal);
            leftFin = new GameObject("Left Fin Tracking").transform;
            leftFin.SetParent(offset.transform, false);
            rightFin = new GameObject("Right Fin Tracking").transform;
            rightFin.SetParent(offset.transform, false);
            hungerVolume = new GameObject("Act1 Hunger Feedback").AddComponent<Volume>();
            hungerVolume.isGlobal = true;
            hungerVolume.priority = 100;
            hungerVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            hunger = hungerVolume.profile.Add<ColorAdjustments>(true);
            heartbeat = player.gameObject.AddComponent<AudioSource>();
            heartbeat.spatialBlend = 0;
            heartbeat.playOnAwake = false;
            heartbeatClip = AudioClip.Create("Prototype Heartbeat", 11025, 1, 44100, false);
            var samples = new float[11025];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 44100f;
                samples[i] = Mathf.Sin(t * 2 * Mathf.PI * 65) * Mathf.Exp(-t * 24) * 0.3f;
            }
            heartbeatClip.SetData(samples, 0);
            sceneData = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(SceneData));
            hasSceneQuery = true;
        }

        private IEnumerator CreateFoodAndGuide()
        {
            // Reuse OceanViz's already loaded meshes/materials, including its GPU swim animation.
            DynamicEntitiesGroup school = null, food = null;
            float deadline = Time.realtimeSinceStartup + 90;
            while (Time.realtimeSinceStartup < deadline)
            {
                school = ocean.simulationModeManager.dynamicEntitiesGroups.Find(g => g.name == "Act1 School");
                food = ocean.simulationModeManager.dynamicEntitiesGroups.Find(g => g.name == "Act1 Food Models");
                if (school != null && food != null && school.IsReady && food.IsReady &&
                    school.meshes != null && food.meshes != null) break;
                yield return null;
            }
            if (school == null || food == null || !school.IsReady || !food.IsReady ||
                school.meshes == null || food.meshes == null || school.meshes.Length == 0 || food.meshes.Length == 0)
            {
                Debug.LogError("[Anadromo] Tutorial species failed to load; see preceding OceanViz errors.");
                yield break;
            }
            feedCenter = player.position + head.forward * 7f;
            guide = CreateFish("Act1 Guide (Sea Bass placeholder)", school.meshes[0], school.material).transform;
            guide.position = player.position + head.forward * 2;
            foodMaterial = new Material(food.material);
            if (foodMaterial.HasProperty("_BaseColor")) foodMaterial.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f));
            for (int i = 0; i < 8; i++)
            {
                var prey = CreateFish("Act1 Edible Alevin " + i, food.meshes[0], foodMaterial);
                float a = i * Mathf.PI * 0.25f;
                prey.transform.position = feedCenter + new Vector3(Mathf.Cos(a) * 2, Mathf.Sin(a * 2) * 0.5f, Mathf.Sin(a) * 2);
                prey.transform.localScale = Vector3.one * 1.5f;
                prey.AddComponent<SphereCollider>().isTrigger = true;
                prey.AddComponent<Prey>().energyValue = 25;
                var krill = prey.AddComponent<Krill>();
                krill.roamRadius = 0.4f;
                krill.speed = 0.15f;
            }
            Debug.Log("[Anadromo] Guide and eight edible OceanViz alevins spawned.");
        }

        private GameObject CreateFish(string name, Mesh mesh, Material material)
        {
            var fish = new GameObject(name);
            fish.transform.SetParent(transform);
            fish.AddComponent<MeshFilter>().sharedMesh = mesh;
            fish.AddComponent<MeshRenderer>().sharedMaterial = material;
            return fish;
        }

        private void Update()
        {
            if (player == null) return;
            float dt = Time.deltaTime;
            phaseTime += dt;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame) diagnostic = !diagnostic;
            if (XRSettings.isDeviceActive) UpdateXR(dt);
            else UpdateDesktop(dt);
            if (Phase == TutorialPhase.Arrival)
            {
                velocity = head.forward * 0.5f;
                if (phaseTime >= arrivalSeconds)
                {
                    Phase = TutorialPhase.Swimming;
                    energy.SetEnergy(20f);
                    phaseTime = 0;
                }
            }
            controller.center = player.InverseTransformPoint(head.position);
            controller.Move(velocity * dt);
            if (Phase == TutorialPhase.Swimming && guide != null && Vector3.Distance(head.position, feedCenter) < 4)
                Phase = TutorialPhase.Feeding;
            if (guide != null)
            {
                var destination = Phase == TutorialPhase.Arrival ? head.position + head.forward * 2 : feedCenter;
                Vector3 dir = destination - guide.position;
                if (dir.sqrMagnitude > 0.04f)
                    guide.rotation = Quaternion.Slerp(guide.rotation, Quaternion.LookRotation(dir), dt * 2);
                guide.position = Vector3.MoveTowards(guide.position, destination, dt * 0.8f);
            }
            if (hasSceneQuery && !sceneData.IsEmptyIgnoreFilter)
            {
                var data = sceneData.GetSingleton<SceneData>();
                data.CameraPosition = head.position;
                sceneData.SetSingleton(data);
            }
            float level = energy.GetEnergyPercentage();
            hunger.saturation.Override(Mathf.Lerp(-65, 0, level));
            if (level < 0.5f && Time.time >= nextBeat)
            {
                heartbeat.PlayOneShot(heartbeatClip, 1f - level);
                nextBeat = Time.time + Mathf.Lerp(0.45f, 1f, level * 2);
            }
        }

        private void UpdateXR(float dt)
        {
            var hmd = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 hp)) head.localPosition = hp;
            if (hmd.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion hr)) head.localRotation = hr;
            bool valid = left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 lp) &
                         right.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 rp);
            if (valid && trackingWasValid && dt > 0)
            {
                Vector3 localForward = head.localRotation * Vector3.forward;
                float stroke = Mathf.Max(0, -Vector3.Dot((lp - lastLeft) / dt, localForward) - 0.3f) +
                               Mathf.Max(0, -Vector3.Dot((rp - lastRight) / dt, localForward) - 0.3f);
                velocity += head.forward * Mathf.Min(stroke, 6) * strokeGain * dt;
                energy.SetSprinting(stroke > 2);
            }
            else energy.SetSprinting(false);
            lastLeft = lp; lastRight = rp; trackingWasValid = valid;
            leftFin.localPosition = lp; rightFin.localPosition = rp;
            velocity = Vector3.ClampMagnitude(velocity * Mathf.Exp(-1.5f * dt), maximumSpeed);
        }

        private void UpdateDesktop(float dt)
        {
            var k = Keyboard.current; var m = Mouse.current;
            Vector3 move = Vector3.zero;
            if (m != null && m.rightButton.isPressed)
            {
                Vector2 delta = m.delta.ReadValue();
                yaw += delta.x * 0.12f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.12f, -85, 85);
                head.localRotation = Quaternion.Euler(pitch, yaw, 0);
            }
            bool sprint = k != null && k.leftShiftKey.isPressed;
            if (k != null)
            {
                if (k.wKey.isPressed) move += head.forward;
                if (k.sKey.isPressed) move -= head.forward;
                if (k.dKey.isPressed) move += head.right;
                if (k.aKey.isPressed) move -= head.right;
                if (k.eKey.isPressed) move += Vector3.up;
                if (k.qKey.isPressed) move -= Vector3.up;
            }
            energy.SetSprinting(sprint && move.sqrMagnitude > 0);
            velocity = Vector3.MoveTowards(velocity, move.normalized * (sprint ? maximumSpeed : 2f), dt * 4f);
        }

        private void OnMeal()
        {
            Meals++;
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
                if (device.TryGetHapticCapabilities(out var cap) && cap.supportsImpulse)
                    device.SendHapticImpulse(0, 0.2f, 0.12f);
            }
            if (Meals >= 4)
            {
                Phase = TutorialPhase.Complete;
                energy.SetEnergy(100);
                Debug.Log("[Anadromo] Act I feeding tutorial complete.");
            }
        }

        private void OnGUI()
        {
            if (!diagnostic || XRSettings.isDeviceActive) return;
            GUI.Box(new Rect(15, 15, 380, 90), "Anadromo — Act I / " + Phase + "\nMeals: " + Meals +
                "/4\nRMB: look | WASD: swim | Q/E: vertical | Shift: sprint");
        }

        private void OnDestroy()
        {
            if (hungerVolume != null) { Destroy(hungerVolume.profile); Destroy(hungerVolume.gameObject); }
            if (heartbeatClip != null) Destroy(heartbeatClip);
            if (foodMaterial != null) Destroy(foodMaterial);
            if (hasSceneQuery && World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
                sceneData.Dispose();
            if (player != null) Destroy(player.gameObject);
        }
    }
}
