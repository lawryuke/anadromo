using System;
using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>One impulse per excursion from a flexible rest point, followed by recovery.</summary>
    public sealed class ArmStrokeCycle
    {
        [Serializable]
        public struct Parameters
        {
            [Tooltip("Recorrido mínimo, en anchos de hombros.")]
            [Range(0.1f, 1f)] public float amplitude;
            [Tooltip("Fracción del recorrido a la que debe regresar para preparar otro impulso.")]
            [Range(0.1f, 0.8f)] public float recoveryRatio;
            [Tooltip("Radio tolerado para reconocer una postura quieta, en anchos de hombros.")]
            [Range(0.01f, 0.15f)] public float restRadius;
            [Tooltip("Tiempo quieto para adoptar una nueva posición de reposo.")]
            [Range(0.3f, 2f)] public float restHoldTime;
            [Range(0.02f, 0.3f)] public float smoothingTime;
            [Range(0.04f, 0.3f)] public float minimumStrokeTime;
            [Range(0.1f, 0.8f)] public float cooldown;
            [Tooltip("Saltos mayores se consideran pérdida de tracking (anchos de hombros/segundo).")]
            public float maximumSampleSpeed;
            [Range(0.1f, 1f)] public float minimumIntensity;
            [Tooltip("Velocidad para alcanzar intensidad 1, en anchos de hombros/segundo.")]
            public float fullIntensitySpeed;

            public static Parameters Default => new Parameters
            {
                amplitude = 0.3f, recoveryRatio = 0.45f, restRadius = 0.05f,
                restHoldTime = 0.7f, smoothingTime = 0.08f, minimumStrokeTime = 0.08f,
                cooldown = 0.25f, maximumSampleSpeed = 15f,
                minimumIntensity = 0.4f, fullIntensitySpeed = 2.5f
            };
        }

        public bool Initialized { get; private set; }
        public bool Recovering { get; private set; }
        public float Excursion { get; private set; }
        private Vector2 filtered, previousRaw, anchor, stablePoint;
        private float stableTime, strokeTime, cooldown;

        public void Reset()
        {
            Initialized = false;
            Recovering = false;
            Excursion = stableTime = strokeTime = cooldown = 0f;
        }

        public bool Step(Vector2 point, float dt, Parameters p, out float intensity)
        {
            intensity = 0f;
            if (!Initialized)
            {
                filtered = previousRaw = anchor = stablePoint = point;
                Initialized = true;
                return false;
            }
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return false;
            if (Vector2.Distance(point, previousRaw) / dt > p.maximumSampleSpeed)
            {
                Reset();
                return false;
            }
            previousRaw = point;
            Vector2 previous = filtered;
            filtered = Vector2.Lerp(filtered, point, 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, p.smoothingTime)));
            float speed = Vector2.Distance(filtered, previous) / dt;
            cooldown = Mathf.Max(0f, cooldown - dt);

            // Any comfortable stationary posture can become the next rest point.
            if (Vector2.Distance(filtered, stablePoint) > p.restRadius)
            {
                stablePoint = filtered;
                stableTime = 0f;
            }
            else stableTime += dt;
            if (stableTime >= p.restHoldTime)
            {
                anchor = filtered;
                Recovering = false;
                strokeTime = 0f;
            }

            Excursion = Vector2.Distance(filtered, anchor);
            if (Recovering)
            {
                if (Excursion <= p.amplitude * p.recoveryRatio)
                {
                    Recovering = false;
                    strokeTime = 0f;
                }
                return false; // Recovery never produces propulsion.
            }

            strokeTime = Excursion > p.restRadius ? strokeTime + dt : 0f;
            if (Excursion < p.amplitude || strokeTime < p.minimumStrokeTime || cooldown > 0f)
                return false;

            intensity = Mathf.Lerp(p.minimumIntensity, 1f,
                Mathf.Clamp01(speed / Mathf.Max(0.01f, p.fullIntensitySpeed)));
            Recovering = true;
            cooldown = p.cooldown;
            strokeTime = 0f;
            return true;
        }
    }
}
