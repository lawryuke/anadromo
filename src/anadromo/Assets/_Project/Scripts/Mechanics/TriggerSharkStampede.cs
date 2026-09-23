using UnityEngine;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("Anadromo/Trigger Shark Stampede")]
    public class TriggerSharkStampede : MonoBehaviour
    {
        [Header("Condiciones")]
        [Tooltip("El spawner de krills a vigilar. Si se vacía, se cumple la condición A.")]
        public BoxObjectSpawner krillSpawner;

        [Tooltip("Referencia al jugador para contar cuántos krills come. Si come la cantidad requerida, se cumple la condición B.")]
        public PlayerFeeding playerFeeding;

        [Tooltip("Cantidad de krills que el jugador debe comer para cumplir la condición B.")]
        public int krillsRequiredToEat = 6;

        private int krillsEatenCount = 0;
        private bool playerInBox = false;
        private bool hasTriggered = false;

        private void Start()
        {
            // Asegurarnos de que el collider sea trigger
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;

            // Escuchar cada vez que el jugador come
            if (playerFeeding != null)
            {
                playerFeeding.OnPreyConsumed.AddListener(OnPlayerAteKrill);
            }
        }

        private void OnPlayerAteKrill()
        {
            krillsEatenCount++;
            CheckConditions();
        }

        private void OnTriggerEnter(Collider other)
        {
            // Verificamos si es el jugador (usando el componente PlayerFeeding como identificador seguro)
            if (other.GetComponentInParent<PlayerFeeding>() != null)
            {
                playerInBox = true;
                CheckConditions();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerFeeding>() != null)
            {
                playerInBox = false;
            }
        }

        private void Update()
        {
            if (hasTriggered || !playerInBox) return;
            
            // Revisar constantemente si el spawner se vació mientras el jugador está en la caja
            CheckConditions();
        }

        private void CheckConditions()
        {
            if (hasTriggered || !playerInBox) return;

            bool isSpawnerEmpty = false;
            
            if (krillSpawner != null && krillSpawner.GeneratedObjects != null && krillSpawner.GeneratedObjects.Count > 0)
            {
                isSpawnerEmpty = true;
                foreach (var obj in krillSpawner.GeneratedObjects)
                {
                    if (obj != null && obj.activeInHierarchy)
                    {
                        isSpawnerEmpty = false;
                        break;
                    }
                }
            }
            else if (krillSpawner != null) 
            {
                // Si el spawner existe pero no tiene objetos generados
                isSpawnerEmpty = true;
            }

            // Si se cumple ALGUNA de las dos condiciones
            if (isSpawnerEmpty || krillsEatenCount >= krillsRequiredToEat)
            {
                hasTriggered = true;
                TriggerStampede();
            }
        }

        private void TriggerStampede()
        {
            // Encontrar automáticamente todos los controladores de tiburones personalizados
            SharkGroupMovement[] allSharkGroups = Object.FindObjectsByType<SharkGroupMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            int count = 0;
            int totalFound = allSharkGroups.Length;
            
            foreach (var group in allSharkGroups)
            {
                if (group != null)
                {
                    group.IniciarGrupo();
                    count++;
                }
            }
            
            if (count == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] Se intentó iniciar la estampida pero no se encontraron objetos con el script 'SharkGroupMovement' en la escena.");
            }
            else
            {
                Debug.Log($"[{gameObject.name}] ¡Condiciones cumplidas! Estampida iniciada automáticamente para {count} grupos de tiburones.");
            }
        }

        private void OnDestroy()
        {
            if (playerFeeding != null)
            {
                playerFeeding.OnPreyConsumed.RemoveListener(OnPlayerAteKrill);
            }
        }
    }
}
