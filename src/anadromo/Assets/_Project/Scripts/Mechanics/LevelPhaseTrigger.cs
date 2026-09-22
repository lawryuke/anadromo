using UnityEngine;
using UnityEngine.Events;

namespace Anadromo.Logic
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Anadromo/Logic/Level Phase Trigger")]
    public class LevelPhaseTrigger : MonoBehaviour
    {
        public enum TriggerType {
            OnEnter,
            OnExit,
            OnMidKrillsDepleted // (Opcional, si queremos reusar este script y llamarlo por código)
        }

        public TriggerType triggerType = TriggerType.OnEnter;
        [Tooltip("Etiqueta que debe tener el objeto que colisiona para activar el evento (ej: Player). Dejar vacío si vale cualquiera.")]
        public string targetTag = "Player";
        
        [Header("Eventos")]
        public UnityEvent onTriggerEvent;
        
        private bool hasTriggered = false;
        public bool triggerOnlyOnce = true;

        private void Start()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (triggerType != TriggerType.OnEnter) return;
            if (hasTriggered && triggerOnlyOnce) return;

            if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
            {
                TriggerEvent();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (triggerType != TriggerType.OnExit) return;
            if (hasTriggered && triggerOnlyOnce) return;

            if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
            {
                TriggerEvent();
            }
        }

        public void TriggerEvent()
        {
            if (hasTriggered && triggerOnlyOnce) return;
            hasTriggered = true;
            onTriggerEvent.Invoke();
        }
    }
}
