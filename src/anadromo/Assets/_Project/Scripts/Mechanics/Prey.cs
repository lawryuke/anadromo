using UnityEngine;

namespace Anadromo.Mechanics
{
    public class Prey : MonoBehaviour
    {
        [Header("Prey Settings")]
        [Tooltip("Cantidad de energía que el jugador recupera al comer esto.")]
        public float energyValue = 15f;
    }
}
