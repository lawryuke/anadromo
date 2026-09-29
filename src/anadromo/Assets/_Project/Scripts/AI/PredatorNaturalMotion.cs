using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.AI
{
    // Animates only the imported visual. The encounter owns movement, damage and timing.
    [DefaultExecutionOrder(100)]
    public sealed class PredatorNaturalMotion : MonoBehaviour
    {
        public enum Species { Piranha, Lamprey, Angler }
        public Species species;
        public Transform model;
        [Serializable] public sealed class Joint
        {
            public Transform bone;
            public Quaternion rest;
            public Vector3 axis;
            public float amplitude, lag;
            public int channel; // 0 swim, 1 jaw, 2 fins, 3 lure
        }
        public Joint[] joints;
        Esc2Piranha piranha;
        Esc2Lamprey lamprey;
        Esc2Anglerfish angler;
        SkinnedMeshRenderer oral;
        int oralIndex = -1;
        Vector3 previous, modelHome;
        Quaternion modelRest;
        float phase, breath, speed, jaw, biteRemaining;
        bool initialized;

        void Awake() { Initialize(); }
        public void Initialize()
        {
            if (initialized) return;
            piranha = GetComponentInParent<Esc2Piranha>();
            lamprey = GetComponentInParent<Esc2Lamprey>();
            angler = GetComponentInParent<Esc2Anglerfish>();
            if (!model) model = transform;
            modelHome = model.localPosition; modelRest = model.localRotation;
            previous = transform.parent ? transform.parent.position : transform.position;
            // Independent phase, stable for each spawn; no global random-state changes.
            phase = Mathf.Repeat(previous.x * 2.17f + previous.z * 1.39f, Mathf.PI * 2);
            breath = phase;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>())
                for (int i = 0; i < r.sharedMesh.blendShapeCount; i++)
                    if (r.sharedMesh.GetBlendShapeName(i).EndsWith("OralPulse")) { oral = r; oralIndex = i; }
            initialized = true;
        }
        public void Bite() { biteRemaining = .32f; }
        void LateUpdate() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            Initialize();
            if (dt <= 0 || joints == null) return;
            Vector3 current = transform.parent ? transform.parent.position : transform.position;
            Vector3 delta = current - previous; previous = current;
            float measured = delta.magnitude / dt;
            // Discard teleports and respawns instead of making the tail snap.
            if (delta.sqrMagnitude > 4) measured = 0;
            speed = Mathf.Lerp(speed, Mathf.Min(measured, 9), 1 - Mathf.Exp(-5 * dt));
            bool attack = (piranha && piranha.State == Esc2Piranha.BehaviourState.Attack) ||
                          (angler && angler.State == Esc2Anglerfish.BehaviourState.Attack);
            bool attached = lamprey && lamprey.State == Esc2Lamprey.BehaviourState.Attached;
            bool stunned = lamprey && lamprey.State == Esc2Lamprey.BehaviourState.Stunned;
            bool chill = lamprey && lamprey.State == Esc2Lamprey.BehaviourState.Chill;
            float frequency = species == Species.Angler ? .75f : species == Species.Lamprey ? (chill ? 0.3f : 1.2f) : 1.65f;
            frequency += speed * (species == Species.Angler ? .09f : .18f);
            phase = Mathf.Repeat(phase + dt * frequency * Mathf.PI * 2, Mathf.PI * 2);
            breath = Mathf.Repeat(breath + dt * 2.1f, Mathf.PI * 2);
            float strength = stunned ? .18f : attached ? .65f : chill ? 0.25f : .65f + Mathf.Clamp01(speed / 4) * .5f;
            biteRemaining = Mathf.Max(0, biteRemaining - dt);
            float bite = biteRemaining > 0 ? Mathf.Sin(Mathf.PI * (1 - biteRemaining / .32f)) : 0;
            float mouth = attack ? .65f : .06f + .04f * Mathf.Sin(breath);
            // Quick closing stroke when gameplay registers a bite, then reopen naturally.
            if (biteRemaining > 0) mouth = Mathf.Lerp(mouth, -.25f, bite);
            jaw = Mathf.Lerp(jaw, mouth, 1 - Mathf.Exp(-18 * dt));
            foreach (var j in joints)
            {
                if (!j.bone) continue;
                float wave = j.channel == 1 ? jaw : j.channel == 3 ? Mathf.Sin(breath + j.lag) : Mathf.Sin(phase - j.lag);
                float amount = j.channel == 1 ? 1 : j.channel == 3 ? .7f : strength;
                j.bone.localRotation = j.rest * Quaternion.AngleAxis(j.amplitude * wave * amount, j.axis);
            }
            if (oral) oral.SetBlendShapeWeight(oralIndex, (attached ? 48 : 22) + Mathf.Sin(breath * 1.8f) * 18);
            model.localPosition = modelHome + Vector3.up * (Mathf.Sin(breath) * (attached ? 0 : .007f));
            Quaternion desired = modelRest;
            // Lamprey gameplay does not rotate its root while chasing; orient just its mesh.
            if (lamprey)
            {
                if (attached) desired = Quaternion.Euler(0, 180, 0) * modelRest;
                else if (delta.sqrMagnitude > .000001f && delta.sqrMagnitude < 4)
                    desired = Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(delta.normalized, Vector3.up) * modelRest;
                else desired = model.localRotation;
            }
            model.localRotation = Quaternion.Slerp(model.localRotation, desired, 1 - Mathf.Exp(-8 * dt));
        }

        // Called by the installer in the rest pose; axes are converted to each bone's space.
        public void Configure()
        {
            var list = new List<Joint>();
            foreach (var b in model.GetComponentsInChildren<Transform>(true))
            {
                string n = b.name;
                float amplitude = 0, lag = 0; int channel = 0; Vector3 axis = Vector3.up;
                if (species == Species.Lamprey && n.StartsWith("Swim_"))
                { int index = int.Parse(n.Substring(5)); amplitude = index == 0 ? 2 : 5 + index * .65f; lag = index * .58f; }
                if (species == Species.Piranha)
                {
                    if (n == "hip_int") { amplitude = 7; lag = .4f; }
                    if (n == "caudal_int") { amplitude = 14; lag = 1.1f; }
                    if (n == "caudal_fin_int") { amplitude = 19; lag = 1.8f; }
                    if (n == "fin_int" || n == "spinous_int") { amplitude = 5; lag = .8f; channel = 2; }
                    if (n == "mouth_int") { amplitude = 23; channel = 1; axis = Vector3.right; }
                }
                if (species == Species.Angler)
                {
                    if (n == "Spine_2") { amplitude = 4; lag = .3f; }
                    if (n == "Tail") { amplitude = 12; lag = 1; }
                    if (n == "TailFinTop_1" || n == "TailFinBottom_1") { amplitude = 16; lag = 1.6f; }
                    if (n == "JawBottom_1") { amplitude = 17; channel = 1; axis = Vector3.right; }
                    if (n == "JawTop_1") { amplitude = -5; channel = 1; axis = Vector3.right; }
                    if (n.StartsWith("FinFront_1_") || n.StartsWith("FinMid_1_"))
                    { amplitude = n.EndsWith("L") ? 10 : -10; channel = 2; lag = .8f; axis = Vector3.forward; }
                    if (n == "Esca_2" || n == "Esca_4" || n == "Esca_6") { amplitude = 3; channel = 3; lag = n[5] * .5f; }
                }
                if (amplitude != 0) list.Add(new Joint { bone = b, rest = b.localRotation, axis = b.InverseTransformDirection(model.TransformDirection(axis)).normalized, amplitude = amplitude, lag = lag, channel = channel });
            }
            joints = list.ToArray();
        }
    }
}
