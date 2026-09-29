using System.Collections.Generic;
using UnityEngine;

namespace Anadromo.Mechanics
{
    /// <summary>Animates the Orca2 rig from actual world-space displacement.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Anadromo/Animation/Orca Route Swim Animation")]
    public sealed class OrcaRouteSwimAnimation : MonoBehaviour
    {
        [Header("Respuesta al movimiento")]
        [Min(.01f), Tooltip("Velocidad de desplazamiento que produce un nado de intensidad completa.")]
        public float referenceSpeed = 2f;
        [Min(0f)] public float minimumSpeed = .02f;
        [Min(.01f), Tooltip("Rapidez con la que empieza y termina el movimiento de nado.")]
        public float response = 6f;
        [Min(.01f), Tooltip("Un salto mayor a esta distancia en un frame se considera teletransporte.")]
        public float teleportDistance = 10f;

        [Header("Ondulación vertical")]
        [Min(0f), Tooltip("Ciclos de nado por segundo a intensidad completa.")]
        public float strokeFrequency = 1.2f;
        [Range(0f, 15f)] public float spineAngle = 3f;
        [Range(0f, 35f)] public float tailAngle = 14f;
        [Range(0f, 2f), Tooltip("Desfase de la onda entre articulaciones, en radianes.")]
        public float phaseLag = .55f;

        struct Joint
        {
            public Transform bone;
            public Quaternion restRotation;
            public Vector3 pitchAxis;
            public int index;
        }

        static readonly string[] JointNames = { "Spine1", "Spine2", "Tail1", "Tail2", "Tail3" };
        readonly List<Joint> joints = new List<Joint>();
        Vector3 previousPosition;
        float effort, phase;
        bool initialized;

        void OnEnable()
        {
            Initialize();
            previousPosition = transform.position;
            effort = phase = 0f;
        }

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            var bones = new HashSet<Transform>();
            foreach (var skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (var bone in skin.bones)
                    if (bone) bones.Add(bone);

            for (int i = 0; i < JointNames.Length; i++)
                foreach (var bone in bones)
                    if (bone.name == JointNames[i])
                    {
                        joints.Add(new Joint
                        {
                            bone = bone,
                            restRotation = bone.localRotation,
                            // The imported bones have different local axes. Bend vertically
                            // around the model's right axis, expressed in each bone's space.
                            pitchAxis = bone.InverseTransformDirection(transform.right).normalized,
                            index = i
                        });
                        break;
                    }
        }

        void LateUpdate() => Tick(Time.deltaTime);

        public void Tick(float deltaTime)
        {
            Initialize();
            float distance = Vector3.Distance(transform.position, previousPosition);
            previousPosition = transform.position;
            if (deltaTime <= 0f) return;
            if (distance > Mathf.Max(.01f, teleportDistance))
            {
                effort = 0f;
                RestorePose();
                return;
            }

            float speed = distance / deltaTime;
            float targetEffort = speed <= minimumSpeed ? 0f : Mathf.Clamp01(speed / Mathf.Max(.01f, referenceSpeed));
            effort = Mathf.Lerp(effort, targetEffort, 1f - Mathf.Exp(-Mathf.Max(.01f, response) * deltaTime));
            if (targetEffort == 0f && effort < .001f) effort = 0f;
            phase = Mathf.Repeat(phase + deltaTime * Mathf.Max(0f, strokeFrequency)
                * Mathf.Lerp(.35f, 1f, effort) * Mathf.PI * 2f, Mathf.PI * 2f);

            foreach (var joint in joints)
            {
                if (!joint.bone) continue;
                float amplitude = joint.index < 2
                    ? spineAngle * (joint.index == 0 ? .5f : 1f)
                    : tailAngle * Mathf.Lerp(.5f, 1f, (joint.index - 2) / 2f);
                float angle = Mathf.Sin(phase - joint.index * phaseLag) * amplitude * effort;
                joint.bone.localRotation = joint.restRotation * Quaternion.AngleAxis(angle, joint.pitchAxis);
            }
        }

        void RestorePose()
        {
            foreach (var joint in joints)
                if (joint.bone) joint.bone.localRotation = joint.restRotation;
        }

        void OnDisable() => RestorePose();
    }
}
