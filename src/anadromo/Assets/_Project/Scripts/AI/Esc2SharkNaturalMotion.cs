using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.AI
{
    // Visual swimming only: the existing passage still controls position, timing and damage.
    [DefaultExecutionOrder(110)]
    public sealed class Esc2SharkNaturalMotion : MonoBehaviour
    {
        [Serializable] public sealed class Joint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public float amplitude, lag;
            public bool fin, jaw;
        }
        public Transform model;
        public Joint[] joints;
        Vector3 previousPosition, previousForward;
        Quaternion modelRest;
        float phase, speed, bank, breath;
        bool initialized;

        void Awake() { Initialize(); }
        void OnEnable()
        {
            Initialize();
            previousPosition = transform.position; previousForward = transform.forward;
            speed = bank = 0;
            if (model) model.localRotation = modelRest;
        }
        public void Initialize()
        {
            if (initialized || !model) return;
            modelRest = model.localRotation;
            previousPosition = transform.position; previousForward = transform.forward;
            phase = Mathf.Repeat(previousPosition.x * 1.73f + previousPosition.z * 2.31f, Mathf.PI * 2);
            breath = phase; initialized = true;
        }
        void LateUpdate() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            Initialize();
            if (!initialized || dt <= 0 || joints == null) return;
            var delta = transform.position - previousPosition;
            float measured = delta.magnitude / dt;
            if (delta.magnitude > 2) measured = 0; // Reset/teleport is not swimming speed.
            speed = Mathf.Lerp(speed, Mathf.Clamp(measured, 0, 20), 1 - Mathf.Exp(-5 * dt));
            phase = Mathf.Repeat(phase + dt * Mathf.PI * 2 * (.85f + speed * .085f), Mathf.PI * 2);
            breath = Mathf.Repeat(breath + dt * 1.8f, Mathf.PI * 2);
            float strength = Mathf.Lerp(.65f, 1.1f, Mathf.Clamp01(speed / 15));
            float yaw = Vector3.SignedAngle(previousForward, transform.forward, transform.up);
            bank = Mathf.Lerp(bank, Mathf.Clamp(-yaw / dt * .025f, -6, 6), 1 - Mathf.Exp(-5 * dt));
            foreach (var j in joints)
            {
                if (!j.bone) continue;
                float wave = j.jaw ? .5f + .5f * Mathf.Sin(breath) : Mathf.Sin((j.fin ? breath : phase) - j.lag);
                j.bone.localRotation = j.rest * Quaternion.AngleAxis(j.amplitude * wave * (j.jaw || j.fin ? 1 : strength), j.axis);
            }
            model.localRotation = modelRest * Quaternion.Euler(0, 0, bank);
            previousPosition = transform.position; previousForward = transform.forward;
        }
        public void Configure()
        {
            var result = new List<Joint>();
            foreach (var bone in model.GetComponentsInChildren<Transform>(true))
            {
                string n = bone.name; float amplitude = 0, lag = 0;
                bool fin = false, jaw = false; Vector3 axis = Vector3.up;
                if (n == "Bone.001_Armature") { amplitude = 2; lag = 0; }
                if (n == "Bone.003_Armature") { amplitude = 5; lag = .55f; }
                if (n == "Bone.002_Armature") { amplitude = 9; lag = 1.05f; }
                if (n == "Bone.004_Armature") { amplitude = 14; lag = 1.55f; }
                if (n == "Bone.024_Armature" || n == "Bone.025_Armature") { amplitude = 8; lag = 1.95f; }
                if (n == "Bone.011_Armature" || n == "Bone.012_Armature")
                { amplitude = n == "Bone.011_Armature" ? 2.5f : -2.5f; lag = .5f; fin = true; axis = Vector3.forward; }
                if (n == "Bone.009_Armature") { amplitude = 1.2f; lag = .7f; fin = true; }
                if (n == "Bone.026_Armature") { amplitude = 2; jaw = true; axis = Vector3.right; }
                if (amplitude > 0 || amplitude < 0)
                    result.Add(new Joint { bone = bone, rest = bone.localRotation, axis = bone.InverseTransformDirection(model.TransformDirection(axis)).normalized, amplitude = amplitude, lag = lag, fin = fin, jaw = jaw });
            }
            joints = result.ToArray();
        }
    }
}
