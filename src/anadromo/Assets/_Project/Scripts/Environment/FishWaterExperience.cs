using System;
using Anadromo.Mechanics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Anadromo.Environment
{
    /// <summary>Scene-owned water perception. Distances and velocities use world metres.</summary>
    public sealed class FishWaterExperience : MonoBehaviour
    {
        [Serializable]
        public struct FlowZone
        {
            public string label;
            public Vector3 center;
            [Min(.1f)] public float radius;
            [Range(0, 1)] public float agitation;
            public Vector3 drift;
            public bool shelter;
        }

        public Camera viewer;
        public Transform surface;
        [Min(1)] public float visibility = 10;
        [Range(0, .5f)] public float maximumDrift = .3f;
        [Range(0, .5f)] public float hapticStrength = .16f;
        public bool haptics = true;
        public Material bubbleMaterial;
        public Material snellMaterial;
        public FlowZone[] zones;
        public float Agitation { get; private set; }
        public Vector3 Drift { get; private set; }

        Volume volume;
        VolumeProfile profile;
        ColorAdjustments colors;
        ChannelMixer channels;
        ParticleSystem bubbles, highlights;
        ParticleSystem.Particle[] preyParticles = new ParticleSystem.Particle[64];
        Prey[] prey = Array.Empty<Prey>();
        GameObject generated;
        float nextPreyScan, nextPulse;
        bool savedFog;
        FogMode savedMode;
        float savedDensity;
        Color savedFogColor, savedBackground;
        AmbientMode savedAmbientMode;
        Color savedSky, savedEquator, savedGround;
        UniversalAdditionalCameraData cameraData;
        bool savedOpaque;

        void OnEnable()
        {
            if (!viewer || !surface) { enabled = false; return; }
            savedFog = RenderSettings.fog;
            savedMode = RenderSettings.fogMode;
            savedDensity = RenderSettings.fogDensity;
            savedFogColor = RenderSettings.fogColor;
            savedBackground = viewer.backgroundColor;
            savedAmbientMode = RenderSettings.ambientMode;
            savedSky = RenderSettings.ambientSkyColor;
            savedEquator = RenderSettings.ambientEquatorColor;
            savedGround = RenderSettings.ambientGroundColor;
            cameraData = viewer.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData) { savedOpaque = cameraData.requiresColorTexture; cameraData.requiresColorTexture = true; }

            generated = new GameObject("Fish perception (runtime)");
            generated.transform.SetParent(transform, false);
            volume = generated.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            colors = profile.Add<ColorAdjustments>(true);
            channels = profile.Add<ChannelMixer>(true);
            bubbles = CreateParticles("Microbubbles", 180, true);
            highlights = CreateParticles("Frontal prey glints", 64, false);

            if (snellMaterial)
            {
                var window = GameObject.CreatePrimitive(PrimitiveType.Plane);
                window.name = "Snell window - surface underside";
                window.transform.SetParent(generated.transform, false);
                window.transform.position = new Vector3(surface.position.x, surface.position.y - .08f, surface.position.z);
                window.transform.localScale = new Vector3(60, 1, 60);
                Destroy(window.GetComponent<Collider>());
                var renderer = window.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = snellMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        ParticleSystem CreateParticles(string label, int count, bool emit)
        {
            var go = new GameObject(label);
            go.transform.SetParent(generated.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 2.2f;
            main.startSpeed = .15f;
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .055f);
            main.startColor = new Color(.55f, .9f, 1, .28f);
            var emission = ps.emission;
            emission.enabled = emit;
            emission.rateOverTime = 0;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 2.8f;
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = emit;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = .22f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = bubbleMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            ps.Play();
            return ps;
        }

        public Vector3 SampleFlow(Vector3 point, float time, out float agitation)
        {
            Vector3 drift = Vector3.zero;
            agitation = 0;
            float shelter = 0;
            if (zones != null) foreach (var zone in zones)
            {
                float weight = Mathf.SmoothStep(0, 1, 1 - Vector3.Distance(point, zone.center) / Mathf.Max(.1f, zone.radius));
                if (zone.shelter) { shelter = Mathf.Max(shelter, weight); continue; }
                agitation += zone.agitation * weight;
                drift += zone.drift * weight;
            }
            agitation = Mathf.Clamp01(agitation) * (1 - shelter);
            float3 p = (float3)point * .35f + new float3(time * .35f);
            var eddies = new Vector3(noise.snoise(p), noise.snoise(p + 29.4f) * .35f, noise.snoise(p + 71.8f));
            return Vector3.ClampMagnitude((drift + eddies * agitation * .22f) * (1 - shelter), maximumDrift);
        }

        void Update()
        {
            if (!viewer || !surface) return;
            float depth = Mathf.Max(0, surface.position.y - viewer.transform.position.y);
            float submerged = Mathf.SmoothStep(0, 1, depth / .6f);
            Vector3 target = SampleFlow(viewer.transform.position, Time.time, out float activity);
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 2);
            Drift = Vector3.Lerp(Drift, target * submerged, blend);
            Agitation = Mathf.Lerp(Agitation, activity * submerged, blend);
            ApplyVision(depth, submerged);
            bubbles.transform.position = viewer.transform.position + viewer.transform.forward * 1.5f;
            var emission = bubbles.emission;
            emission.rateOverTime = 65 * Agitation;
            UpdatePrey(submerged);
            if (haptics && Agitation > .03f && Time.time >= nextPulse)
            {
                float rhythm = .5f + .5f * Mathf.PerlinNoise(Time.time * 3.1f, 17);
                Pulse(XRNode.LeftHand, Agitation * hapticStrength * rhythm);
                Pulse(XRNode.RightHand, Agitation * hapticStrength * (1 - rhythm * .4f));
                nextPulse = Time.time + Mathf.Lerp(.24f, .09f, rhythm);
            }
        }

        void ApplyVision(float depth, float submerged)
        {
            float deep = 1 - Mathf.Exp(-depth / 10);
            Color fog = Color.Lerp(new Color(.07f, .27f, .29f), new Color(.018f, .085f, .12f), deep);
            RenderSettings.fog = submerged > 0 || savedFog;
            RenderSettings.fogMode = submerged > 0 ? FogMode.ExponentialSquared : savedMode;
            // 10% scene contrast at the nominal range, continuous fade beyond it.
            float density = Mathf.Sqrt(-Mathf.Log(.1f)) / Mathf.Max(1, visibility);
            RenderSettings.fogDensity = Mathf.Lerp(savedDensity, density * (1 + Agitation * .2f), submerged);
            RenderSettings.fogColor = Color.Lerp(savedFogColor, fog, submerged);
            viewer.backgroundColor = Color.Lerp(savedBackground, fog, submerged);
            RenderSettings.ambientMode = submerged > 0 ? AmbientMode.Trilight : savedAmbientMode;
            RenderSettings.ambientSkyColor = Color.Lerp(savedSky, new Color(.15f, .36f, .4f) * Mathf.Lerp(1, .55f, deep), submerged);
            RenderSettings.ambientEquatorColor = Color.Lerp(savedEquator, fog * .8f, submerged);
            RenderSettings.ambientGroundColor = Color.Lerp(savedGround, fog * .4f, submerged);
            volume.weight = submerged;
            channels.redOutRedIn.Override(Mathf.Lerp(100, 12, Mathf.Clamp01(depth / 10)));
            colors.postExposure.Override(-.65f * deep);
            colors.saturation.Override(-12 * deep);
        }

        void UpdatePrey(float submerged)
        {
            if (Time.time >= nextPreyScan)
            {
                prey = FindObjectsByType<Prey>(FindObjectsSortMode.None);
                nextPreyScan = Time.time + 2;
            }
            int count = 0;
            foreach (var food in prey)
            {
                if (!food || !food.isActiveAndEnabled || food.gameObject.scene != gameObject.scene) continue;
                Vector3 offset = food.transform.position - viewer.transform.position;
                float distance = offset.magnitude;
                if (distance < .2f || distance > visibility || Vector3.Dot(viewer.transform.forward, offset / distance) < .924f) continue;
                float alpha = submerged * Mathf.Clamp01((visibility - distance) / 2) * (.5f + .2f * Mathf.Sin(Time.time * 3 + count));
                preyParticles[count++] = new ParticleSystem.Particle {
                    position = food.transform.position, startSize = .07f,
                    startColor = new Color(.4f, 1, .85f, alpha), startLifetime = 1, remainingLifetime = 1
                };
                if (count == preyParticles.Length) break;
            }
            highlights.SetParticles(preyParticles, count);
        }

        static void Pulse(XRNode hand, float amplitude)
        {
            var device = InputDevices.GetDeviceAtXRNode(hand);
            if (device.isValid && device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), .055f);
        }

        void OnDisable()
        {
            Drift = Vector3.zero;
            Agitation = 0;
            if (!generated) return;
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedMode;
            RenderSettings.fogDensity = savedDensity;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientSkyColor = savedSky;
            RenderSettings.ambientEquatorColor = savedEquator;
            RenderSettings.ambientGroundColor = savedGround;
            if (viewer) viewer.backgroundColor = savedBackground;
            if (cameraData) cameraData.requiresColorTexture = savedOpaque;
            foreach (var component in profile.components) Destroy(component);
            Destroy(profile);
            Destroy(generated);
        }

        void OnDrawGizmosSelected()
        {
            if (zones == null) return;
            foreach (var zone in zones)
            {
                Gizmos.color = zone.shelter ? Color.cyan : new Color(1, .55f, .1f);
                Gizmos.DrawWireSphere(zone.center, zone.radius);
                Gizmos.DrawRay(zone.center, zone.drift * 10);
            }
        }
    }
}
