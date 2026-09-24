using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace Anadromo.Mechanics
{
    /// <summary>One owner for E4 fog and depth grading. Does not edit shared materials or the Act 1 renderer.</summary>
    public sealed class Scenario4VisibilityController : MonoBehaviour
    {
        public float surfaceY = 48;
        public float calmVisibility = 15;
        public float minimumVisibility = 11;
        public Material particleMaterial;
        public Transform[] shelters;
        Scenario4Manager encounter;
        float sediment, currentVisibility, nextPulse;
        bool oldFog;
        FogMode oldMode;
        float oldDensity;
        Color oldFogColor, oldBackground, oldSky, oldEquator, oldGround;
        AmbientMode oldAmbient;
        VolumeProfile profile;
        ColorAdjustments colors;
        ChannelMixer channels;
        Vignette hitVignette;
        ChromaticAberration hitAberration;
        float hitEffect;
        ParticleSystem motes;
        AudioSource rumble;
        AudioClip rumbleClip;
        bool initialized;

        public void Initialize(Scenario4Manager manager)
        {
            encounter = manager;
            oldFog = RenderSettings.fog; oldMode = RenderSettings.fogMode;
            oldDensity = RenderSettings.fogDensity; oldFogColor = RenderSettings.fogColor;
            oldSky = RenderSettings.ambientSkyColor; oldEquator = RenderSettings.ambientEquatorColor;
            oldGround = RenderSettings.ambientGroundColor; oldAmbient = RenderSettings.ambientMode;
            oldBackground = encounter.viewer.backgroundColor;
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 110;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;
            colors = profile.Add<ColorAdjustments>(true);
            channels = profile.Add<ChannelMixer>(true);
            hitVignette = profile.Add<Vignette>(true);
            hitVignette.color.Override(new Color(.04f, .13f, .16f));
            hitVignette.smoothness.Override(.65f);
            hitAberration = profile.Add<ChromaticAberration>(true);
            motes = gameObject.AddComponent<ParticleSystem>();
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.maxParticles = 140; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 4; main.startSpeed = .12f;
            main.startSize = new ParticleSystem.MinMaxCurve(.015f, .055f);
            main.startColor = new Color(.4f, .8f, .75f, .25f);
            var shape = motes.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 4;
            var emission = motes.emission; emission.rateOverTime = 18;
            var renderer = motes.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial; renderer.shadowCastingMode = ShadowCastingMode.Off;
            motes.Play();
            rumble = gameObject.AddComponent<AudioSource>();
            rumble.loop = true; rumble.spatialBlend = 0; rumble.volume = 0;
            var samples = new float[22050];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / 22050f;
                samples[i] = Mathf.Sin(t * Mathf.PI * 2 * 55) * .3f + Mathf.Sin(t * Mathf.PI * 2 * 83) * .12f;
            }
            rumbleClip = AudioClip.Create("Presencia del Bloop E4", samples.Length, 1, 22050, false);
            rumbleClip.SetData(samples, 0); rumble.clip = rumbleClip; rumble.Play();
            initialized = true;
            ResetVision();
        }

        public void AddSediment(float strength) { sediment = Mathf.Clamp01(sediment + strength); }
        public void HitFeedback(float severity)
        {
            hitEffect = Mathf.Max(hitEffect, Mathf.Clamp01(severity));
            Pulse(XRNode.LeftHand, .22f * severity);
            Pulse(XRNode.RightHand, .22f * severity);
        }
        public void ResetVision()
        {
            sediment = 0; hitEffect = 0; currentVisibility = calmVisibility;
            if (rumble) rumble.volume = 0;
            if (hitVignette != null) hitVignette.intensity.Override(0);
            if (hitAberration != null) hitAberration.intensity.Override(0);
        }
        void LateUpdate()
        {
            if (!initialized || !encounter || !encounter.viewer) return;
            Vector3 point = encounter.viewer.transform.position;
            transform.position = point;
            sediment = Mathf.MoveTowards(sediment, 0, Time.deltaTime * .13f);
            hitEffect = Mathf.MoveTowards(hitEffect, 0, Time.deltaTime * .45f);
            // A brief dazed effect; never rotate, roll or blur the tracked HMD camera.
            hitVignette.intensity.Override(hitEffect * (XRSettings.isDeviceActive ? .18f : .32f));
            hitAberration.intensity.Override(XRSettings.isDeviceActive ? 0 : hitEffect * .22f);
            float shelter = 0;
            if (shelters != null) foreach (var item in shelters)
                if (item) shelter = Mathf.Max(shelter, 1 - Mathf.Clamp01(Vector3.Distance(point, item.position) / 3));
            float activity = Mathf.Clamp01(sediment + encounter.Threat * .25f) * (1 - shelter * .8f);
            float target = Mathf.Lerp(calmVisibility, minimumVisibility, activity);
            if (encounter.Phase == Scenario4Phase.Won) target = 19;
            currentVisibility = Mathf.Lerp(currentVisibility, target, 1 - Mathf.Exp(-Time.deltaTime * 1.5f));
            float depth = 1 - Mathf.Exp(-Mathf.Max(0, surfaceY - point.y) / 24);
            Color fog = Color.Lerp(new Color(.025f, .22f, .28f), new Color(.01f, .05f, .11f), depth);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Sqrt(-Mathf.Log(.1f)) / Mathf.Max(1, currentVisibility);
            RenderSettings.fogColor = fog;
            encounter.viewer.backgroundColor = fog;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.15f, .45f, .48f);
            RenderSettings.ambientEquatorColor = fog * 1.85f;
            RenderSettings.ambientGroundColor = fog * .6f;
            colors.postExposure.Override(-.08f * depth - hitEffect * .08f);
            colors.saturation.Override(-5 * depth);
            channels.redOutRedIn.Override(Mathf.Lerp(100, 25, depth));
            var emission = motes.emission; emission.rateOverTime = Mathf.Lerp(18, 38, activity);
            rumble.volume = Mathf.Lerp(rumble.volume, encounter.Running ? .08f + encounter.Threat * .3f :
                encounter.Phase == Scenario4Phase.Awakening ? .12f : 0, Time.deltaTime * 2);
            if (encounter.Threat > .1f && Time.time >= nextPulse)
            {
                Pulse(XRNode.LeftHand, encounter.Threat * .15f);
                Pulse(XRNode.RightHand, encounter.Threat * .12f);
                nextPulse = Time.time + .3f;
            }
        }
        static void Pulse(XRNode hand, float strength)
        {
            var device = InputDevices.GetDeviceAtXRNode(hand);
            if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)
                device.SendHapticImpulse(0, strength, .08f);
        }
        void OnDisable()
        {
            if (!initialized) return;
            RenderSettings.fog = oldFog; RenderSettings.fogMode = oldMode;
            RenderSettings.fogDensity = oldDensity; RenderSettings.fogColor = oldFogColor;
            RenderSettings.ambientMode = oldAmbient;
            RenderSettings.ambientSkyColor = oldSky; RenderSettings.ambientEquatorColor = oldEquator; RenderSettings.ambientGroundColor = oldGround;
            if (encounter && encounter.viewer) encounter.viewer.backgroundColor = oldBackground;
            if (rumble) rumble.Stop();
            if (motes) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var volume = GetComponent<Volume>(); if (volume) volume.enabled = false;
        }
        void OnDestroy()
        {
            if (rumbleClip) Destroy(rumbleClip);
            if (!profile) return;
            foreach (var component in profile.components) Destroy(component);
            Destroy(profile);
        }
    }
}
