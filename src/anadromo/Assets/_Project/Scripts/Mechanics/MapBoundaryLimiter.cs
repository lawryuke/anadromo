using UnityEngine;
using Anadromo.Logic; // For ZoneLimit

namespace Anadromo.Mechanics
{
    /// <summary>
    /// Limita el mapa usando un ZoneLimit. Si el jugador entra y luego intenta salir, 
    /// lo hace girar automáticamente hacia un punto de referencia y reproduce un sonido 3D.
    /// </summary>
    public class MapBoundaryLimiter : MonoBehaviour
    {
        [Header("Configuración de Zonas")]
        [Tooltip("La zona que representa el límite del mapa.")]
        public ZoneLimit mapLimit;
        [Tooltip("El punto hacia el que el jugador será rotado al intentar salir.")]
        public Transform referencePoint;

        [Header("Efectos")]
        public AudioClip limitSound;
        [Range(0f, 1f)] public float soundVolume = 1f;
        [Tooltip("Distancia a la que se generará el sonido (en la dirección que miraba el jugador).")]
        public float soundDistance = 10f;

        [Header("Referencias del Jugador")]
        [Tooltip("La cámara del jugador (cabeza en VR).")]
        public Transform playerCamera;
        [Tooltip("La raíz o rig del jugador que debe ser rotado (ej. XROrigin).")]
        public Transform rigToRotate;

        private bool hasEntered = false;
        private bool isHandlingExit = false;

        void Update()
        {
            if (mapLimit == null || playerCamera == null) return;

            bool isInside = mapLimit.Contains(playerCamera.position);

            // Esperar a que el jugador entre por primera vez a la zona válida
            if (!hasEntered)
            {
                if (isInside) hasEntered = true;
                return;
            }

            // Si el jugador intenta salir
            if (!isInside && !isHandlingExit)
            {
                isHandlingExit = true;
                HandleBoundaryExit();
            }
            // Si regresa dentro de la zona, resetear para permitir futuras colisiones con el límite
            else if (isInside)
            {
                isHandlingExit = false;
            }
        }

        void HandleBoundaryExit()
        {
            // 1. Reproducir sonido a X metros de distancia en la dirección que estaba viendo el jugador
            if (limitSound != null)
            {
                Vector3 soundPosition = playerCamera.position + playerCamera.forward * soundDistance;
                GameObject audioObj = new GameObject("BoundarySound");
                audioObj.transform.position = soundPosition;
                AudioSource src = audioObj.AddComponent<AudioSource>();
                src.clip = limitSound;
                src.volume = soundVolume;
                src.spatialBlend = 1f; // 100% 3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 5f;
                src.maxDistance = 50f;
                src.Play();
                Destroy(audioObj, limitSound != null && limitSound.length > 0 ? limitSound.length + 0.1f : 2f);
            }

            // 2. Rotar al jugador para que apunte hacia el punto de referencia
            if (referencePoint != null && rigToRotate != null)
            {
                Vector3 directionToTarget = referencePoint.position - playerCamera.position;
                directionToTarget.y = 0; // Mantener la rotación solo en el eje Y (horizontal)

                if (directionToTarget.sqrMagnitude > 0.001f)
                {
                    // Dirección actual de la cámara
                    Vector3 camForward = playerCamera.forward;
                    camForward.y = 0;

                    if (camForward.sqrMagnitude > 0.001f)
                    {
                        // Calcular cuántos grados debe girar el rig para que la cámara mire al objetivo
                        float angleToTurn = Vector3.SignedAngle(camForward, directionToTarget, Vector3.up);
                        
                        // Rotar todo el rig alrededor de la posición actual de la cabeza para no desfasar al jugador en VR
                        rigToRotate.RotateAround(playerCamera.position, Vector3.up, angleToTurn);
                    }
                }
            }
        }
    }
}
