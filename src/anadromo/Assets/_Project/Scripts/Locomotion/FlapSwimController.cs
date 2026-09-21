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

            // CASO 2: Solo el izquierdo aleteó y ya pasó la ventana de espera
            if (leftFlapPending && leftFlapTimer > settings.simultaneityWindow)
            {
                ApplyRotationTorque(storedLeftIntensity, 1f); // Rotar Derecha
                ApplyForwardImpulse(storedLeftIntensity * settings.forwardOnSingleFlapRatio);
                leftFlapPending = false;
            }

            // CASO 3: Solo el derecho aleteó y ya pasó la ventana de espera
            if (rightFlapPending && rightFlapTimer > settings.simultaneityWindow)
            {
                ApplyRotationTorque(storedRightIntensity, -1f); // Rotar Izquierda
                ApplyForwardImpulse(storedRightIntensity * settings.forwardOnSingleFlapRatio);
                rightFlapPending = false;
            }
        }

        // ─── Aplicación de Fuerzas ───

        private void ApplyForwardImpulse(float intensity)
        {
            if (salmonBody == null) return;
            
            // Avanzar en la dirección frontal del cuerpo del salmón
            Vector3 force = salmonBody.forward * (intensity * settings.forwardForceMultiplier);
            rb.AddForce(force, ForceMode.Impulse);
        }

        private void ApplyRotationTorque(float intensity, float direction)
        {
            // Rotar en el eje Y global (Yaw)
            Vector3 torque = Vector3.up * (intensity * settings.rotationTorqueMultiplier * direction);
            rb.AddTorque(torque, ForceMode.Impulse);
        }

        // ─── Movimiento del Cuerpo ───

        private void UpdateBodyPitch()
        {
            // Queremos que el SalmonBody siga el Yaw (eje Y) del propio SalmonBody
            // (que es manejado por la rotación física del Rigidbody del XR Origin)
            // pero que siga el Pitch (arriba/abajo) de la cabeza (headTransform).

            // 1. Obtener el pitch del headset (convirtiéndolo a -180...180)
            float headPitch = headTransform.eulerAngles.x;
            if (headPitch > 180f) headPitch -= 360f;

            // Restringir el pitch máximo para que el salmón no se voltee por completo
            headPitch = Mathf.Clamp(headPitch, -settings.maxPitchAngle, settings.maxPitchAngle);

            // 2. Obtener la rotación actual del cuerpo (su yaw viene del padre XR Origin, su pitch es local)
            float currentBodyPitch = salmonBody.localEulerAngles.x;
            if (currentBodyPitch > 180f) currentBodyPitch -= 360f;

            // 3. Interpolar suavemente
            float newPitch = Mathf.Lerp(currentBodyPitch, headPitch, Time.fixedDeltaTime * settings.pitchLerpSpeed);

            // 4. Aplicar (mantenemos yaw y roll local en 0, ya que el Rigidbody del XR Origin controla el yaw)
            salmonBody.localEulerAngles = new Vector3(newPitch, 0f, 0f);
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
