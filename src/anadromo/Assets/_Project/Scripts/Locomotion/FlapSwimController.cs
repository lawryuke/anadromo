using UnityEngine;
using Anadromo.Act1; // Para integración con corrientes (OceanEnvironment)

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Controlador físico de nado por aleteo.
    /// Se acopla al Rigidbody del XR Origin y lee eventos del FlapDetector.
    /// Maneja el SalmonBody (pitch + yaw) y aplica fuerzas en su dirección.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FlapSwimController : MonoBehaviour
    {
        [Header("Dependencias")]
        [Tooltip("Detector de aleteo en la escena (PoseBridge).")]
        [SerializeField] private FlapDetector flapDetector;
        
        [Tooltip("Asset de configuración de físicas de nado.")]
        [SerializeField] private SwimSettings settings;

        [Tooltip("Transform de la cámara principal (Headset VR).")]
        [SerializeField] private Transform headTransform;

        [Tooltip("Transform que representa el cuerpo del salmón (orientación de avance).")]
        [SerializeField] private Transform salmonBody;

        [Header("Debug")]
        [SerializeField] private bool applyOceanCurrents = true;

        private Rigidbody rb;

        // ─── Variables de estado para retrasar la evaluación ───
        private float leftFlapTimer = 0f;
        private float rightFlapTimer = 0f;
        private bool leftFlapPending = false;
        private bool rightFlapPending = false;
        private float storedLeftIntensity = 0f;
        private float storedRightIntensity = 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.AddListener(HandleLeftFlap);
                flapDetector.OnRightFlap.AddListener(HandleRightFlap);
            }
        }

        private void OnDisable()
        {
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.RemoveListener(HandleLeftFlap);
                flapDetector.OnRightFlap.RemoveListener(HandleRightFlap);
            }
        }

        private void FixedUpdate()
        {
            if (settings == null || salmonBody == null || headTransform == null) return;

            UpdateBodyPitch();
            UpdateBodyYaw();
            LimitVelocities();
            ApplyCurrents();
            ProcessPendingFlaps();
        }

        private void ApplyCurrents()
        {
            if (applyOceanCurrents && !rb.isKinematic)
            {
                try
                {
                    Vector3 currentForce = OceanEnvironment.PlayerCurrentAt(rb.position);
                    rb.AddForce(currentForce * rb.linearDamping, ForceMode.Acceleration);
                }
                catch { }
            }
        }

        // ─── Manejo de Aleteos ───

        private void HandleLeftFlap(float intensity)
        {
            leftFlapPending = true;
            leftFlapTimer = 0f;
            storedLeftIntensity = intensity;
        }

        private void HandleRightFlap(float intensity)
        {
            rightFlapPending = true;
            rightFlapTimer = 0f;
            storedRightIntensity = intensity;
        }

        private void ProcessPendingFlaps()
        {
            if (rb.isKinematic) return;

            float dt = Time.fixedDeltaTime;

            if (leftFlapPending) leftFlapTimer += dt;
            if (rightFlapPending) rightFlapTimer += dt;

            // CASO 1: Ambos brazos han aleteado dentro de la ventana de simultaneidad
            if (leftFlapPending && rightFlapPending)
            {
                // Avance frontal
                float avgIntensity = (storedLeftIntensity + storedRightIntensity) * 0.5f;
                ApplyForwardImpulse(avgIntensity);

                // Consumir
                leftFlapPending = false;
                rightFlapPending = false;
                return;
            }

            // Aleteo individual: se descarta al pasar la ventana de simultaneidad.
            // El giro ahora lo controla el headset VR (UpdateBodyYaw).
            if (leftFlapPending && leftFlapTimer > settings.simultaneityWindow)
                leftFlapPending = false;

            if (rightFlapPending && rightFlapTimer > settings.simultaneityWindow)
                rightFlapPending = false;
        }

        // ─── Aplicación de Fuerzas ───

        private void ApplyForwardImpulse(float intensity)
        {
            if (salmonBody == null) return;
            
            // Avanzar en la dirección frontal del cuerpo del salmón
            Vector3 force = salmonBody.forward * (intensity * settings.forwardForceMultiplier);
            rb.AddForce(force, ForceMode.Impulse);
        }

        // ─── Movimiento del Cuerpo ───

        /// <summary>
        /// El cuerpo del salmón rota en yaw siguiendo la orientación del headset VR.
        /// Esto reemplaza la rotación por aleteo individual: ahora a donde miras, giras.
        /// </summary>
        private void UpdateBodyYaw()
        {
            float headYaw = headTransform.eulerAngles.y;
            float currentYaw = salmonBody.eulerAngles.y;

            float newYaw = Mathf.LerpAngle(currentYaw, headYaw,
                                            Time.fixedDeltaTime * settings.yawLerpSpeed);

            // Preservar el pitch interpolado (de UpdateBodyPitch) + el nuevo yaw
            salmonBody.eulerAngles = new Vector3(
                salmonBody.eulerAngles.x,   // pitch (manejado por UpdateBodyPitch)
                newYaw,                      // yaw ← sigue la cabeza
                0f                           // roll = 0
            );
        }

        private void UpdateBodyPitch()
        {
            // El pitch del cuerpo del salmón sigue suavemente el pitch de la cabeza (headset).

            // 1. Obtener el pitch del headset (convirtiéndolo a -180...180)
            float headPitch = headTransform.eulerAngles.x;
            if (headPitch > 180f) headPitch -= 360f;

            // Restringir el pitch máximo para que el salmón no se voltee por completo
            headPitch = Mathf.Clamp(headPitch, -settings.maxPitchAngle, settings.maxPitchAngle);

            // 2. Obtener la rotación actual del cuerpo
            float currentBodyPitch = salmonBody.eulerAngles.x;
            if (currentBodyPitch > 180f) currentBodyPitch -= 360f;

            // 3. Interpolar suavemente
            float newPitch = Mathf.Lerp(currentBodyPitch, headPitch, Time.fixedDeltaTime * settings.pitchLerpSpeed);

            // 4. Aplicar pitch (yaw se aplica en UpdateBodyYaw, roll = 0)
            salmonBody.eulerAngles = new Vector3(newPitch, salmonBody.eulerAngles.y, 0f);
        }

        private void LimitVelocities()
        {
            // Limitar velocidad lineal
            if (rb.linearVelocity.sqrMagnitude > settings.maxLinearVelocity * settings.maxLinearVelocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * settings.maxLinearVelocity;
            }

            // Limitar velocidad angular
            if (rb.angularVelocity.sqrMagnitude > settings.maxAngularVelocity * settings.maxAngularVelocity)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * settings.maxAngularVelocity;
            }
        }
    }
}
