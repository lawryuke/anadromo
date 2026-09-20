using System.Collections.Generic;
using UnityEngine;

/// <summary>Uses Bloop's authored spine, fluke and fin bones for slow, vertical abyssal ascent with subtle fin motion (poco aleteo).</summary>
[DisallowMultipleComponent]
public sealed class BloopSwimAnimation : MonoBehaviour
{
    class Joint
    {
        public Transform bone;
        public Quaternion rest;
        public Vector3 primaryAxis;
        public Vector3 secondaryAxis;
        public float primaryAmp;
        public float secondaryAmp;
        public float lag;
    }
    readonly List<Joint> joints = new List<Joint>();
    NaturalSwimPath movement;
    float phase;
    SkinnedMeshRenderer[] skins;
    Bounds[] originalBounds;

    void Awake() => Initialize();

    public void Initialize()
    {
        if (joints.Count > 0) return;
        movement = GetComponent<NaturalSwimPath>();
        skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        originalBounds = new Bounds[skins.Length];
        for (int i = 0; i < skins.Length; i++)
        {
            originalBounds[i] = skins[i].localBounds;
            Bounds expanded = originalBounds[i];
            expanded.Expand(expanded.size * 0.4f);
            skins[i].localBounds = expanded;
        }
        var bones = GetComponentsInChildren<Transform>(true);
        Transform head = null, tailRoot = null, left = null, right = null;
        foreach (var bone in bones)
        {
            if (bone.name == "w_hoofd1") head = bone;
            if (bone.name == "w_rug4") tailRoot = bone;
            if (bone.name == "w_L_vin1") left = bone;
            if (bone.name == "w_R_vin1") right = bone;
        }
        Vector3 side = left && right ? (left.position - right.position).normalized : transform.right;
        Vector3 forward = head && tailRoot ? (head.position - tailRoot.position).normalized : transform.forward;
        Vector3 up = Vector3.Cross(forward, side).normalized;

        foreach (var bone in bones)
        {
            string name = bone.name;
            float pAmp = 0, sAmp = 0, lag = 0;
            Vector3 worldPrimary = side;
            Vector3 worldSecondary = up;

            // 1. Columna (w_rug): Ondulación suave y pesada de criatura abisal
            if (name.StartsWith("w_rug") && int.TryParse(name.Substring(5), out int segment))
            {
                pAmp = 1.5f + segment * 0.8f;
                sAmp = 0.5f + segment * 0.3f;
                lag = segment * 0.5f;
                worldPrimary = side;
                worldSecondary = up;
            }
            // 2. Cola / Aletas caudales (w_L_staart, w_R_staart): Movimiento pausado y sutil
            else if ((name.StartsWith("w_L_staart") || name.StartsWith("w_R_staart")) &&
                     int.TryParse(name.Substring(10), out int tail))
            {
                pAmp = 3.0f + tail * 1.2f;
                sAmp = 1.0f + tail * 0.4f;
                lag = 2.0f + tail * 0.3f;
                worldPrimary = side;
                worldSecondary = up;
            }
            // 3. Aletas laterales (w_L_vin, w_R_vin): Poco aleteo (movimiento muy sutil y natural)
            else if ((name.StartsWith("w_L_vin") || name.StartsWith("w_R_vin")) &&
                     int.TryParse(name.Substring(7), out int fin))
            {
                bool isLeft = name.StartsWith("w_L");
                float sign = isLeft ? 1f : -1f;
                // Poco aleteo: amplitudes reducidas para un aleteo pausado y minimalista
                pAmp = sign * (fin == 1 ? 3.5f : (fin == 2 ? 2.0f : 1.0f));
                sAmp = sign * (fin == 1 ? 1.5f : (fin == 2 ? 0.8f : 0.4f));
                lag = fin * 0.4f;
                worldPrimary = forward;
                worldSecondary = side;
            }

            if (pAmp == 0 && sAmp == 0) continue;
            joints.Add(new Joint {
                bone = bone,
                rest = bone.localRotation,
                primaryAxis = bone.InverseTransformDirection(worldPrimary).normalized,
                secondaryAxis = bone.InverseTransformDirection(worldSecondary).normalized,
                primaryAmp = pAmp,
                secondaryAmp = sAmp,
                lag = lag
            });
        }
    }

    void LateUpdate() => Tick(Time.deltaTime);

    public void Tick(float deltaTime)
    {
        float effort = movement ? movement.Effort : 0f;
        float blendEffort = Mathf.Lerp(0.35f, 1.0f, effort);
        // Frecuencia lenta y pausada para ascenso vertical majestuoso
        phase = Mathf.Repeat(phase + Mathf.Max(0, deltaTime) * Mathf.Lerp(0.14f, 0.38f, effort) * Mathf.PI * 2, Mathf.PI * 2);

        foreach (var joint in joints)
        {
            if (!joint.bone) continue;
            float pRot = Mathf.Sin(phase - joint.lag) * joint.primaryAmp * blendEffort;
            float sRot = Mathf.Cos(phase - joint.lag) * joint.secondaryAmp * blendEffort;
            Quaternion rot = Quaternion.AngleAxis(pRot, joint.primaryAxis) * Quaternion.AngleAxis(sRot, joint.secondaryAxis);
            joint.bone.localRotation = joint.rest * rot;
        }
    }

    void OnDisable()
    {
        foreach (var joint in joints) if (joint.bone) joint.bone.localRotation = joint.rest;
    }

    void OnDestroy()
    {
        if (skins == null) return;
        for (int i = 0; i < skins.Length; i++) if (skins[i]) skins[i].localBounds = originalBounds[i];
    }
}
