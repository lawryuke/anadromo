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
        private Anadromo.Systems.PlayerEnergyController vital;
        private Anadromo.AI.PiranhaPlayerTarget target;
        float SwimMultiplier => (vital ? vital.SwimMultiplier : 1f) * (target ? target.SpeedMultiplier : 1f);
        float TurnMultiplier => vital ? vital.TurnMultiplier : 1f;
        [Header("Controles adicionales Quest")]
        public bool enableJoystickMovement = true;
        public bool enableJoystickTurn = true;
        [Min(0f)] public float joystickTurnSpeed = 60f;
        [System.NonSerialized] public Vector3 externalVelocity;

        // Un brazo impulsa en el siguiente paso físico; el segundo solo completa
        // la intensidad de ese ciclo para no duplicar la velocidad.
        private float pendingIntensity;
        private float windowIntensity;
        private float lastImpulseTime = float.NegativeInfinity;
        private bool headTurnInitialized;
        private float headNeutralYaw;
        private float headTurnSpeed;
        private bool headTurnFastZone;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            vital = GetComponent<Anadromo.Systems.PlayerEnergyController>();
            target = GetComponent<Anadromo.AI.PiranhaPlayerTarget>();
        }

        private void OnEnable()
        {
            headTurnInitialized = false;
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.AddListener(HandleLeftFlap);
                flapDetector.OnRightFlap.AddListener(HandleRightFlap);
            }
        }

        private void OnDisable()
        {
            if (vital) vital.Energy.SetSprinting(false);
            if (flapDetector != null)
            {
                flapDetector.OnLeftFlap.RemoveListener(HandleLeftFlap);
                flapDetector.OnRightFlap.RemoveListener(HandleRightFlap);
            }
            pendingIntensity = windowIntensity = 0f;
            lastImpulseTime = float.NegativeInfinity;
            headTurnInitialized = false;
        }

        private void FixedUpdate()
        {
            if (settings == null || salmonBody == null || headTransform == null) return;
            if (vital && !vital.HasEnergy) return;

            UpdateHeadTurn();
            UpdateBodyOrientation();
            ApplyCurrents();
            ProcessPendingFlaps();
            LimitVelocities();
            if (!rb.isKinematic)
            {
                rb.AddForce(externalVelocity * rb.linearDamping * (vital ? vital.CurrentMultiplier : 1f), ForceMode.Acceleration);
                if (vital) vital.Energy.SetSprinting(enableJoystickMovement && QuestSwimInput.IsSprinting &&
                    QuestSwimInput.ReadVelocity(headTransform).sqrMagnitude > 0f);
                if (UnityEngine.XR.XRSettings.isDeviceActive)
                {
                    if (enableJoystickMovement)
                    {
                        Vector3 requested = QuestSwimInput.ReadVelocity(headTransform) * SwimMultiplier;
                        if (requested.sqrMagnitude > 0f)
                        {
                            Vector3 direction = requested.normalized;
                            float extra = Mathf.Max(0f, requested.magnitude - Vector3.Dot(rb.linearVelocity, direction));
                            rb.AddForce(direction * extra, ForceMode.VelocityChange);
                        }
                    }
                    if (enableJoystickTurn)
                        QuestSwimInput.TurnAroundHead(transform, headTransform, rb,
                            QuestSwimInput.ReadTurn() * joystickTurnSpeed * TurnMultiplier * Time.fixedDeltaTime);
                }
            }
        }

        private void ApplyCurrents()
        {
            if (applyOceanCurrents && !rb.isKinematic)
            {
                try
                {
                    Vector3 currentForce = OceanEnvironment.PlayerCurrentAt(rb.position);
                    rb.AddForce(currentForce * rb.linearDamping * (vital ? vital.CurrentMultiplier : 1f), ForceMode.Acceleration);
                }
                catch { }
            }
        }

        // ─── Manejo de Aleteos ───

        private void HandleLeftFlap(float intensity)
        {
            QueueFlap(intensity);
        }

        private void HandleRightFlap(float intensity)
        {
            QueueFlap(intensity);
        }

        private void QueueFlap(float intensity)
        {
            if (rb.isKinematic || (vital && !vital.HasEnergy) || float.IsNaN(intensity) || float.IsInfinity(intensity)) return;
            pendingIntensity = Mathf.Max(pendingIntensity, Mathf.Clamp01(intensity));
        }

        private void ProcessPendingFlaps()
        {
            float intensity = pendingIntensity;
            pendingIntensity = 0f;
            if (rb.isKinematic || intensity <= 0f) return;

            if (Time.fixedTime - lastImpulseTime > settings.simultaneityWindow)
            {
                lastImpulseTime = Time.fixedTime;
                windowIntensity = 0f;
            }
            float additionalIntensity = Mathf.Max(0f, intensity - windowIntensity);
            windowIntensity = Mathf.Max(windowIntensity, intensity);
            if (additionalIntensity > 0f)
                ApplyForwardImpulse(additionalIntensity * settings.forwardOnSingleFlapRatio);
        }

        // ─── Aplicación de Fuerzas ───

        private void ApplyForwardImpulse(float intensity)
        {
            if (salmonBody == null) return;
            
            // Avanzar en la dirección frontal del cuerpo del salmón
            Vector3 deltaVelocity = salmonBody.forward *
                (intensity * settings.forwardForceMultiplier * SwimMultiplier / rb.mass);
            Vector3 cappedVelocity = Vector3.ClampMagnitude(
                rb.linearVelocity + deltaVelocity, settings.maxLinearVelocity * SwimMultiplier);
            rb.AddForce((cappedVelocity - rb.linearVelocity) * rb.mass, ForceMode.Impulse);
        }

        private void UpdateHeadTurn()
        {
            var headset = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            if (rb.isKinematic || !headset.isValid ||
                !headset.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) || !tracked)
            {
                headTurnInitialized = false;
                headTurnSpeed = 0f;
                headTurnFastZone = false;
                return;
            }

            // X negativo en el espacio del XR Origin es la izquierda visible para el jugador.
            Vector3 localForward = transform.InverseTransformDirection(headTransform.forward);
            if (localForward.x * localForward.x + localForward.z * localForward.z < 0.01f)
            {
                headTurnSpeed = 0f;
                return;
            }
            float localYaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;

            // Calibrar el frente físico al activar el nado o recuperar el tracking.
            // La rotación virtual del XR Origin no modifica esta referencia local.
            if (!headTurnInitialized)
            {
                headTurnInitialized = true;
                headNeutralYaw = localYaw;
                headTurnSpeed = 0f;
                headTurnFastZone = false;
                return;
            }

            float yaw = Mathf.DeltaAngle(headNeutralYaw, localYaw);
            float angle = Mathf.Abs(yaw);
            if (angle <= settings.headTurnDeadzone)
            {
                // Detener inmediatamente al volver a la zona de mirada libre.
                headTurnSpeed = 0f;
                headTurnFastZone = false;
                return;
            }

            float fastAngle = Mathf.Max(settings.headTurnDeadzone + 1f, settings.headTurnFastAngle);
            // Dos grados de histéresis evitan alternar velocidades por temblores.
            headTurnFastZone = headTurnFastZone ? angle >= fastAngle - 2f : angle >= fastAngle;
            float targetSpeed = Mathf.Sign(yaw) * (headTurnFastZone
                ? settings.headTurnMaxSpeed : Mathf.Min(settings.headTurnSlowSpeed, settings.headTurnMaxSpeed));
            if (Mathf.Sign(headTurnSpeed) != Mathf.Sign(targetSpeed)) headTurnSpeed = 0f;
            headTurnSpeed = Mathf.MoveTowards(headTurnSpeed, targetSpeed,
                settings.headTurnAcceleration * Time.fixedDeltaTime);

            // Giro negativo = izquierda de la pantalla; positivo = derecha.
            QuestSwimInput.TurnAroundHead(transform, headTransform, rb,
                headTurnSpeed * TurnMultiplier * Time.fixedDeltaTime);
        }

        // ─── Movimiento del Cuerpo ───

        private void UpdateBodyOrientation()
        {
            // El visor gira dentro del XR Origin; el cuerpo debe seguir su orientación
            // local para que el próximo aleteo avance hacia donde mira el jugador.
            Quaternion localHeadRotation = Quaternion.Inverse(transform.rotation) * headTransform.rotation;
            float headPitch = Mathf.DeltaAngle(0f, localHeadRotation.eulerAngles.x);
            float headYaw = Mathf.DeltaAngle(0f, localHeadRotation.eulerAngles.y);
            headPitch = Mathf.Clamp(headPitch, -settings.maxPitchAngle, settings.maxPitchAngle);
            float bodyPitch = Mathf.DeltaAngle(0f, salmonBody.localEulerAngles.x);
            float bodyYaw = Mathf.DeltaAngle(0f, salmonBody.localEulerAngles.y);
            float newPitch = Mathf.LerpAngle(bodyPitch, headPitch, Time.fixedDeltaTime * settings.pitchLerpSpeed);
            float newYaw = Mathf.LerpAngle(bodyYaw, headYaw, Time.fixedDeltaTime * settings.headYawLerpSpeed);
            salmonBody.localRotation = Quaternion.Euler(newPitch, newYaw, 0f);
        }

        private void LimitVelocities()
        {
            // Limitar velocidad lineal
            float maxSpeed = settings.maxLinearVelocity * SwimMultiplier;
            if (rb.linearVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }

            // Limitar velocidad angular
            if (rb.angularVelocity.sqrMagnitude > settings.maxAngularVelocity * settings.maxAngularVelocity)
            {
                rb.angularVelocity = rb.angularVelocity.normalized * settings.maxAngularVelocity;
            }
        }
    }
}
