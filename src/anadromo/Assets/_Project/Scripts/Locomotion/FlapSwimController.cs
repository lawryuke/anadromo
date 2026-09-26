using UnityEngine;
using UnityEngine.InputSystem;
using Anadromo.Act1; // Para integración con corrientes (OceanEnvironment)

namespace Anadromo.Locomotion
{
    /// <summary>
    /// Controlador físico de nado por aleteo.
    /// Se acopla al Rigidbody del XR Origin y lee eventos del FlapDetector.
    /// Orienta el nado con el visor y aplica impulsos al detectar aleteos.
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

        // Acumular eventos hasta el siguiente paso de física, sin esperar al otro brazo.
        private float pendingIntensity;
        private float windowIntensity;
        private float lastImpulseTime = float.NegativeInfinity;
        private InputAction moveStick;
        private InputAction turnStick;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            moveStick ??= new InputAction("Swim Move", InputActionType.Value,
                "<XRController>{LeftHand}/primary2DAxis", expectedControlType: "Vector2");
            turnStick ??= new InputAction("Swim Turn", InputActionType.Value,
                "<XRController>{RightHand}/primary2DAxis", expectedControlType: "Vector2");
            moveStick.Enable();
            turnStick.Enable();
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.AddListener(QueueFlap);
                flapDetector.OnRightFlap.AddListener(QueueFlap);
            }
        }

        private void OnDisable()
        {
            moveStick?.Disable();
            turnStick?.Disable();
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.RemoveListener(QueueFlap);
                flapDetector.OnRightFlap.RemoveListener(QueueFlap);
            }
            pendingIntensity = windowIntensity = 0f;
            lastImpulseTime = float.NegativeInfinity;
        }

        private void OnDestroy()
        {
            moveStick?.Dispose();
            turnStick?.Dispose();
        }

        private void FixedUpdate()
        {
            if (settings == null || salmonBody == null || headTransform == null) return;

            UpdateBodyPitch();
            UpdateBodyYaw();
            ApplyJoystickMovement();
            ApplyCurrents();
            ProcessPendingFlaps();
            LimitVelocities();
        }

        private void ApplyJoystickMovement()
        {
            if (rb.isKinematic || moveStick == null) return;
            Vector2 input = Vector2.ClampMagnitude(moveStick.ReadValue<Vector2>(), 1f);
            if (input.magnitude <= settings.joystickDeadzone) return;

            // Avance según el visor (incluye subida/bajada al mirar); desplazamiento lateral horizontal.
            Vector3 right = Vector3.ProjectOnPlane(headTransform.right, Vector3.up).normalized;
            Vector3 direction = headTransform.forward * input.y + right * input.x;
            if (direction.sqrMagnitude > 1f) direction.Normalize();
            rb.AddForce(direction * settings.joystickAcceleration, ForceMode.Acceleration);
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

        private void QueueFlap(float intensity)
        {
            if (rb.isKinematic || float.IsNaN(intensity) || float.IsInfinity(intensity)) return;
            pendingIntensity = Mathf.Max(pendingIntensity, Mathf.Clamp01(intensity));
        }

        private void ProcessPendingFlaps()
        {
            float intensity = pendingIntensity;
            pendingIntensity = 0f;
            if (rb.isKinematic || intensity <= 0f) return;

            // First arm propels immediately. Nearby events only top up to the stronger stroke.
            if (Time.fixedTime - lastImpulseTime > settings.simultaneityWindow)
            {
                lastImpulseTime = Time.fixedTime;
                windowIntensity = 0f;
            }
            float additionalIntensity = Mathf.Max(0f, intensity - windowIntensity);
            windowIntensity = Mathf.Max(windowIntensity, intensity);
            if (additionalIntensity > 0f) ApplyForwardImpulse(additionalIntensity);
        }

        private void ApplyForwardImpulse(float intensity)
        {
            if (salmonBody == null) return;
            
            // Avanzar en la dirección frontal del cuerpo del salmón
            Vector3 deltaVelocity = salmonBody.forward * (intensity * settings.forwardForceMultiplier / rb.mass);
            Vector3 cappedVelocity = Vector3.ClampMagnitude(rb.linearVelocity + deltaVelocity, settings.maxLinearVelocity);
            rb.AddForce((cappedVelocity - rb.linearVelocity) * rb.mass, ForceMode.Impulse);
        }

        // ─── Movimiento del Cuerpo ───

        private void UpdateBodyYaw()
        {
            // El ángulo horizontal del visor respecto al XR Origin funciona como mando de giro.
            Vector3 localForward = transform.InverseTransformDirection(headTransform.forward);
            float localYaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
            float turnAmount = Mathf.Abs(localYaw) - settings.headTurnDeadzone;
            float stickTurn = turnStick != null ? turnStick.ReadValue<Vector2>().x : 0f;
            if (Mathf.Abs(stickTurn) > settings.joystickDeadzone && !rb.isKinematic)
            {
                float degrees = stickTurn * settings.joystickTurnSpeed * Time.fixedDeltaTime;
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, degrees, 0f));
            }
            else if (turnAmount > 0f && !rb.isKinematic)
            {
                float speedRatio = Mathf.Clamp01(turnAmount / settings.headTurnFullSpeedAngle);
                float degrees = Mathf.Sign(localYaw) * speedRatio *
                                settings.headTurnMaxSpeed * Time.fixedDeltaTime;
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, degrees, 0f));
            }

            // El cuerpo sigue la dirección del visor para que el impulso avance hacia donde mira.
            float newYaw = Mathf.LerpAngle(salmonBody.localEulerAngles.y, localYaw,
                Time.fixedDeltaTime * settings.yawLerpSpeed);
            Vector3 angles = salmonBody.localEulerAngles;
            salmonBody.localEulerAngles = new Vector3(angles.x, newYaw, 0f);
        }

        private void UpdateBodyPitch()
        {
            // El cuerpo sigue la inclinación de la cabeza sin perder el yaw del visor.

            // 1. Obtener el pitch del headset (convirtiéndolo a -180...180)
            float headPitch = headTransform.eulerAngles.x;
            if (headPitch > 180f) headPitch -= 360f;

            // Restringir el pitch máximo para que el salmón no se voltee por completo
            headPitch = Mathf.Clamp(headPitch, -settings.maxPitchAngle, settings.maxPitchAngle);

            // 2. Obtener la inclinación local actual del cuerpo.
            float currentBodyPitch = salmonBody.localEulerAngles.x;
            if (currentBodyPitch > 180f) currentBodyPitch -= 360f;

            // 3. Interpolar suavemente
            float newPitch = Mathf.Lerp(currentBodyPitch, headPitch, Time.fixedDeltaTime * settings.pitchLerpSpeed);

            // Conservar el yaw calculado a partir del visor.
            salmonBody.localEulerAngles = new Vector3(newPitch, salmonBody.localEulerAngles.y, 0f);
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
