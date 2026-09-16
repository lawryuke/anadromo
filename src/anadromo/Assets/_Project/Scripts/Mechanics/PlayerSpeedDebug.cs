using UnityEngine;

namespace Anadromo.Mechanics
{
    public class PlayerSpeedDebug : MonoBehaviour
    {
        [Header("Medidor de Velocidad (Metros x Segundo)")]
        [Tooltip("Esta es tu velocidad real calculada cuadro por cuadro.")]
        public float currentSpeed;

        private Vector3 lastPosition;

        private void Start()
        {
            lastPosition = transform.position;
        }

        private void Update()
        {
            // Calculamos cuánta distancia física se movió en un frame
            float distance = Vector3.Distance(transform.position, lastPosition);
            
            // Lo dividimos entre el tiempo para sacar metros por segundo reales
            if (Time.deltaTime > 0)
            {
                currentSpeed = distance / Time.deltaTime;
            }

            lastPosition = transform.position;
        }
    }
}
