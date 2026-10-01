using Anadromo.Systems;
using UnityEngine;

namespace Anadromo.Mechanics
{
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerOrcaImpact : MonoBehaviour
    {
        [Min(.1f)] public float repeatDelay = .8f;
        [Min(0)] public float shakeAmplitude = .015f;
        Rigidbody body;
        PlayerEnergyController vital;
        AudioSource audioSource;
        Transform shakeRoot;
        Vector3 impulse;
        float started = float.NegativeInfinity, duration = 2f;
        public int ImpactCount { get; private set; }
        public AudioSource ImpactAudio => audioSource;
        public float Strength => isActiveAndEnabled ? Mathf.Clamp01(1 - (Time.time - started) / duration) : 0;
        public Vector3 PushVelocity => impulse * Mathf.Exp(-5 * Mathf.Max(0, Time.time - started)) * Strength;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            vital = GetComponent<PlayerEnergyController>();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0;
            audioSource.loop = false;
        }

        public bool Push(Vector3 velocity, float seconds, AudioClip clip, float volume)
        {
            if (!isActiveAndEnabled || body.isKinematic || velocity.sqrMagnitude < .0001f ||
                (vital && (!vital.HasEnergy || vital.Energy.ConsumptionPaused)) || Time.time - started < repeatDelay)
                return false;
            // Remove the remaining impulse before accepting another contact: no unbounded stacking.
            Vector3 previous = PushVelocity;
            impulse = Vector3.ClampMagnitude(velocity, 6f);
            started = Time.time;
            duration = Mathf.Max(.1f, seconds);
            body.AddForce(impulse - previous, ForceMode.VelocityChange);
            ImpactCount++;
            if (clip) { audioSource.clip = clip; audioSource.volume = volume; audioSource.Play(); }
            if (!shakeRoot)
            {
                var viewer = GetComponentInChildren<Camera>();
                if (viewer && viewer.transform != transform)
                {
                    shakeRoot = new GameObject("Orca impact camera shake").transform;
                    shakeRoot.SetParent(viewer.transform.parent, false);
                    viewer.transform.SetParent(shakeRoot, false);
                }
            }
            return true;
        }

        void LateUpdate()
        {
            if (!shakeRoot) return;
            if (Strength <= 0) { shakeRoot.localPosition = Vector3.zero; return; }
            float t = (Time.time - started) * 32;
            shakeRoot.localPosition = new Vector3(Mathf.Sin(t * 1.13f), Mathf.Sin(t * 1.71f), 0)
                * (shakeAmplitude * Strength);
        }

        void OnDisable()
        {
            impulse = Vector3.zero;
            started = float.NegativeInfinity;
            if (shakeRoot) shakeRoot.localPosition = Vector3.zero;
            if (audioSource) audioSource.Stop();
        }

        void OnDestroy()
        {
            // The identity pivot belongs to the player hierarchy and dies with it.
            // Reparenting its camera during hierarchy destruction is invalid in Unity.
            if (audioSource) Destroy(audioSource);
        }
    }
}
