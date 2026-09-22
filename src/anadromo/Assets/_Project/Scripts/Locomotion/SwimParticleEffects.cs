using UnityEngine;

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Crea y maneja efectos de partículas para la experiencia de nado del salmón en VR.
    /// 
    /// Fase 5 - Efectos visuales de nado.
    /// </summary>
    public class SwimParticleEffects : MonoBehaviour
    {
        [Header("Dependencias")]
        [SerializeField] private FlapDetector flapDetector;
        [SerializeField] private Rigidbody playerRigidbody;

        [Header("Configuración de Burbujas (Aleteo)")]
        [SerializeField] private bool enableBubbles = true;
        
        [Tooltip("Cantidad base de burbujas por aleteo.")]
        [SerializeField] private int bubblesPerFlap = 8;
        
        [Tooltip("Tamaño de las burbujas.")]
        [Range(0.005f, 0.05f)]
        [SerializeField] private float bubbleSize = 0.02f;

        [Header("Configuración de Estela (Velocidad)")]
        [SerializeField] private bool enableTrail = true;
        
        [Tooltip("Velocidad mínima para emitir partículas de estela (m/s).")]
        [SerializeField] private float trailSpeedThreshold = 1.0f;

        // Sistemas de partículas creados dinámicamente
        private ParticleSystem leftFinBubbles;
        private ParticleSystem rightFinBubbles;
        private ParticleSystem wakeTrail;

        private void Start()
        {
            if (enableBubbles)
            {
                // Posiciones aproximadas de las aletas pectorales (atrás y a los lados)
                leftFinBubbles = CreateBubbleSystem("LeftFinBubbles", new Vector3(-0.3f, -0.2f, -0.2f));
                rightFinBubbles = CreateBubbleSystem("RightFinBubbles", new Vector3(0.3f, -0.2f, -0.2f));
            }

            if (enableTrail)
            {
                // Estela detrás del cuerpo
                wakeTrail = CreateTrailSystem("WakeTrail", new Vector3(0f, 0f, -0.5f));
            }
        }

        private void OnEnable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.AddListener(OnLeftFlap);
                flapDetector.OnRightFlap.AddListener(OnRightFlap);
            }
        }

        private void OnDisable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.RemoveListener(OnLeftFlap);
                flapDetector.OnRightFlap.RemoveListener(OnRightFlap);
            }
        }

        private void Update()
        {
            if (!enableTrail || wakeTrail == null || playerRigidbody == null) return;

            float speed = playerRigidbody.linearVelocity.magnitude;
            var emission = wakeTrail.emission;

            if (speed > trailSpeedThreshold)
            {
                if (!emission.enabled) emission.enabled = true;
                
                // Emisión proporcional a la velocidad
                emission.rateOverTime = (speed - trailSpeedThreshold) * 5f; 
            }
            else
            {
                if (emission.enabled)
                {
                    emission.enabled = false;
                    emission.rateOverTime = 0f;
                }
            }
        }

        private void OnLeftFlap(float intensity)
        {
            if (enableBubbles && leftFinBubbles != null)
            {
                EmitBubbles(leftFinBubbles, intensity);
            }
        }

        private void OnRightFlap(float intensity)
        {
            if (enableBubbles && rightFinBubbles != null)
            {
                EmitBubbles(rightFinBubbles, intensity);
            }
        }

        private void EmitBubbles(ParticleSystem ps, float intensity)
        {
            // Escalar cantidad de burbujas con la intensidad del aleteo
            int count = Mathf.Max(1, Mathf.RoundToInt(bubblesPerFlap * intensity));
            
            // Asegurarse de no emitir demasiadas de golpe si la intensidad es anómalamente alta
            count = Mathf.Clamp(count, 1, 15);
            
            ps.Emit(count);
        }

        private ParticleSystem CreateBubbleSystem(string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var pso = go.GetComponent<ParticleSystemRenderer>();
            
            // Main config
            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSpeed = 0f; // La velocidad se controla con velocityOverLifetime
            main.startSize = new ParticleSystem.MinMaxCurve(bubbleSize * 0.5f, bubbleSize * 1.5f);
            main.maxParticles = 20; // Máximo para rendimiento (Quest 2)
            main.startColor = new Color(0.85f, 0.95f, 1f, 1f); // Azul claro/blanco
            main.playOnAwake = false;

            // Emission
            var emission = ps.emission;
            emission.enabled = false; // Solo emitimos por ráfagas (burst) mediante código

            // Shape
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            // Movement (burbujas flotando hacia arriba en World Space)
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);

            // Color / Fade out
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(Color.white, 0.0f), 
                    new GradientColorKey(new Color(0.8f, 0.95f, 1f), 1.0f) 
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(0.0f, 0.0f), 
                    new GradientAlphaKey(1.0f, 0.2f), 
                    new GradientAlphaKey(0.0f, 1.0f) 
                }
            );
            colorOverLifetime.color = grad;

            // Renderer
            pso.renderMode = ParticleSystemRenderMode.Billboard;
            AssignDefaultMaterial(pso);
            
            return ps;
        }

        private ParticleSystem CreateTrailSystem(string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var pso = go.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = 0f; // Se quedan en el sitio donde se emitieron (efecto estela)
            main.startSize = new ParticleSystem.MinMaxCurve(bubbleSize * 0.8f, bubbleSize * 1.2f);
            main.maxParticles = 10;
            main.startColor = new Color(0.9f, 0.95f, 1f, 0.6f);
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            pso.renderMode = ParticleSystemRenderMode.Billboard;
            AssignDefaultMaterial(pso);
            
            return ps;
        }

        private void AssignDefaultMaterial(ParticleSystemRenderer pso)
        {
            // Intentar cargar shaders comunes para asegurar que no sale magenta
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            
            if (shader != null)
            {
                pso.material = new Material(shader);
            }
            // Si shader es null, Unity usará el material default de partículas asignado al añadir el componente
        }
    }
}
