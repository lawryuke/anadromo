using UnityEngine;

namespace Anadromo.Mechanics
{
    public class Prey : MonoBehaviour
    {
        [Header("Prey Settings")]
        [Tooltip("Cantidad de energía que el jugador recupera al comer esto.")]
        public float energyValue = 15f;
        public bool IsConsumed { get; private set; }

        public bool TryConsume()
        {
            if (IsConsumed || !isActiveAndEnabled) return false;
            IsConsumed = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }
    }
}
