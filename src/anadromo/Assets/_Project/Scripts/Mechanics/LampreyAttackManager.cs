using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Anadromo.Locomotion;

namespace Anadromo.Mechanics
{
    public class LampreyAttackManager : MonoBehaviour
    {
        [Header("Dependencias")]
        public ShakeDetector shakeDetector;
        public ShakeSettings settings;
        public Transform playerCamera;
        public Image damageVignette;
        
        [Header("Configuración del Enemigo")]
        public Transform originPoint;
        public float returnSpeed = 2f;
        public float pursuitSpeed = 5f; // Slower than player
        public float cooldownDuration = 5f;
        public float visionRange = 20f;

        [Header("Eventos")]
        public UnityEvent OnAttackStarted;
        public UnityEvent OnAttackEndedSuccess;
        public UnityEvent OnPlayerDied;

        private bool isAttacking = false;
        private bool isReturning = false;
        private bool inCooldown = false;

        private float timeSustainedEnergy = 0f;
        private float attackTimer = 0f;
        private float cooldownTimer = 0f;

        // Variables para el efecto visual
        private Vector3 initialCameraLocalEuler;
        private float cameraShakeTimer = 0f;
        private float cameraShakeFrequency = 10f;
        private float cameraShakeAmplitude = 5f;

        private SimpleFlyCamera playerFlyCamera;

        private void Start()
        {
            if (playerCamera != null)
            {
                playerFlyCamera = playerCamera.GetComponent<SimpleFlyCamera>();
                if (playerFlyCamera == null)
                    playerFlyCamera = playerCamera.GetComponentInParent<SimpleFlyCamera>();
            }

            if (damageVignette != null)
            {
                Color c = damageVignette.color;
                c.a = 0f;
                damageVignette.color = c;
            }
        }

        private void Update()
        {
            if (inCooldown)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    inCooldown = false;
                }
            }

            if (isReturning)
            {
                transform.position = Vector3.Lerp(transform.position, originPoint.position, Time.deltaTime * returnSpeed);
                if (Vector3.Distance(transform.position, originPoint.position) < 0.1f)
                {
                    isReturning = false;
                }
            }
            else if (isAttacking)
            {
                attackTimer += Time.deltaTime;

                // Restricción: Efecto visual en la cámara (Cabeceo Z)
                cameraShakeTimer += Time.deltaTime * cameraShakeFrequency;
                float zTilt = Mathf.Sin(cameraShakeTimer) * cameraShakeAmplitude;
                playerCamera.localEulerAngles = initialCameraLocalEuler + new Vector3(0, 0, zTilt);

                // Feedback UI: Borde rojo (Vignette)
                if (damageVignette != null)
                {
                    Color c = damageVignette.color;
                    // Pulsa entre 0.2 y 0.5 de alfa para simbolizar daño constante
                    c.a = Mathf.Lerp(0.2f, 0.5f, (Mathf.Sin(cameraShakeTimer * 0.5f) + 1f) / 2f);
                    damageVignette.color = c;
                }

                // Lógica de liberación
                if (shakeDetector != null && shakeDetector.HasReachedRequiredEnergy())
                {
                    timeSustainedEnergy += Time.deltaTime;
                    if (timeSustainedEnergy >= settings.timeRequiredToBreakFree)
                    {
                        BreakFree();
                    }
                }
                else
                {
                    timeSustainedEnergy = Mathf.Max(0, timeSustainedEnergy - Time.deltaTime);
                }

                // Lógica de muerte
                if (attackTimer >= settings.maxAttackDuration)
                {
                    Die();
                }
            }
            else if (!inCooldown)
            {
                // Pursuit logic
                if (playerCamera != null)
                {
                    float distanceToPlayer = Vector3.Distance(transform.position, playerCamera.position);
                    if (distanceToPlayer <= visionRange)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, playerCamera.position, Time.deltaTime * pursuitSpeed);
                        transform.LookAt(playerCamera.position);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (inCooldown || isAttacking || isReturning) return;

            if (other.CompareTag("Player") || (playerCamera != null && other.transform == playerCamera))
            {
                StartAttack();
            }
        }

        private void StartAttack()
        {
            isAttacking = true;
            attackTimer = 0f;
            timeSustainedEnergy = 0f;
            
            if (playerCamera != null)
                initialCameraLocalEuler = playerCamera.localEulerAngles;

            // Restricción: Detener movimiento y rotación del jugador
            if (playerFlyCamera != null)
                playerFlyCamera.enabled = false;

            OnAttackStarted?.Invoke();
            Debug.Log("[Anadromo] ¡La lamprea te ha atacado! ¡Sacúdete!");
        }

        private void BreakFree()
        {
            isAttacking = false;
            isReturning = true;
            inCooldown = true;
            cooldownTimer = cooldownDuration;

            if (playerCamera != null)
                playerCamera.localEulerAngles = initialCameraLocalEuler;

            if (damageVignette != null)
            {
                Color c = damageVignette.color;
                c.a = 0f;
                damageVignette.color = c;
            }

            // Restaurar movimiento
            if (playerFlyCamera != null)
                playerFlyCamera.enabled = true;

            OnAttackEndedSuccess?.Invoke();
            Debug.Log("[Anadromo] ¡Te has liberado de la lamprea!");
        }

        private void Die()
        {
            isAttacking = false;
            
            if (playerCamera != null)
                playerCamera.localEulerAngles = initialCameraLocalEuler;

            if (damageVignette != null)
            {
                Color c = damageVignette.color;
                c.a = 0f;
                damageVignette.color = c;
            }
                
            // Restaurar movimiento o mostrar pantalla de muerte
            if (playerFlyCamera != null)
                playerFlyCamera.enabled = true;

            OnPlayerDied?.Invoke();
            Debug.Log("[Anadromo] Has muerto. No te sacudiste a tiempo.");
        }
    }
}
