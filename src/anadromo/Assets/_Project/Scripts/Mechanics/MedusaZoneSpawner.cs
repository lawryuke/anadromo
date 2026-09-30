using UnityEngine;

namespace Anadromo.Mechanics
{
    [RequireComponent(typeof(ZoneLimit))]
    [AddComponentMenu("Anadromo/Logic/Medusa Zone Spawner")]
    public class MedusaZoneSpawner : MonoBehaviour
    {
        [Tooltip("El prefab de la medusa (3D) que quieres que aparezca.")]
        public GameObject medusaPrefab;
        public Transform[] waypoints;
        
        private bool hasSpawned = false;

        void Update()
        {
            // Cuando este objeto (el ZoneLimit) se activa, creamos la medusa
            if (!hasSpawned && gameObject.activeInHierarchy)
            {
                hasSpawned = true;
                if (medusaPrefab != null)
                {
                    // Crear la medusa en el centro de la zona
                    GameObject medusaInstance = Instantiate(medusaPrefab, transform.position, Quaternion.identity);
                    
                    // La zona activa el recorrido; conserva los waypoints del prefab si no hay reemplazo.
                    LuzViajera lv = medusaInstance.GetComponent<LuzViajera>();
                    if (lv != null)
                    {
                        lv.activationZone = GetComponent<ZoneLimit>();
                        if (waypoints != null && waypoints.Length > 0) lv.waypoints = waypoints;
                    }
                }
            }
        }
    }
}
